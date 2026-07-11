using Bdtm.Onnx;
using Xunit;

namespace Bdtm.Onnx.Tests;

public class Wd14MetadataTests
{
    [Fact]
    public void ResolveSessionMetadata_PrefersInputAndOutputNames()
    {
        var (input, output) = Wd14OnnxTaggerService.ResolveSessionMetadata(
            new[] { "foo", "input" },
            new[] { "bar", "predictions" });

        Assert.Equal("input", input);
        Assert.Equal("predictions", output);
    }

    [Fact]
    public void ResolveInputSize_HandlesNhwc()
    {
        Assert.Equal(448, Wd14OnnxTaggerService.ResolveInputSize(new[] { 1, 448, 448, 3 }));
        Assert.Equal(448, Wd14OnnxTaggerService.ResolveInputSize(Array.Empty<int>()));
    }

    [Fact]
    public void GetLocalPath_UsesRepoSegments()
    {
        string path = Wd14OnnxTaggerService.GetLocalPath("/models", "SmilingWolf/wd-vit-tagger-v3", "model.onnx");
        Assert.Contains("SmilingWolf", path);
        Assert.Contains("wd-vit-tagger-v3", path);
        Assert.EndsWith("model.onnx", path);
    }
}
