using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class VideoProcessingServiceTests
{
    [Theory]
    [InlineData("30/1", 30)]
    [InlineData("30000/1001", 30000.0 / 1001.0)]
    [InlineData("24", 24)]
    [InlineData("0/0", 0)]
    public void ParseFpsString_Works(string text, double expected)
    {
        double actual = VideoProcessingService.ParseFpsString(text);
        if (expected == 0)
            Assert.Equal(0, actual);
        else
            Assert.InRange(actual, expected - 0.01, expected + 0.01);
    }

    [Fact]
    public void IsVideoFile_ChecksExtension()
    {
        Assert.True(VideoProcessingService.IsVideoFile("/tmp/a.mp4"));
        Assert.True(VideoProcessingService.IsVideoFile("/tmp/a.WebM"));
        Assert.False(VideoProcessingService.IsVideoFile("/tmp/a.png"));
        Assert.False(VideoProcessingService.IsVideoFile(""));
    }

    [Fact]
    public async Task GetVideoInfo_WhenFfmpegAvailable_DoesNotThrowOnMissingFile()
    {
        var locator = new FfmpegLocator(AppContext.BaseDirectory, string.Empty);
        if (!locator.IsAvailable)
            return; // skip when ffmpeg not on PATH in CI

        var svc = new VideoProcessingService(locator);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            svc.GetVideoInfoAsync(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid() + ".mp4")));
    }
}
