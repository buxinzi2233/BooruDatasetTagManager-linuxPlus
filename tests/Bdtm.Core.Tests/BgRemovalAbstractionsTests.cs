using Bdtm.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bdtm.Core.Tests;

public sealed class BgRemovalAbstractionsTests
{
    [Fact]
    public void BgOptions_Defaults_AreSensible()
    {
        var opt = new BgOptions();
        Assert.Equal(BgMode.SelectedOnly, opt.Mode);
        Assert.Equal(BgOutputMode.SaveAsCopy, opt.Output);
        Assert.Equal(BgBackgroundKind.Transparent, opt.Background);
        Assert.True(opt.BackupOriginal);
    }

    [Fact]
    public void BgRemovalRegistry_DedupById()
    {
        // Use a unique id per run so the process-wide singleton does not bleed between tests.
        string id = "fake-dedup-" + Guid.NewGuid().ToString("N");
        var a = new FakeBackend(id);
        var b = new FakeBackend(id);

        BgRemovalRegistry.Register(a);
        BgRemovalRegistry.Register(b);

        Assert.NotNull(BgRemovalRegistry.Find(id));
        Assert.Same(a, BgRemovalRegistry.Find(id));
        // The duplicate (b) must not have replaced the first registration.
        Assert.NotSame(b, BgRemovalRegistry.Find(id));
    }

    [Fact]
    public void BackgroundCompositor_ParsesRrggbb()
    {
        var color = BackgroundCompositor.ParseColor("#FF0000");
        Assert.Equal(255, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void BackgroundCompositor_ParsesAarrggbb()
    {
        var color = BackgroundCompositor.ParseColor("#80FF0000");
        Assert.Equal(128, color.A);
        Assert.Equal(255, color.R);
    }

    [Fact]
    public void BackgroundCompositor_FallbackOnMalformed()
    {
        var color = BackgroundCompositor.ParseColor("nope");
        Assert.Equal((255, 255, 255, 255), color);
    }

    [Fact]
    public async Task BgRemovalService_AggregatesSuccessAndFailure()
    {
        var backend = new FakeBackend();
        var service = new BgRemovalService();
        var summary = await service.RunOnAsync(
            backend,
            new BgModel { ModelId = "test" },
            new BgOptions(),
            new[] { "ok.png", "fail.png" });
        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
    }

    [Fact]
    public async Task BgRemovalService_ReportsProgress()
    {
        var backend = new FakeBackend();
        var service = new BgRemovalService();
        var sink = new ProgressSink();
        var progress = (IProgress<BgProgress>)sink;

        await service.RunOnAsync(
            backend,
            new BgModel { ModelId = "test" },
            new BgOptions(),
            new[] { "ok.png", "ok.png", "fail.png" },
            progress);

        Assert.Equal(3, sink.Reports.Count);
        Assert.Equal(1, sink.Reports[0].Completed);
        Assert.Equal(3, sink.Reports[2].Total);
        Assert.Equal("fail.png", sink.Reports[2].CurrentFile);
    }

    [Fact]
    public void BackgroundCompositor_ReplacesTransparentPixelsWithSolid()
    {
        // Build a 2x2 image: top-left transparent, others opaque red.
        using var img = new Image<Rgba32>(2, 2);
        img[0, 0] = new Rgba32(0, 0, 0, 0);
        img[1, 0] = new Rgba32(255, 0, 0, 255);
        img[0, 1] = new Rgba32(255, 0, 0, 255);
        img[1, 1] = new Rgba32(255, 0, 0, 255);

        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        byte[] png = ms.ToArray();

        byte[] result = BackgroundCompositor.CompositeTransparentToSolid(png, 2, 2, "#00FF00");
        using var outImg = Image.Load<Rgba32>(result);

        // The transparent pixel should now be solid green at full alpha.
        Assert.Equal(new Rgba32(0, 255, 0, 255), outImg[0, 0]);
        // Opaque pixels are unchanged.
        Assert.Equal(new Rgba32(255, 0, 0, 255), outImg[1, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), outImg[0, 1]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), outImg[1, 1]);
    }

    private sealed class FakeBackend : IBgRemovalBackend
    {
        private readonly string _id;

        public FakeBackend(string id = "fake")
        {
            _id = id;
        }

        public string Id => _id;

        public Task<IReadOnlyList<BgModel>> ListModelsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BgModel>>(Array.Empty<BgModel>());

        public Task EnsureModelAsync(BgModel model, IProgress<long>? progress = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<BgRunResult> RunAsync(BgModel model, string imagePath, BgOptions options, CancellationToken ct = default)
        {
            if (imagePath.Contains("fail"))
            {
                return Task.FromResult(new BgRunResult
                {
                    Success = false,
                    SourcePath = imagePath,
                    ErrorMessage = "simulated failure",
                });
            }

            return Task.FromResult(new BgRunResult
            {
                Success = true,
                SourcePath = imagePath,
                OutputPath = imagePath + ".out",
            });
        }
    }

    /// <summary>
    /// Records progress reports synchronously, avoiding the threadpool indirection of
    /// <see cref="Progress{T}"/> that would make tests racy.
    /// </summary>
    private sealed class ProgressSink : IProgress<BgProgress>
    {
        public List<BgProgress> Reports { get; } = new();
        public void Report(BgProgress value) => Reports.Add(value);
    }
}