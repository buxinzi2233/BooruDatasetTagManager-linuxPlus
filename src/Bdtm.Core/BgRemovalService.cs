namespace Bdtm.Core;

public sealed class BgRemovalService
{
    public async Task<BgBatchSummary> RunOnAsync(
        IBgRemovalBackend backend,
        BgModel model,
        BgOptions options,
        IReadOnlyList<string> imagePaths,
        IProgress<BgProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<BgRunResult>();
        int succeeded = 0, failed = 0;
        int idx = 0;

        foreach (string path in imagePaths)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress?.Report(new BgProgress { Completed = idx, Total = imagePaths.Count, CurrentFile = Path.GetFileName(path) });

            try
            {
                var runResult = await backend.RunAsync(model, path, options, ct);
                if (runResult.Success)
                    succeeded++;
                else
                    failed++;
                results.Add(runResult);
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new BgRunResult { Success = false, SourcePath = path, ErrorMessage = ex.Message });
            }
        }

        return new BgBatchSummary { Succeeded = succeeded, Failed = failed, Results = results };
    }
}