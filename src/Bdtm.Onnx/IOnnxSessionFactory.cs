using Microsoft.ML.OnnxRuntime;

namespace Bdtm.Onnx;

public interface IOnnxSessionFactory
{
    OnnxExecutionProvider ActiveProvider { get; }
    string? FallbackReason { get; }
    InferenceSession Create(string modelPath, bool forceCpu = false);
}
