using System.Net.Http.Headers;
using System.Text.Json;

namespace Bdtm.Core;

public sealed class DanbooruWikiPage
{
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public IReadOnlyList<string> OtherNames { get; init; } = Array.Empty<string>();
    public DateTimeOffset? UpdatedAt { get; init; }
    public string Url { get; init; } = string.Empty;
}

public sealed class DanbooruWikiClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public DanbooruWikiClient() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(12) }, true)
    {
    }

    public DanbooruWikiClient(HttpClient client) : this(client, false)
    {
    }

    private DanbooruWikiClient(HttpClient client, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Bdtm.Linux/0.2 (+https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus)");
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DanbooruWikiPage?> GetWikiPageAsync(string tag, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        try
        {
            string normalized = NormalizeTag(tag);
            string url = "https://danbooru.donmai.us/wiki_pages.json?search[title]="
                + Uri.EscapeDataString(normalized)
                + "&limit=1";

            using var response = await _client.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var item = doc.RootElement[0];
            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? normalized : normalized;
            string body = item.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
            DateTimeOffset? updated = null;
            if (item.TryGetProperty("updated_at", out var u) && u.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(u.GetString(), out var parsed))
                updated = parsed;

            var others = new List<string>();
            if (item.TryGetProperty("other_names", out var on) && on.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in on.EnumerateArray())
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        others.Add(s);
                }
            }

            return new DanbooruWikiPage
            {
                Title = title,
                Body = body,
                OtherNames = others,
                UpdatedAt = updated,
                Url = GetWikiUrl(normalized),
            };
        }
        catch
        {
            return null;
        }
    }

    public static string NormalizeTag(string tag) =>
        (tag ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');

    public static string GetWikiUrl(string tag) =>
        "https://danbooru.donmai.us/wiki_pages/" + Uri.EscapeDataString(NormalizeTag(tag));

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
