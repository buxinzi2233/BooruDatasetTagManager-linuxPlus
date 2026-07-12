using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bdtm.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Bdtm.Onnx;

public sealed class PixAiOnnxTaggerService : IDisposable
{
    public const string ModelRepo = "deepghs/pixai-tagger-v0.9-onnx";

    public static readonly IReadOnlyList<string> RequiredFiles = new[]
    {
        "model.onnx",
        "selected_tags.csv",
        "categories.json",
        "preprocess.json",
        "thresholds.csv",
    };

    private static readonly string[] PreferredOutputNames = { "prediction", "logits" };

    private readonly IOnnxSessionFactory _sessionFactory;
    private readonly string _modelsRoot;
    private InferenceSession? _session;
    private List<(string Name, int Category)> _labels = new();
    private string _inputName = "input";
    private string _outputName = "prediction";
    private bool _outputRequiresSigmoid;
    private int _inputWidth = 448;
    private int _inputHeight = 448;

    public PixAiOnnxTaggerService(IOnnxSessionFactory sessionFactory, string modelsRoot)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _modelsRoot = modelsRoot ?? throw new ArgumentNullException(nameof(modelsRoot));
    }

    public bool IsLoaded => _session is not null;
    public OnnxExecutionProvider ActiveProvider => _sessionFactory.ActiveProvider;
    public string? FallbackReason => _sessionFactory.FallbackReason;
    public string Repo => ModelRepo;

    public static string GetLocalPath(string modelsRoot, string filename) =>
        Path.Combine(modelsRoot, ModelRepo.Replace('/', Path.DirectorySeparatorChar), filename);

    public bool IsModelReady()
    {
        return RequiredFiles.All(f => File.Exists(GetLocalPath(_modelsRoot, f)));
    }

    public void LoadModel(bool forceCpu = false)
    {
        if (_session is not null)
            return;

        foreach (string file in RequiredFiles)
        {
            string path = GetLocalPath(_modelsRoot, file);
            if (!File.Exists(path))
                throw new FileNotFoundException($"PixAI model file missing: {file}", path);
        }

        _labels = PixAiSelectedTagsCsvLoader.Load(GetLocalPath(_modelsRoot, "selected_tags.csv"));
        LoadPreprocess(GetLocalPath(_modelsRoot, "preprocess.json"));
        string modelPath = GetLocalPath(_modelsRoot, "model.onnx");
        _session = _sessionFactory.Create(modelPath, forceCpu);
        (_inputName, _outputName, _outputRequiresSigmoid) = ResolveSessionMetadata(_session);
    }

    public OnnxTagResult TagImage(string imagePath, double generalThreshold, double characterThreshold)
    {
        if (_session is null)
            throw new InvalidOperationException("PixAI model is not loaded.");
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image not found.", imagePath);

        var sw = Stopwatch.StartNew();
        DenseTensor<float> input = PixAiImagePreprocessor.CreateInputTensor(imagePath, _inputWidth, _inputHeight);
        float[] output = RunPrediction(input);
        var items = BuildTagItems(output, generalThreshold, characterThreshold);
        sw.Stop();
        return new OnnxTagResult
        {
            Tags = items,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            Provider = _sessionFactory.ActiveProvider,
        };
    }

    public void Unload()
    {
        _session?.Dispose();
        _session = null;
        _labels.Clear();
    }

    public void Dispose() => Unload();

    public static (string InputName, string OutputName, bool RequiresSigmoid) ResolveSessionMetadata(
        InferenceSession session) =>
        ResolveSessionMetadata(session.InputMetadata.Keys, session.OutputMetadata.Keys);

    public static (string InputName, string OutputName, bool RequiresSigmoid) ResolveSessionMetadata(
        IEnumerable<string> inputNames,
        IEnumerable<string> outputNames)
    {
        string resolvedInput = inputNames.FirstOrDefault(n =>
                string.Equals(n, "input", StringComparison.OrdinalIgnoreCase))
            ?? inputNames.First();

        foreach (string preferred in PreferredOutputNames)
        {
            if (!outputNames.Any(n => string.Equals(n, preferred, StringComparison.OrdinalIgnoreCase)))
                continue;
            bool sigmoid = string.Equals(preferred, "logits", StringComparison.OrdinalIgnoreCase);
            return (resolvedInput, preferred, sigmoid);
        }

        throw new InvalidOperationException(
            "PixAI ONNX model missing prediction/logits output. Available: " + string.Join(", ", outputNames));
    }

    public static float[] ApplySigmoid(IReadOnlyList<float> values)
    {
        var output = new float[values.Count];
        for (int i = 0; i < values.Count; i++)
            output[i] = 1f / (1f + MathF.Exp(-values[i]));
        return output;
    }

    private float[] RunPrediction(DenseTensor<float> input)
    {
        try
        {
            return RunPredictionCore(_session!, input);
        }
        catch (OnnxRuntimeException) when (_sessionFactory.ActiveProvider == OnnxExecutionProvider.Cuda)
        {
            _session?.Dispose();
            string modelPath = GetLocalPath(_modelsRoot, "model.onnx");
            _session = _sessionFactory.Create(modelPath, forceCpu: true);
            (_inputName, _outputName, _outputRequiresSigmoid) = ResolveSessionMetadata(_session);
            return RunPredictionCore(_session, input);
        }
    }

    private float[] RunPredictionCore(InferenceSession session, DenseTensor<float> input)
    {
        using var results = session.Run(
            new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) },
            new[] { _outputName });
        float[] output = ExtractFloatVector(results.First());
        if (_outputRequiresSigmoid)
            output = ApplySigmoid(output);
        return output;
    }

    private static float[] ExtractFloatVector(NamedOnnxValue result)
    {
        if (result.Value is DenseTensor<float> dense)
            return dense.ToArray();
        if (result.Value is Tensor<float> tensor)
            return tensor.ToArray();
        return result.AsEnumerable<float>().ToArray();
    }

    private List<TagPrediction> BuildTagItems(float[] output, double generalThreshold, double characterThreshold)
    {
        var items = new List<TagPrediction>();
        int count = Math.Min(_labels.Count, output.Length);
        for (int i = 0; i < count; i++)
        {
            (string name, int category) = _labels[i];
            if (category is not (0 or 4))
                continue;
            double threshold = category == 4 ? characterThreshold : generalThreshold;
            if (output[i] >= threshold)
                items.Add(new TagPrediction { Tag = name, Confidence = output[i] });
        }
        return items;
    }

    private void LoadPreprocess(string preprocessPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(preprocessPath));
            if (!doc.RootElement.TryGetProperty("stages", out var stages) || stages.ValueKind != JsonValueKind.Array)
                return;
            foreach (var stage in stages.EnumerateArray())
            {
                if (!stage.TryGetProperty("type", out var typeEl))
                    continue;
                if (!string.Equals(typeEl.GetString(), "resize", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!stage.TryGetProperty("size", out var size) || size.ValueKind != JsonValueKind.Array || size.GetArrayLength() < 2)
                    continue;
                _inputWidth = size[0].GetInt32();
                _inputHeight = size[1].GetInt32();
                break;
            }
        }
        catch
        {
            // keep defaults 448
        }
    }
}

public static class PixAiSelectedTagsCsvLoader
{
    public static List<(string Name, int Category)> Load(string labelsPath) =>
        ParseLines(File.ReadAllLines(labelsPath));

    public static List<(string Name, int Category)> ParseLines(IEnumerable<string> lines)
    {
        var result = new List<(string, int)>();
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string[] parts = line.Split(',');
            if (parts.Length < 2 || IsHeader(parts))
                continue;
            if (TryParsePixAiRow(parts, out string name, out int category))
            {
                result.Add((name, category));
                continue;
            }
            if (TryParseLegacyRow(parts, out name, out category))
                result.Add((name, category));
        }
        return result;
    }

    private static bool IsHeader(string[] parts) =>
        parts[0].Equals("id", StringComparison.OrdinalIgnoreCase)
        || parts[0].Equals("name", StringComparison.OrdinalIgnoreCase);

    private static bool TryParsePixAiRow(string[] parts, out string name, out int category)
    {
        name = string.Empty;
        category = 0;
        if (parts.Length < 4)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return false;
        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out category))
            return false;
        name = parts[2];
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool TryParseLegacyRow(string[] parts, out string name, out int category)
    {
        name = parts[0];
        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out category)
            && !string.IsNullOrWhiteSpace(name);
    }
}
