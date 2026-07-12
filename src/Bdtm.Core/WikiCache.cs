using System.Text.Json;

namespace Bdtm.Core;

/// <summary>
/// Disk cache for Danbooru wiki pages under DataDir/cache/wiki/.
/// </summary>
public sealed class WikiCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;
    private readonly TimeSpan _ttl;

    public WikiCache(string? root = null, TimeSpan? ttl = null)
    {
        _root = root ?? Path.Combine(AppPaths.DataDir, "cache", "wiki");
        _ttl = ttl ?? TimeSpan.FromDays(7);
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public DanbooruWikiPage? TryGet(string tag)
    {
        string path = GetPath(tag);
        if (!File.Exists(path))
            return null;

        try
        {
            var entry = JsonSerializer.Deserialize<WikiCacheEntry>(File.ReadAllText(path), JsonOptions);
            if (entry is null || entry.Page is null)
                return null;
            if (DateTimeOffset.UtcNow - entry.CachedAtUtc > _ttl)
                return null;
            return entry.Page;
        }
        catch
        {
            return null;
        }
    }

    public void Set(string tag, DanbooruWikiPage page)
    {
        if (page is null) return;
        string path = GetPath(tag);
        Directory.CreateDirectory(_root);
        var entry = new WikiCacheEntry
        {
            CachedAtUtc = DateTimeOffset.UtcNow,
            Page = page,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(entry, JsonOptions));
    }

    public void Invalidate(string tag)
    {
        string path = GetPath(tag);
        if (File.Exists(path))
            File.Delete(path);
    }

    public string GetPath(string tag)
    {
        string key = DanbooruWikiClient.NormalizeTag(tag);
        // filesystem-safe
        foreach (char c in Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        if (key.Length == 0) key = "_empty";
        return Path.Combine(_root, key + ".json");
    }

    private sealed class WikiCacheEntry
    {
        public DateTimeOffset CachedAtUtc { get; set; }
        public DanbooruWikiPage? Page { get; set; }
    }
}
