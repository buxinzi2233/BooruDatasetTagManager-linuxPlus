using System.Text;

namespace Bdtm.Core;

/// <summary>
/// Portable subset of WinForms ChineseTagLookupService for zh-CN display names.
/// CSV format: english_tag,chinese1|chinese2|...
/// </summary>
public sealed class ChineseTagLookup
{
    private readonly Dictionary<string, string> _englishToChinese =
        new(StringComparer.OrdinalIgnoreCase);

    public static ChineseTagLookup Empty { get; } = new();

    public int Count => _englishToChinese.Count;

    public static ChineseTagLookup LoadFromFile(string filePath, bool fixTags = true)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return Empty;

        var lookup = new ChineseTagLookup();
        foreach (string raw in File.ReadLines(filePath, Encoding.UTF8))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
                continue;

            int sep = line.IndexOf(',');
            if (sep <= 0 || sep >= line.Length - 1)
                continue;

            string english = NormalizeEnglish(line[..sep], fixTags);
            string chinesePart = line[(sep + 1)..];
            string[] names = chinesePart.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (english.Length == 0 || names.Length == 0)
                continue;

            if (!lookup._englishToChinese.ContainsKey(english))
                lookup._englishToChinese[english] = names[0];
        }

        return lookup;
    }

    public string GetChinese(string? englishTag)
    {
        if (string.IsNullOrWhiteSpace(englishTag))
            return string.Empty;

        string key = NormalizeEnglish(englishTag, fixTags: false);
        if (_englishToChinese.TryGetValue(key, out string? zh))
            return zh;

        string underscored = NormalizeEnglish(englishTag, fixTags: true);
        return _englishToChinese.TryGetValue(underscored, out zh) ? zh : string.Empty;
    }

    private static string NormalizeEnglish(string tag, bool fixTags)
    {
        string t = (tag ?? string.Empty).Trim().ToLowerInvariant();
        if (fixTags)
            t = t.Replace(' ', '_');
        return t;
    }
}
