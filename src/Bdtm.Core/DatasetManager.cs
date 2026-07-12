using System.Collections.Concurrent;

namespace Bdtm.Core;

public enum TagAddingType
{
    Top,
    Center,
    Down,
    Custom,
}

public enum DatasetOrderType
{
    Name,
    ImageModifyTime,
    TagsModifyTime,
}

public sealed class DatasetLoadOptions
{
    public bool LoadPreviewImages { get; set; }
    public bool ReadMetadata { get; set; }
    public int PreviewSize { get; set; } = 130;
    public string SeparatorOnLoad { get; set; } = ",";
    public string SeparatorOnSave { get; set; } = ", ";
    public bool FixTagsOnSaveLoad { get; set; } = true;
    public string DefaultTagsFileExtension { get; set; } = "txt";
    public string[] TagFileExtensions { get; set; } = { "txt", "caption" };
}

/// <summary>
/// Portable dataset manager: folder of images + sidecar tag files.
/// No WinForms BindingSource, ImageList, or System.Drawing previews.
/// Preview is left to the UI via ImageFilePath.
/// </summary>
public sealed class DatasetManager
{
    private readonly ConcurrentDictionary<string, DataItem> _dataSet =
        new(StringComparer.OrdinalIgnoreCase);

    private int _originalHash;

    public IReadOnlyDictionary<string, DataItem> DataSet => _dataSet;
    public string DatasetRoot { get; private set; } = string.Empty;
    public DatasetLoadOptions Options { get; private set; } = new();

    /// <summary>Progress: current, total.</summary>
    public event Action<int, int>? LoadingProgressChanged;

    public bool LoadFromFolder(string folder, DatasetLoadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return false;

        Options = options ?? new DatasetLoadOptions();
        _dataSet.Clear();

        string[] imgs = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(MediaExtensions.IsSupportedMedia)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imgs.Length == 0)
            return false;

        int progress = 0;
        object progressLock = new();

        Parallel.ForEach(imgs, path =>
        {
            var item = DataItem.Create(path, Options);
            if (_dataSet.TryAdd(item.ImageFilePath, item))
            {
                lock (progressLock)
                {
                    progress++;
                    LoadingProgressChanged?.Invoke(progress, imgs.Length);
                }
            }
        });

        DatasetRoot = Path.GetFullPath(folder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        UpdateDatasetHash();
        return _dataSet.Count > 0;
    }

    public Task<bool> LoadFromFolderAsync(string folder, DatasetLoadOptions? options = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return LoadFromFolder(folder, options);
        }, ct);

    public bool SaveAll()
    {
        bool saved = false;
        foreach (var item in _dataSet.Values)
        {
            if (!item.IsModified(Options.SeparatorOnSave))
                continue;

            item.Tags.Deduplicate();
            string promptText = item.Tags.Format(Options.SeparatorOnSave);
            string textPath = item.TextFilePath;
            if (string.IsNullOrEmpty(textPath))
            {
                textPath = Path.Combine(
                    Path.GetDirectoryName(item.ImageFilePath) ?? DatasetRoot,
                    item.Name + "." + Options.DefaultTagsFileExtension);
                item.TextFilePath = textPath;
            }

            File.WriteAllText(textPath, promptText);
            item.AcceptCurrentTagsAsSaved(Options.SeparatorOnSave);
            saved = true;
        }

        if (saved)
            UpdateDatasetHash();
        return saved;
    }

    public bool Remove(string imagePath)
    {
        if (!_dataSet.TryRemove(imagePath, out var item))
            return false;
        item.Tags.Clear();
        UpdateDatasetHash();
        return true;
    }

    /// <summary>
    /// Adds image paths into the dataset (e.g. cropped region files).
    /// Skips missing, unsupported, and already-present paths.
    /// </summary>
    public IReadOnlyList<string> AddImages(IEnumerable<string> paths)
    {
        if (paths is null)
            return Array.Empty<string>();

        var added = new List<string>();
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;
            if (!MediaExtensions.IsSupportedMedia(path))
                continue;

            string full = Path.GetFullPath(path);
            if (_dataSet.ContainsKey(full))
                continue;

            var item = DataItem.Create(full, Options);
            item.ImageFilePath = full;
            if (_dataSet.TryAdd(item.ImageFilePath, item))
                added.Add(item.ImageFilePath);
        }

        if (added.Count > 0)
            UpdateDatasetHash();
        return added;
    }

    public void AddTagToAll(string tag, bool skipExist = true, bool useFilter = false)
    {
        foreach (var item in Enumerate(useFilter))
        {
            if (skipExist && item.Tags.Contains(tag))
                continue;
            item.Tags.Add(tag, skipIfExists: skipExist);
        }
    }

    public void DeleteTagFromAll(string tag, bool useFilter = false)
    {
        foreach (var item in Enumerate(useFilter))
            item.Tags.RemoveAll(tag);
    }

    public void ReplaceTagInAll(string srcTag, string dstTag, bool useFilter = false)
    {
        foreach (var item in Enumerate(useFilter))
            item.Tags.Replace(srcTag, dstTag);
    }

    public IReadOnlyList<DataItem> GetDataSource(DatasetOrderType orderBy = DatasetOrderType.Name)
    {
        IEnumerable<DataItem> q = _dataSet.Values;
        q = orderBy switch
        {
            DatasetOrderType.ImageModifyTime => q.OrderBy(a => a.ImageModifyTime).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            DatasetOrderType.TagsModifyTime => q.OrderBy(a => a.TagsModifyTime).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            _ => q.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
        };
        return q.ToList();
    }

    public bool IsDataSetChanged() => !Equals(_originalHash, ComputeHash());

    public void UpdateDatasetHash() => _originalHash = ComputeHash();

    private IEnumerable<DataItem> Enumerate(bool useFilter)
    {
        // MVP: filter support reserved; currently returns all.
        _ = useFilter;
        return _dataSet.Values;
    }

    private int ComputeHash()
    {
        unchecked
        {
            int hash = 17;
            foreach (var item in _dataSet.Values.OrderBy(a => a.ImageFilePath, StringComparer.OrdinalIgnoreCase))
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(item.ImageFilePath);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(item.Tags.Format(Options.SeparatorOnSave));
            }
            return hash;
        }
    }

    public sealed class DataItem
    {
        public string Name { get; set; } = string.Empty;
        public TagList Tags { get; set; } = new();
        public string TextFilePath { get; set; } = string.Empty;
        public string ImageFilePath { get; set; } = string.Empty;
        public DateTime ImageModifyTime { get; set; }
        public DateTime TagsModifyTime { get; set; }

        public bool IsModified(string separatorOnSave) => Tags.IsModified(separatorOnSave);

        public static DataItem Create(string imagePath, DatasetLoadOptions options)
        {
            var item = new DataItem
            {
                ImageFilePath = imagePath,
                Name = Path.GetFileNameWithoutExtension(imagePath),
                ImageModifyTime = File.GetLastWriteTime(imagePath),
            };

            string? dir = Path.GetDirectoryName(imagePath);
            string textPath = string.Empty;
            foreach (string ext in options.TagFileExtensions ?? Array.Empty<string>())
            {
                string candidate = Path.Combine(dir ?? string.Empty, item.Name + "." + ext.Trim().TrimStart('.'));
                if (File.Exists(candidate))
                {
                    textPath = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(textPath))
                textPath = Path.Combine(dir ?? string.Empty, item.Name + "." + options.DefaultTagsFileExtension.Trim().TrimStart('.'));

            item.TextFilePath = textPath;
            item.LoadTagsFromFile(options);
            return item;
        }

        public void LoadTagsFromFile(DatasetLoadOptions options)
        {
            if (File.Exists(TextFilePath))
            {
                TagsModifyTime = File.GetLastWriteTime(TextFilePath);
                string text = File.ReadAllText(TextFilePath);
                Tags.LoadFromText(text, options.FixTagsOnSaveLoad, options.SeparatorOnLoad);
                Tags.AcceptAsSaved(Tags.Format(options.SeparatorOnSave));
            }
            else
            {
                TagsModifyTime = DateTime.MinValue;
                Tags.Clear();
                Tags.AcceptAsSaved(string.Empty);
            }
        }

        public void AcceptCurrentTagsAsSaved(string separatorOnSave)
        {
            TagsModifyTime = File.Exists(TextFilePath) ? File.GetLastWriteTime(TextFilePath) : DateTime.MinValue;
            Tags.AcceptAsSaved(Tags.Format(separatorOnSave));
        }

        public void DeduplicateTags() => Tags.Deduplicate();
    }
}
