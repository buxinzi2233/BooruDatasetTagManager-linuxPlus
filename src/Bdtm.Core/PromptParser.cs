using System.Globalization;
using System.Text.RegularExpressions;

namespace Bdtm.Core;

/// <summary>
/// Portable prompt/tag parser adapted from the WinForms PromptParser.
/// </summary>
public static class PromptParser
{
    private static readonly Regex Attention = new(
        @"\\\(|\\\)|\\\[|\\]|\\\\|\\|\(|\[|:\s*([+-]?[.\d]+)\s*\)|\)|]|[^\\()\[\]:]+|:",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Break = new(@"\s*\bBREAK\b\s*", RegexOptions.Compiled | RegexOptions.Multiline);

    public static float RoundBracketMultiplier { get; set; } = 1.1f;
    public static float SquareBracketMultiplier { get; set; } = 1f / 1.1f;

    public static List<PromptItem> ParsePrompt(string promptString, bool fixTagsForWeight, string splitSeparator = ",")
    {
        splitSeparator = UnescapeSeparator(splitSeparator);
        if (fixTagsForWeight)
            return ParsePromptWeight(promptString, splitSeparator);

        var result = new List<PromptItem>();
        string[] tags = promptString.Split(new[] { splitSeparator }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            string textTag = tag.ToLowerInvariant().Trim();
            if (result.FindIndex(a => a.Text == textTag) == -1)
                result.Add(new PromptItem(textTag, 1f));
        }
        return result;
    }

    public static List<PromptItem> ParsePromptWeight(string promptString, string splitSeparator = ",")
    {
        splitSeparator = UnescapeSeparator(splitSeparator);
        var res = new List<PromptItem>();
        var roundBrackets = new List<int>();
        var squareBrackets = new List<int>();
        var result = new List<PromptItem>();

        void MultiplyRange(int startPosition, float multiplier)
        {
            for (int i = startPosition; i < res.Count; i++)
                res[i].Weight *= multiplier;
        }

        foreach (Match m in Attention.Matches(promptString ?? string.Empty))
        {
            string text = m.Groups[0].Value;
            string weight = m.Groups[1].Value;

            if (text.StartsWith('\\'))
                res.Add(new PromptItem(text[1..], 1.0f));
            else if (text == "(")
                roundBrackets.Add(res.Count);
            else if (text == "[")
                squareBrackets.Add(res.Count);
            else if (!string.IsNullOrEmpty(weight) && roundBrackets.Count > 0)
                MultiplyRange(Pop(roundBrackets), (float)Convert.ToDouble(weight, CultureInfo.InvariantCulture));
            else if (text == ")" && roundBrackets.Count > 0)
                MultiplyRange(Pop(roundBrackets), RoundBracketMultiplier);
            else if (text == "]" && squareBrackets.Count > 0)
                MultiplyRange(Pop(squareBrackets), SquareBracketMultiplier);
            else
            {
                string[] parts = Break.Split(text);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                        res.Add(new PromptItem("BREAK", -1f));
                    res.Add(new PromptItem(parts[i], 1.0f));
                }
            }
        }

        foreach (int item in roundBrackets)
            MultiplyRange(item, RoundBracketMultiplier);
        foreach (int item in squareBrackets)
            MultiplyRange(item, SquareBracketMultiplier);

        if (res.Count == 0)
            res.Add(new PromptItem("", 1f));

        int p = 0;
        while (p + 1 < res.Count)
        {
            if (Math.Abs(res[p].Weight - res[p + 1].Weight) < float.Epsilon)
            {
                res[p].Text += res[p + 1].Text;
                res.RemoveAt(p + 1);
            }
            else
                p += 1;
        }

        foreach (var item in res)
        {
            string[] clearedTags = item.Text.Split(new[] { splitSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tag in clearedTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                string textTag = tag.Replace('_', ' ').ToLowerInvariant().Trim();
                int tagIndex = result.FindIndex(a => a.Text == textTag);
                if (tagIndex != -1)
                    result[tagIndex].Weight *= RoundBracketMultiplier * item.Weight;
                else
                    result.Add(new PromptItem(textTag, item.Weight));
            }
        }

        return result;
    }

    public static string UnescapeSeparator(string separator) =>
        (separator ?? ",").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");

    private static int Pop(List<int> stack)
    {
        int last = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return last;
    }
}

public sealed class PromptItem
{
    public string Text { get; set; }
    public float Weight { get; set; }

    public PromptItem(string text, float weight)
    {
        Text = text;
        Weight = weight;
    }
}
