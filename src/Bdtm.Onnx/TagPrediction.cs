namespace Bdtm.Onnx;

public sealed class TagPrediction
{
    public string Tag { get; init; } = string.Empty;
    public float Confidence { get; init; }
}

public sealed class OnnxTagResult
{
    public IReadOnlyList<TagPrediction> Tags { get; init; } = Array.Empty<TagPrediction>();
    public double ElapsedMilliseconds { get; init; }
    public OnnxExecutionProvider Provider { get; init; }
}
