namespace Bdtm.Core;

public sealed class AiApiBgBackend : IBgRemovalBackend
{
    private readonly string _endpoint;

    public AiApiBgBackend(string endpoint)
    {
        _endpoint = endpoint.TrimEnd('/') + "/";
    }

    public string Id => "aiapi";

    public async Task<IReadOnlyList<BgModel>> ListModelsAsync(CancellationToken ct = default)
    {
        using var client = new RmbgClient(_endpoint);
        var (ok, _) = await client.CheckConnectionAsync(ct);
        if (!ok) return Array.Empty<BgModel>();
        var models = await client.GetRmbgModelsAsync(ct);
        return models
            .Select(m => new BgModel
            {
                BackendId = "aiapi",
                ModelId = m,
                Display = m,
                Downloaded = true,
            })
            .ToList();
    }

    public Task EnsureModelAsync(BgModel model, IProgress<long>? progress = null, CancellationToken ct = default)
    {
        // AiApiServer handles model loading server-side; nothing to download here.
        return Task.CompletedTask;
    }

    public async Task<BgRunResult> RunAsync(BgModel model, string imagePath, BgOptions options, CancellationToken ct = default)
    {
        using var client = new RmbgClient(_endpoint);
        var result = await client.RemoveBackgroundAsync(imagePath, model.ModelId, ct);
        if (!result.Success || result.ImageData is null)
            return new BgRunResult { Success = false, SourcePath = imagePath, ErrorMessage = result.ErrorMessage ?? "unknown" };

        string outDir = Path.GetDirectoryName(imagePath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(imagePath);

        byte[] outputBytes;
        if (options.Background == BgBackgroundKind.SolidColor)
        {
            try
            {
                using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(result.ImageData);
                outputBytes = BackgroundCompositor.CompositeTransparentToSolid(result.ImageData, img.Width, img.Height, options.SolidColorArgb);
            }
            catch
            {
                outputBytes = result.ImageData;
            }
        }
        else
        {
            outputBytes = result.ImageData;
        }

        string outputPath;
        if (options.Output == BgOutputMode.OverwriteOriginal)
        {
            outputPath = imagePath;
            if (options.BackupOriginal && File.Exists(imagePath))
            {
                string backup = imagePath + ".bdtm-bak";
                File.Copy(imagePath, backup, overwrite: true);
            }
            string tmp = imagePath + ".bdtm-tmp-" + Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(tmp, outputBytes, ct);
            File.Move(tmp, imagePath, overwrite: true);
        }
        else
        {
            outputPath = Path.Combine(outDir, baseName + "_bgremoved.png");
            await File.WriteAllBytesAsync(outputPath, outputBytes, ct);
        }

        return new BgRunResult
        {
            Success = true,
            SourcePath = imagePath,
            OutputPath = outputPath,
        };
    }
}