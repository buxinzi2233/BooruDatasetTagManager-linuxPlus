namespace Bdtm.Core;

/// <summary>
/// Bridges <see cref="CharacterTagAuditService"/> model requests to <see cref="OpenAiVisionClient"/>.
/// Text-only stages omit images; visual review attaches the first existing image path.
/// </summary>
public static class CharacterTagOpenAiAdapter
{
    public static async Task<CharacterTagModelResponse> SendAsync(
        OpenAiVisionClient client,
        CharacterTagModelRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        byte[] image = Array.Empty<byte>();
        string mime = "image/jpeg";
        bool includeImage = false;

        if (request.ImagePaths.Count > 0)
        {
            string path = request.ImagePaths[0];
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                image = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                includeImage = image.Length > 0;
                mime = GuessMime(path);
            }
        }

        OpenAiVisionCompletionResult result = await client.CompleteAsync(
            new OpenAiVisionCompletionRequest
            {
                SystemPrompt = request.SystemPrompt,
                UserPrompt = request.UserPrompt,
                ImageData = image,
                ContentType = mime,
                Model = request.Model,
                IncludeImage = includeImage,
            },
            ct).ConfigureAwait(false);

        if (!result.Success)
            return new CharacterTagModelResponse(string.Empty, result.ErrorMessage ?? "error", null);

        CharacterTagTokenUsage? usage = result.TotalTokens is int total
            ? new CharacterTagTokenUsage(result.InputTokens ?? 0, result.OutputTokens ?? 0, total)
            : null;

        return new CharacterTagModelResponse(result.Text ?? string.Empty, string.Empty, usage);
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
}
