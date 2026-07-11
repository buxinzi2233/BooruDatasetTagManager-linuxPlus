using Microsoft.ML.OnnxRuntime;

namespace Bdtm.Onnx;

/// <summary>
/// Creates ORT sessions with CUDA-first, CPU-fallback policy for Linux MVP.
/// </summary>
public sealed class OnnxSessionFactory : IOnnxSessionFactory
{
    private readonly Action<string>? _log;

    public OnnxSessionFactory(Action<string>? log = null)
    {
        _log = log;
    }

    public OnnxExecutionProvider ActiveProvider { get; private set; } = OnnxExecutionProvider.Cpu;

    public InferenceSession Create(string modelPath, bool forceCpu = false)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is required.", nameof(modelPath));
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("ONNX model not found.", modelPath);

        string path = Path.GetFullPath(modelPath);

        if (!forceCpu)
        {
            try
            {
                using var cudaOptions = CreateBaseOptions();
                cudaOptions.AppendExecutionProvider_CUDA(0);
                var session = new InferenceSession(path, cudaOptions);
                ActiveProvider = OnnxExecutionProvider.Cuda;
                _log?.Invoke($"ONNX execution provider: CUDA (device 0) for {path}");
                return session;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"CUDA EP unavailable, falling back to CPU: {ex.Message}");
            }
        }

        using var cpuOptions = CreateBaseOptions();
        cpuOptions.AppendExecutionProvider_CPU();
        var cpuSession = new InferenceSession(path, cpuOptions);
        ActiveProvider = OnnxExecutionProvider.Cpu;
        _log?.Invoke($"ONNX execution provider: CPU for {path}");
        return cpuSession;
    }

    private static SessionOptions CreateBaseOptions() =>
        new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
}
