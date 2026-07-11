using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
/// Minimal OpenAI-compatible multimodal chat client for vision tagging.
/// Uses /chat/completions with image_url data URLs (no OpenAI SDK / WinForms).
/// </summary>
public sealed class OpenAiVisionTagger : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenAiVisionTagger(LlmSettings settings, HttpClient? http = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (http is null)
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(10, settings.TimeoutSeconds)) };
            _ownsHttp = true;
        }
        else
        {
            _http = http;
            _ownsHttp = false;
        }
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
            string b64 = Convert.ToBase64String(bytes);
            string dataUrl = $"data:{mime};base64,{b64}";

            string baseUrl = Settings.Endpoint.TrimEnd('/');
            if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                && !baseUrl.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                // allow either .../v1 or full custom root
            }
            string url = baseUrl.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : baseUrl + "/chat/completions";

            string json = BuildRequestJson(dataUrl);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Fail($"HTTP {(int)resp.StatusCode}: {Truncate(body, 400)}", sw);

            string text = ExtractAssistantText(body);
            text = StripThinking(text).Trim();
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Fail(ex.Message, sw);
        }
    }

    private string BuildRequestJson(string dataUrl)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", Settings.VisionModel);
            if (Settings.Temperature >= 0)
                writer.WriteNumber("temperature", Settings.Temperature);

            writer.WritePropertyName("messages");
            writer.WriteStartArray();

            if (!string.IsNullOrWhiteSpace(Settings.SystemPrompt))
            {
                writer.WriteStartObject();
                writer.WriteString("role", "system");
                writer.WriteString("content", Settings.SystemPrompt);
                writer.WriteEndObject();
            }

            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WritePropertyName("content");
            writer.WriteStartArray();

            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", Settings.UserPrompt ?? string.Empty);
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("type", "image_url");
            writer.WritePropertyName("image_url");
            writer.WriteStartObject();
            writer.WriteString("url", dataUrl);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string ExtractAssistantText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var msg = choices[0].GetProperty("message");
            if (msg.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                    return content.GetString() ?? string.Empty;
                // some providers return array content parts
                if (content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text))
                            sb.Append(text.GetString());
                        else if (part.ValueKind == JsonValueKind.String)
                            sb.Append(part.GetString());
                    }
                    return sb.ToString();
                }
            }
        }
        throw new InvalidOperationException("Response missing choices[0].message.content");
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

    private static string Truncate(string s, int n) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s[..n] + "…");

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}
