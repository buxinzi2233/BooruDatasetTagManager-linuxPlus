namespace Bdtm.Core;

public static class BgRemovalRegistry
{
    private static readonly List<IBgRemovalBackend> _backends = new();
    public static IReadOnlyList<IBgRemovalBackend> Backends => _backends;

    public static void Register(IBgRemovalBackend backend)
    {
        if (backend is null) return;
        if (_backends.Any(b => b.Id == backend.Id)) return;
        _backends.Add(backend);
    }

    public static IBgRemovalBackend? Find(string backendId)
    {
        return _backends.FirstOrDefault(b => b.Id == backendId);
    }

    public static async Task<IReadOnlyList<BgModel>> ListAllAsync(CancellationToken ct = default)
    {
        var all = new List<BgModel>();
        foreach (var backend in _backends)
        {
            try
            {
                var models = await backend.ListModelsAsync(ct);
                all.AddRange(models);
            }
            catch { /* skip failed backends */ }
        }
        return all;
    }
}