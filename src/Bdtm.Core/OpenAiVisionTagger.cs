using System.Text.RegularExpressions;

namespace Bdtm.Core;

public sealed class LlmTagItem
{
    public string Tag { get; init; } = string.Empty;
    public float Confidence { get; init; } = 1f;
}

public sealed class LlmTagResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string RawText { get; init; } = string.Empty;
    public IReadOnlyList<LlmTagItem> Tags { get; init; } = Array.Empty<LlmTagItem>();
    public double ElapsedMilliseconds { get; init; }
}

/// <summary>
/// OpenAI-compatible vision tagger. Uses <see cref="OpenAiVisionClient"/> for chat+image,
/// then StripThinking + ParseTags (P5.1 behavior).
/// </summary>
public sealed class OpenAiVisionTagger : IDisposable
{
    private readonly OpenAiVisionClient _client;

    public OpenAiVisionTagger(LlmSettings settings, HttpClient? http = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _client = new OpenAiVisionClient(settings, http);
    }

    public LlmSettings Settings { get; }

    public async Task<LlmTagResult> TagImageAsync(string imagePath, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(Settings.Endpoint))
                return Fail("LLM Endpoint 未配置", sw);
            if (string.IsNullOrWhiteSpace(Settings.VisionModel))
                return Fail("Vision 模型未配置", sw);
            if (!File.Exists(imagePath))
                return Fail("图片不存在: " + imagePath, sw);

            byte[] bytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
            string mime = GuessMime(imagePath);

            OpenAiVisionCompletionResult completion = await _client.CompleteAsync(
                new OpenAiVisionCompletionRequest
                {
                    SystemPrompt = Settings.SystemPrompt ?? string.Empty,
                    UserPrompt = Settings.UserPrompt ?? string.Empty,
                    ImageData = bytes,
                    ContentType = mime,
                },
                ct).ConfigureAwait(false);

            if (!completion.Success)
                return Fail(completion.ErrorMessage ?? "LLM error", sw);

            string text = StripThinking(completion.Text).Trim();
            var tags = ParseTags(text, Settings);
            sw.Stop();
            return new LlmTagResult
            {
                Success = true,
                RawText = text,
                Tags = tags,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message, sw);
        }
    }

    public static List<LlmTagItem> ParseTags(string text, LlmSettings settings)
    {
        var result = new List<LlmTagItem>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        if (settings.SplitTags)
        {
            string splitter = string.IsNullOrEmpty(settings.Splitter) ? "," : settings.Splitter;
            foreach (string part in text.Split(new[] { splitter }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tag = NormalizeTag(part);
                if (tag.Length > 0)
                    result.Add(new LlmTagItem { Tag = tag, Confidence = 1f });
            }
        }
        else
        {
            string tag = NormalizeTag(text);
            if (tag.Length > 0)
                result.Add(new LlmTagItem { Tag = tag, Confidence = 1f });
        }

        return result
            .GroupBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string NormalizeTag(string raw)
    {
        string t = (raw ?? string.Empty).Trim().Trim('`', '"', '\'');
        t = Regex.Replace(t, @"^\d+[\.\)]\s*", ""); // strip list numbers
        t = t.Replace(' ', '_').ToLowerInvariant();
        return t.Trim(',', ';', '.', ' ');
    }

    private static string StripThinking(string text)
    {
        // remove <think>...</think> blocks if present
        return Regex.Replace(text, "<think>[\\s\\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
    }

    private static string GuessMime(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }

    private static LlmTagResult Fail(string err, System.Diagnostics.Stopwatch sw)
    {
        sw.Stop();
        return new LlmTagResult
        {
            Success = false,
            ErrorMessage = err,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
        };
    }

    public void Dispose() => _client.Dispose();
}
