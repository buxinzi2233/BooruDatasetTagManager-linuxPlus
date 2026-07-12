using Bdtm.Onnx;
using Xunit;

namespace Bdtm.Onnx.Tests;

public class PixAiMetadataTests
{
    [Fact]
    public void ResolveSessionMetadata_PrefersPrediction()
    {
        var (input, output, sigmoid) = PixAiOnnxTaggerService.ResolveSessionMetadata(
            new[] { "input" },
            new[] { "prediction", "other" });
        Assert.Equal("input", input);
        Assert.Equal("prediction", output);
        Assert.False(sigmoid);
    }

    [Fact]
    public void ResolveSessionMetadata_LogitsNeedsSigmoid()
    {
        var (_, output, sigmoid) = PixAiOnnxTaggerService.ResolveSessionMetadata(
            new[] { "input" },
            new[] { "logits" });
        Assert.Equal("logits", output);
        Assert.True(sigmoid);
    }

    [Fact]
    public void ApplySigmoid_MapsZeroToHalf()
    {
        float[] s = PixAiOnnxTaggerService.ApplySigmoid(new[] { 0f, 10f, -10f });
        Assert.InRange(s[0], 0.49f, 0.51f);
        Assert.True(s[1] > 0.99f);
        Assert.True(s[2] < 0.01f);
    }

    [Fact]
    public void CsvLoader_ParsesPixAiRows()
    {
        var lines = new[]
        {
            "id,category_name,name,category",
            "1,general,1girl,0",
            "2,character,some_char,4",
            "3,rating,safe,9",
        };
        var tags = PixAiSelectedTagsCsvLoader.ParseLines(lines);
        Assert.Equal(3, tags.Count);
        Assert.Equal(("1girl", 0), tags[0]);
        Assert.Equal(("some_char", 4), tags[1]);
    }
}
