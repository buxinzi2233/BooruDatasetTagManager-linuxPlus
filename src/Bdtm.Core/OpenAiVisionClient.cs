using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bdtm.Core;

public sealed class OpenAiVisionCompletionRequest
{
    public string SystemPrompt { get; init; } = "";
    public string UserPrompt { get; init; } = "";
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "image/jpeg";

    /// <summary>When set, overrides Settings.VisionModel for this call.</summary>
    public string? Model { get; init; }

    /// <summary>When false, omit image_url part (text-only chat).</summary>
    public bool IncludeImage { get; init; } = true;
}

public sealed class OpenAiVisionCompletionResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = "";
    public string? ErrorMessage { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? TotalTokens { get; init; }
}

/// <summary>
/// Shared OpenAI-compatible multimodal chat client (chat + image data URL).
/// Returns raw assistant text; callers handle ParseTags / caption formatting.
/// </summary>
public sealed class OpenAiVisionClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenAiVisionClient(LlmSettings settings, HttpClient? http = null)
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

    public async Task<OpenAiVisionCompletionResult> CompleteAsync(
        OpenAiVisionCompletionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (string.IsNullOrWhiteSpace(Settings.Endpoint))
                return Fail("LLM Endpoint 未配置");

            string model = !string.IsNullOrWhiteSpace(request.Model)
                ? request.Model.Trim()
                : (Settings.VisionModel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(model))
                return Fail("Vision 模型未配置");

            byte[] imageData = request.ImageData ?? Array.Empty<byte>();
            bool includeImage = request.IncludeImage && imageData.Length > 0;
            string mime = string.IsNullOrWhiteSpace(request.ContentType)
                ? "image/jpeg"
                : request.ContentType;
            string url = BuildChatCompletionsUrl(Settings.Endpoint);

            string json = BuildRequestJson(
                model,
                request.SystemPrompt,
                request.UserPrompt,
                includeImage ? imageData : null,
                mime);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Fail($"HTTP {(int)resp.StatusCode}: {Truncate(body, 400)}");

            string text = ExtractAssistantText(body);
            TryParseUsage(body, out int? inputTokens, out int? outputTokens, out int? totalTokens);
            return new OpenAiVisionCompletionResult
            {
                Success = true,
                Text = text,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = totalTokens,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    public static string BuildChatCompletionsUrl(string endpoint)
    {
        string baseUrl = (endpoint ?? string.Empty).TrimEnd('/');
        if (baseUrl.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return baseUrl;
        return baseUrl + "/chat/completions";
    }

    private string BuildRequestJson(
        string model,
        string? systemPrompt,
        string? userPrompt,
        byte[]? imageData,
        string mime)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            if (Settings.Temperature >= 0)
                writer.WriteNumber("temperature", Settings.Temperature);

            writer.WritePropertyName("messages");
            writer.WriteStartArray();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                writer.WriteStartObject();
                writer.WriteString("role", "system");
                writer.WriteString("content", systemPrompt);
                writer.WriteEndObject();
            }

            writer.WriteStartObject();
            writer.WriteString("role", "user");

            if (imageData is { Length: > 0 })
            {
                string b64 = Convert.ToBase64String(imageData);
                string dataUrl = $"data:{mime};base64,{b64}";

                writer.WritePropertyName("content");
                writer.WriteStartArray();

                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", userPrompt ?? string.Empty);
                writer.WriteEndObject();

                writer.WriteStartObject();
                writer.WriteString("type", "image_url");
                writer.WritePropertyName("image_url");
                writer.WriteStartObject();
                writer.WriteString("url", dataUrl);
                writer.WriteEndObject();
                writer.WriteEndObject();

                writer.WriteEndArray();
            }
            else
            {
                // Text-only: plain string content (audit TextScreening / Repair).
                writer.WriteString("content", userPrompt ?? string.Empty);
            }

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

    private static void TryParseUsage(
        string json,
        out int? inputTokens,
        out int? outputTokens,
        out int? totalTokens)
    {
        inputTokens = null;
        outputTokens = null;
        totalTokens = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("usage", out var usage)
                || usage.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            inputTokens = ReadTokenCount(usage, "prompt_tokens", "input_tokens");
            outputTokens = ReadTokenCount(usage, "completion_tokens", "output_tokens");
            totalTokens = ReadTokenCount(usage, "total_tokens");
        }
        catch (JsonException)
        {
            // usage is optional; ignore malformed usage blocks
        }
    }

    private static int? ReadTokenCount(JsonElement usage, params string[] names)
    {
        foreach (string name in names)
        {
            if (!usage.TryGetProperty(name, out var prop))
                continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int n))
                return n;
            if (prop.ValueKind == JsonValueKind.String
                && int.TryParse(prop.GetString(), out int parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static OpenAiVisionCompletionResult Fail(string err) =>
        new()
        {
            Success = false,
            ErrorMessage = err,
        };

    private static string Truncate(string s, int n) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s[..n] + "…");

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}
