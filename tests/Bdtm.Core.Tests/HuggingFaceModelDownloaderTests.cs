using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class HuggingFaceModelDownloaderTests
{
    [Fact]
    public void BuildDownloadUrl_UsesMirrorOrOfficial()
    {
        string mirror = HuggingFaceModelDownloader.BuildDownloadUrl(
            HuggingFaceDownloadSource.HfMirror,
            "SmilingWolf/wd-eva02-large-tagger-v3",
            "model.onnx");
        Assert.StartsWith("https://hf-mirror.com/", mirror);
        Assert.Contains("SmilingWolf/wd-eva02-large-tagger-v3", mirror);

        string official = HuggingFaceModelDownloader.BuildDownloadUrl(
            HuggingFaceDownloadSource.HuggingFace,
            "SmilingWolf/wd-vit-tagger-v3",
            "selected_tags.csv");
        Assert.StartsWith("https://huggingface.co/", official);
    }

    [Fact]
    public void GetLocalPath_UnderModelsRoot()
    {
        var dl = new HuggingFaceModelDownloader("/tmp/bdtm-models");
        string path = dl.GetLocalPath("SmilingWolf/wd-eva02-large-tagger-v3", "model.onnx");
        Assert.Contains("SmilingWolf", path);
        Assert.EndsWith("model.onnx", path);
    }

    [Fact]
    public void ValidateCachedFile_RejectsMissing()
    {
        Assert.False(HuggingFaceModelDownloader.ValidateCachedFile("/no/such/file.onnx", "model.onnx"));
    }
}
