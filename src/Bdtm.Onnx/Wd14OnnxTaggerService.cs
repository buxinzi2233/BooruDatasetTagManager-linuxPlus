using System.Diagnostics;
using System.Globalization;
using Bdtm.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Bdtm.Onnx;

public sealed class Wd14ModelDefinition
{
    public string Repo { get; init; } = string.Empty;
    public double DefaultThreshold { get; init; }
    public double DefaultCharacterThreshold { get; init; } = 0.85;
    public string ShortName { get; init; } = string.Empty;
}

/// <summary>
/// Portable WD14 ONNX tagger using CUDA-first session factory and ImageSharp preprocessing.
/// </summary>
public sealed class Wd14OnnxTaggerService : IDisposable
{
    public const string ModelFileName = "model.onnx";
    public const string LabelsFileName = "selected_tags.csv";

    public static IReadOnlyList<Wd14ModelDefinition> Models { get; } = new[]
    {
        new Wd14ModelDefinition { Repo = "SmilingWolf/wd-eva02-large-tagger-v3", DefaultThreshold = 0.52, ShortName = "eva02-large v3" },
        new Wd14ModelDefinition { Repo = "SmilingWolf/wd-vit-tagger-v3", DefaultThreshold = 0.25, ShortName = "vit v3" },
        new Wd14ModelDefinition { Repo = "SmilingWolf/wd-swinv2-tagger-v3", DefaultThreshold = 0.25, ShortName = "swinv2 v3" },
        new Wd14ModelDefinition { Repo = "SmilingWolf/wd-convnext-tagger-v3", DefaultThreshold = 0.25, ShortName = "convnext v3" },
    };

    private static readonly string[] PreferredOutputNames = { "output", "predictions", "probs", "logits" };

    private readonly IOnnxSessionFactory _sessionFactory;
    private readonly string _modelsRoot;
    private InferenceSession? _session;
    private string? _loadedRepo;
    private string? _loadedModelPath;
    private string _inputName = "input";
    private string _outputName = "output";
    private List<(string Name, int Category)> _labels = new();

    public Wd14OnnxTaggerService(IOnnxSessionFactory sessionFactory, string modelsRoot)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _modelsRoot = modelsRoot ?? throw new ArgumentNullException(nameof(modelsRoot));
    }

    public string? LoadedRepo => _loadedRepo;
    public bool IsLoaded => _session is not null;
    public OnnxExecutionProvider ActiveProvider => _sessionFactory.ActiveProvider;
    public string? FallbackReason => _sessionFactory.FallbackReason;

    public static string GetLocalPath(string modelsRoot, string repo, string filename)
    {
        string safeRepo = repo.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(modelsRoot, safeRepo, filename);
    }

    public bool IsModelReady(string repo) =>
        File.Exists(GetLocalPath(_modelsRoot, repo, ModelFileName))
        && File.Exists(GetLocalPath(_modelsRoot, repo, LabelsFileName));

    public void LoadModel(string repo, bool forceCpu = false)
    {
        if (string.Equals(_loadedRepo, repo, StringComparison.OrdinalIgnoreCase) && _session is not null)
            return;

        Unload();
        string modelPath = GetLocalPath(_modelsRoot, repo, ModelFileName);
        string labelsPath = GetLocalPath(_modelsRoot, repo, LabelsFileName);
        if (!File.Exists(modelPath) || !File.Exists(labelsPath))
            throw new FileNotFoundException($"WD14 model files missing for repo '{repo}'. Expected under {_modelsRoot}.");

        _labels = LoadLabels(labelsPath);
        _loadedModelPath = modelPath;
        _session = _sessionFactory.Create(modelPath, forceCpu);
        (_inputName, _outputName) = ResolveSessionMetadata(_session);
        _loadedRepo = repo;
    }

    public OnnxTagResult TagImage(string imagePath, double generalThreshold, double characterThreshold)
    {
        if (_session is null)
            throw new InvalidOperationException("Model is not loaded.");
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image not found.", imagePath);

        var sw = Stopwatch.StartNew();
        int targetSize = ResolveInputSize(_session.InputMetadata[_inputName].Dimensions);
        DenseTensor<float> input = Wd14ImagePreprocessor.CreateInputTensor(imagePath, targetSize);
        float[] output = RunPrediction(_session, input);

        var items = new List<TagPrediction>();
        int count = Math.Min(_labels.Count, output.Length);
        for (int i = 0; i < count; i++)
        {
            (string name, int category) = _labels[i];
            double threshold = category switch
            {
                0 => generalThreshold,
                4 => characterThreshold,
                _ => double.PositiveInfinity
            };

            if (category is not (0 or 4))
                continue;

            if (output[i] >= threshold)
                items.Add(new TagPrediction { Tag = name, Confidence = output[i] });
        }

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
        _loadedRepo = null;
        _loadedModelPath = null;
        _labels.Clear();
    }

    public void Dispose() => Unload();

    public static (string InputName, string OutputName) ResolveSessionMetadata(InferenceSession session) =>
        ResolveSessionMetadata(session.InputMetadata.Keys, session.OutputMetadata.Keys);

    public static (string InputName, string OutputName) ResolveSessionMetadata(
        IEnumerable<string> inputNames,
        IEnumerable<string> outputNames)
    {
        string resolvedInput = inputNames.FirstOrDefault(name =>
                string.Equals(name, "input", StringComparison.OrdinalIgnoreCase))
            ?? inputNames.First();

        foreach (string preferred in PreferredOutputNames)
        {
            if (outputNames.Any(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase)))
                return (resolvedInput, preferred);
        }

        return (resolvedInput, outputNames.First());
    }

    public static int ResolveInputSize(IReadOnlyList<int> dims)
    {
        if (dims is null || dims.Count == 0)
            return 448;

        if (dims.Count >= 4 && dims[1] > 0 && dims[3] == 3)
            return dims[1];
        if (dims.Count >= 4 && dims[1] > 0 && dims[1] == dims[2])
            return dims[1];
        if (dims.Count >= 4 && dims[1] == 3 && dims[2] > 0)
            return dims[2];
        if (dims.Count >= 3 && dims[0] > 0 && dims[0] != 3)
            return dims[0];

        return 448;
    }

    private float[] RunPrediction(InferenceSession activeSession, DenseTensor<float> input)
    {
        try
        {
            return RunPredictionCore(activeSession, input);
        }
        catch (OnnxRuntimeException) when (_sessionFactory.ActiveProvider == OnnxExecutionProvider.Cuda && _loadedModelPath is not null)
        {
            // Retry once on CPU if CUDA session fails at run time.
            activeSession.Dispose();
            _session = _sessionFactory.Create(_loadedModelPath, forceCpu: true);
            (_inputName, _outputName) = ResolveSessionMetadata(_session);
            return RunPredictionCore(_session, input);
        }
    }

    private float[] RunPredictionCore(InferenceSession activeSession, DenseTensor<float> input)
    {
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = activeSession.Run(
            new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) },
            new[] { _outputName });
        return ExtractFloatVector(results.First());
    }

    public static float[] ExtractFloatVector(NamedOnnxValue result)
    {
        if (result.Value is DenseTensor<float> denseTensor)
            return denseTensor.ToArray();
        if (result.Value is Tensor<float> tensor)
            return tensor.ToArray();
        return result.AsEnumerable<float>().ToArray();
    }

    private static List<(string Name, int Category)> LoadLabels(string labelsPath)
    {
        var labels = new List<(string, int)>();
        using var reader = new StreamReader(labelsPath);
        string? header = reader.ReadLine();
        if (header is null)
            return labels;

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // selected_tags.csv: tag_id,name,category,count
            string[] parts = line.Split(',');
            if (parts.Length < 3)
                continue;

            string name = parts[1].Trim().Trim('"');
            if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int category))
                category = 0;
            labels.Add((name, category));
        }

        return labels;
    }
}
