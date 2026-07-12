namespace Bdtm.Core;

public enum BgMode { AllImages, SelectedOnly }
public enum BgOutputMode { OverwriteOriginal, SaveAsCopy }
public enum BgBackgroundKind { Transparent, SolidColor }

public sealed class BgOptions
{
    public BgMode Mode { get; init; } = BgMode.SelectedOnly;
    public BgOutputMode Output { get; init; } = BgOutputMode.SaveAsCopy;
    public BgBackgroundKind Background { get; init; } = BgBackgroundKind.Transparent;
    public string SolidColorArgb { get; init; } = "#FFFFFF";
    public bool BackupOriginal { get; init; } = true;
}

public sealed class BgModel
{
    public string BackendId { get; init; } = "";
    public string ModelId { get; init; } = "";
    public string Display { get; init; } = "";
    public long SizeBytes { get; init; }
    public bool Downloaded { get; init; }
    public string? Description { get; init; }
}

public sealed class BgRunResult
{
    public bool Success { get; init; }
    public string? SourcePath { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class BgProgress
{
    public int Completed { get; init; }
    public int Total { get; init; }
    public string CurrentFile { get; init; } = "";
}

public sealed class BgBatchSummary
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<BgRunResult> Results { get; init; } = Array.Empty<BgRunResult>();
}

public interface IBgRemovalBackend
{
    string Id { get; }
    Task<IReadOnlyList<BgModel>> ListModelsAsync(CancellationToken ct = default);
    Task EnsureModelAsync(BgModel model, IProgress<long>? progress = null, CancellationToken ct = default);
    Task<BgRunResult> RunAsync(BgModel model, string imagePath, BgOptions options, CancellationToken ct = default);
}