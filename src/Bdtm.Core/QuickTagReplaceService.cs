namespace Bdtm.Core;

/// <summary>
/// Finds low-frequency tags that share the same category token as the selected tag,
/// suitable as replacement source candidates.
/// </summary>
public static class QuickTagReplaceService
{
    /// <summary>
    /// Returns tags from <paramref name="allTags"/> that share the same category token
    /// as <paramref name="selectedTag"/> and appear fewer than <paramref name="threshold"/> times.
    /// The selected tag itself is excluded from results.
    /// </summary>
    public static List<string> GetReplacementSourceTags(
        IReadOnlyList<string> allTags,
        string selectedTag,
        int threshold)
    {
        if (allTags == null)
            throw new ArgumentNullException(nameof(allTags));

        string normalizedSelected = NormalizeTag(selectedTag);
        if (string.IsNullOrEmpty(normalizedSelected))
            return new List<string>();

        string category = GetCategoryToken(normalizedSelected);
        if (string.IsNullOrEmpty(category))
            return new List<string>();

        // Build frequency map from the raw tag list
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string tag in allTags)
        {
            if (tag == null)
                continue;
            string normalized = NormalizeTag(tag);
            if (normalized.Length == 0)
                continue;
            frequency.TryGetValue(normalized, out int count);
            frequency[normalized] = count + 1;
        }

        return frequency
            .Where(kvp => kvp.Value < threshold)
            .Where(kvp => !string.Equals(kvp.Key, normalizedSelected, StringComparison.OrdinalIgnoreCase))
            .Where(kvp => IsSameCategory(kvp.Key, category))
            .Select(kvp => kvp.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Normalize a tag: trim whitespace and lowercase.</summary>
    private static string NormalizeTag(string tag)
    {
        return (tag ?? string.Empty).Trim().ToLowerInvariant();
    }

    /// <summary>Extract the last word (category token) from a tag.</summary>
    private static string GetCategoryToken(string tag)
    {
        string normalized = tag.Replace('_', ' ').Trim();
        if (normalized.Length == 0)
            return string.Empty;

        string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    /// <summary>
    /// Check if a tag belongs to the given category.
    /// A tag matches if it equals the category, or ends with " {category}" or "_{category}".
    /// </summary>
    private static bool IsSameCategory(string tag, string category)
    {
        return string.Equals(tag, category, StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith(" " + category, StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_" + category, StringComparison.OrdinalIgnoreCase);
    }
}
