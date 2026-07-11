namespace Bdtm.Core;

/// <summary>Portable tag entry without WinForms binding interfaces.</summary>
public sealed class TagItem
{
    public string Tag { get; set; } = string.Empty;
    public float Weight { get; set; } = 1f;

    public TagItem()
    {
    }

    public TagItem(string tag, float weight = 1f)
    {
        Tag = tag ?? string.Empty;
        Weight = weight;
    }

    public TagItem Clone() => new(Tag, Weight);

    public override string ToString()
    {
        if (Math.Abs(Weight - 1f) < 0.0001f)
            return Tag;
        // Match common booru weight serialization used by the original EditableTag.ToString
        return $"({Tag}:{Weight.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    }
}

/// <summary>Ordered mutable tag list with serialize/deserialize helpers.</summary>
public sealed class TagList
{
    private readonly List<TagItem> _items = new();
    private string _savedSnapshot = string.Empty;

    public IReadOnlyList<TagItem> Items => _items;
    public int Count => _items.Count;
    public TagItem this[int index] => _items[index];

    public event Action? Changed;

    public void Clear()
    {
        _items.Clear();
        RaiseChanged();
    }

    public void LoadFromPromptItems(IEnumerable<PromptItem> tags)
    {
        _items.Clear();
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Text))
                continue;
            _items.Add(new TagItem(tag.Text, tag.Weight));
        }
        AcceptAsSaved(Format(", "));
        RaiseChanged();
    }

    public void LoadFromText(string text, bool fixTagsForWeight, string separatorOnLoad)
    {
        var parsed = PromptParser.ParsePrompt(text ?? string.Empty, fixTagsForWeight, separatorOnLoad);
        LoadFromPromptItems(parsed);
    }

    public string Format(string separatorOnSave)
    {
        string sep = PromptParser.UnescapeSeparator(separatorOnSave);
        return string.Join(sep, _items.Select(i => i.ToString()));
    }

    public void AcceptAsSaved(string snapshotText)
    {
        _savedSnapshot = snapshotText ?? string.Empty;
    }

    public bool IsModified(string separatorOnSave) =>
        !string.Equals(Format(separatorOnSave), _savedSnapshot, StringComparison.Ordinal);

    public bool Contains(string tag) =>
        _items.Any(i => string.Equals(i.Tag, tag, StringComparison.Ordinal));

    public int IndexOf(string tag) =>
        _items.FindIndex(i => string.Equals(i.Tag, tag, StringComparison.Ordinal));

    public void Add(string tag, bool skipIfExists = true)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        if (skipIfExists && Contains(tag))
            return;
        _items.Add(new TagItem(tag.Trim()));
        RaiseChanged();
    }

    public void Insert(int index, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        index = Math.Clamp(index, 0, _items.Count);
        _items.Insert(index, new TagItem(tag.Trim()));
        RaiseChanged();
    }

    public bool Remove(string tag)
    {
        int idx = IndexOf(tag);
        if (idx < 0)
            return false;
        _items.RemoveAt(idx);
        RaiseChanged();
        return true;
    }

    public int RemoveAll(string tag)
    {
        int removed = _items.RemoveAll(i => string.Equals(i.Tag, tag, StringComparison.Ordinal));
        if (removed > 0)
            RaiseChanged();
        return removed;
    }

    public bool Replace(string src, string dst)
    {
        bool changed = false;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!string.Equals(_items[i].Tag, src, StringComparison.Ordinal))
                continue;
            _items[i].Tag = dst;
            changed = true;
        }
        if (changed)
            RaiseChanged();
        return changed;
    }

    public void Deduplicate()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool changed = false;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            string tag = _items[i].Tag;
            if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
            {
                _items.RemoveAt(i);
                changed = true;
            }
        }
        if (changed)
            RaiseChanged();
    }

    public void SetTags(IEnumerable<string> tags)
    {
        _items.Clear();
        foreach (string tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _items.Add(new TagItem(tag.Trim()));
        }
        RaiseChanged();
    }

    /// <summary>Replace list preserving per-tag weights.</summary>
    public void SetTags(IEnumerable<(string Tag, float Weight)> tags)
    {
        _items.Clear();
        foreach (var (tag, weight) in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            _items.Add(new TagItem(tag.Trim(), weight));
        }
        RaiseChanged();
    }

    public TagList Clone()
    {
        var clone = new TagList();
        foreach (var item in _items)
            clone._items.Add(item.Clone());
        clone._savedSnapshot = _savedSnapshot;
        return clone;
    }

    private void RaiseChanged() => Changed?.Invoke();
}
