using Microsoft.ML.OnnxRuntime;

namespace Bdtm.Onnx;

public interface IOnnxSessionFactory
{
    /// <summary>Last successfully used provider (after Create).</summary>
    OnnxExecutionProvider ActiveProvider { get; }

    /// <summary>
    /// Create a session preferring CUDA when available, falling back to CPU.
    /// </summary>
    InferenceSession Create(string modelPath, bool forceCpu = false);
}
