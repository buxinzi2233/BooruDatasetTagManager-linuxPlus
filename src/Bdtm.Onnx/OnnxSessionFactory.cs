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

    public string? FallbackReason { get; private set; }

    public InferenceSession Create(string modelPath, bool forceCpu = false)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is required.", nameof(modelPath));
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("ONNX model not found.", modelPath);

        string path = Path.GetFullPath(modelPath);
        FallbackReason = null;

        if (!forceCpu)
        {
            try
            {
                using var cudaOptions = CreateBaseOptions();
                cudaOptions.AppendExecutionProvider_CUDA(0);
                var session = new InferenceSession(path, cudaOptions);
                ActiveProvider = OnnxExecutionProvider.Cuda;
                FallbackReason = null;
                _log?.Invoke("ONNX execution provider: CUDA (device 0) for " + path);
                return session;
            }
            catch (Exception ex)
            {
                FallbackReason = SummarizeCudaFailure(ex);
                _log?.Invoke("CUDA EP unavailable, falling back to CPU: " + FallbackReason);
            }
        }
        else
        {
            FallbackReason = "forceCpu=true";
        }

        using var cpuOptions = CreateBaseOptions();
        cpuOptions.AppendExecutionProvider_CPU();
        var cpuSession = new InferenceSession(path, cpuOptions);
        ActiveProvider = OnnxExecutionProvider.Cpu;
        if (FallbackReason is null)
            _log?.Invoke("ONNX execution provider: CPU for " + path);
        else
            _log?.Invoke("ONNX execution provider: CPU for " + path + " (reason: " + FallbackReason + ")");
        return cpuSession;
    }

    private static string SummarizeCudaFailure(Exception ex)
    {
        string msg = ex.Message ?? ex.GetType().Name;
        string[] libs =
        {
            "libcublasLt.so.12",
            "libcublas.so.12",
            "libcudart.so.12",
            "libcudnn.so.9",
            "libonnxruntime_providers_cuda.so",
        };
        foreach (string lib in libs)
        {
            if (msg.Contains(lib, StringComparison.Ordinal))
                return "missing " + lib + " (set LD_LIBRARY_PATH to CUDA 12 libs, or use scripts/run-linux.sh)";
        }

        msg = msg.Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (msg.Length > 180)
            msg = msg.Substring(0, 180) + "...";
        return msg;
    }

    private static SessionOptions CreateBaseOptions() =>
        new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
}
