using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bdtm.Core;

/// <summary>
/// Configuration model returned by the AiApiServer /getconfig endpoint.
/// </summary>
public sealed class AiApiConfig
{
    public List<AiApiInterrogatorInfo> Interrogators { get; set; } = new();
}

/// <summary>
/// Describes a single model (interrogator, editor or translator) registered on the AiApiServer.
/// </summary>
public sealed class AiApiInterrogatorInfo
{
    public string ModelName { get; set; } = "";
    public string Type { get; set; } = "";
    // other fields ignored for now
}

/// <summary>
/// Result of a remove-background operation.
/// </summary>
public sealed class RmbgResult
{
    public bool Success { get; init; }
    public byte[]? ImageData { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Minimal HTTP client that talks to the AiApiServer REST API for background removal (rmbg2 models).
/// </summary>
public sealed class RmbgClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _endpoint; // base URL like http://127.0.0.1:7866/

    public RmbgClient(string endpoint)
    {
        _endpoint = endpoint.TrimEnd('/') + "/";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>
    /// Verify the server is reachable by calling GET getconfig.
    /// </summary>
    public async Task<(bool Ok, string? Error)> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(_endpoint + "getconfig", ct);
            response.EnsureSuccessStatusCode();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Retrieve available rmbg model names from the server (type == "rmbg2").
    /// </summary>
    public async Task<List<string>> GetRmbgModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync(_endpoint + "getconfig", ct);
            var dto = JsonSerializer.Deserialize<ConfigResponseDto>(json);
            // rmbg2 models are registered as Editors on the server
            return dto?.Editors
                .Select(e => e.ModelName)
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Send an image to the server for background removal using the specified model.
    /// Returns the processed image data (PNG RGBA) on success.
    /// </summary>
    public async Task<RmbgResult> RemoveBackgroundAsync(string imagePath, string modelName, CancellationToken ct = default)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var fileName = Path.GetFileName(imagePath);

            var request = new EditImageRequestDto
            {
                Image = imageBytes,
                FileName = fileName,
                SkipInternetRequests = false,
                SerializeVramUsage = false,
                Model = new ModelParametersDto
                {
                    ModelName = modelName,
                    AdditionalParameters = new List<ModelAdditionalParameterDto>()
                }
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(_endpoint + "editimage", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var editResponse = JsonSerializer.Deserialize<EditImageResponseDto>(responseJson);

            if (editResponse?.Success == true && editResponse.Image != null)
            {
                return new RmbgResult
                {
                    Success = true,
                    ImageData = editResponse.Image
                };
            }

            return new RmbgResult
            {
                Success = false,
                ErrorMessage = editResponse?.ErrorMessage ?? "Unknown error"
            };
        }
        catch (Exception ex)
        {
            return new RmbgResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public void Dispose() => _http.Dispose();

    // --- Private DTOs matching the AiApiServer JSON contract ---

    private sealed class ConfigResponseDto
    {
        public List<ModelBaseInfoDto> Interrogators { get; set; } = new();
        public List<ModelBaseInfoDto> Editors { get; set; } = new();
        public List<ModelBaseInfoDto> Translators { get; set; } = new();
    }

    private sealed class ModelBaseInfoDto
    {
        public string ModelName { get; set; } = "";
        public bool SupportedVideo { get; set; }
        public string? RepositoryLink { get; set; }
    }

    private sealed class EditImageRequestDto
    {
        [JsonPropertyName("Image")]
        public byte[] Image { get; set; } = Array.Empty<byte>();

        [JsonPropertyName("SkipInternetRequests")]
        public bool SkipInternetRequests { get; set; }

        [JsonPropertyName("SerializeVramUsage")]
        public bool SerializeVramUsage { get; set; }

        [JsonPropertyName("FileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("Model")]
        public ModelParametersDto Model { get; set; } = new();
    }

    private sealed class ModelParametersDto
    {
        [JsonPropertyName("ModelName")]
        public string ModelName { get; set; } = "";

        [JsonPropertyName("AdditionalParameters")]
        public List<ModelAdditionalParameterDto> AdditionalParameters { get; set; } = new();
    }

    private sealed class ModelAdditionalParameterDto
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string Type { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    private sealed class EditImageResponseDto
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("ErrorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("Image")]
        public byte[]? Image { get; set; }
    }
}
