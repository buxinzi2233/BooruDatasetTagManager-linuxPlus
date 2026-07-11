using System.Diagnostics;
using Bdtm.Onnx;
using Microsoft.ML.OnnxRuntime;

static void Banner(string t) => Console.WriteLine($"\n=== {t} ===");

Banner("OrtEnv available providers");
try
{
    string[] providers = OrtEnv.Instance().GetAvailableProviders();
    Console.WriteLine(providers.Length == 0 ? "(none)" : string.Join(", ", providers));
}
catch (Exception ex)
{
    Console.WriteLine("GetAvailableProviders failed: " + ex);
}

string modelsRoot = args.ElementAtOrDefault(0)
    ?? Path.Combine(AppContext.BaseDirectory, "Models");
// Also try publish layout / common symlink location
string[] roots =
{
    modelsRoot,
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dist", "linux-x64", "Models")),
    Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Projects/BooruDatasetTagManager-linuxPlus/dist/linux-x64/Models")),
    Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger")),
};

string repo = args.ElementAtOrDefault(1) ?? "SmilingWolf/wd-eva02-large-tagger-v3";
string? imagePath = args.ElementAtOrDefault(2);

Banner("Resolve model");
string? chosenRoot = null;
string? modelPath = null;
foreach (string root in roots.Distinct())
{
    string p = Wd14OnnxTaggerService.GetLocalPath(root, repo, "model.onnx");
    string labels = Wd14OnnxTaggerService.GetLocalPath(root, repo, "selected_tags.csv");
    Console.WriteLine($"try: {p} exists={File.Exists(p)} labels={File.Exists(labels)}");
    if (File.Exists(p) && File.Exists(labels))
    {
        chosenRoot = root;
        modelPath = p;
        break;
    }

    // flat layout: root/wd-eva02-large-tagger-v3/
    string flat = Path.Combine(root, repo.Split('/').Last(), "model.onnx");
    string flatLabels = Path.Combine(root, repo.Split('/').Last(), "selected_tags.csv");
    Console.WriteLine($"try flat: {flat} exists={File.Exists(flat)}");
    if (File.Exists(flat) && File.Exists(flatLabels))
    {
        // Build a temp org/repo tree via symlink for the service
        string tmp = Path.Combine(Path.GetTempPath(), "bdtm-onnx-probe-models");
        string dest = Path.Combine(tmp, "SmilingWolf", "wd-eva02-large-tagger-v3");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (Directory.Exists(dest) || File.Exists(dest) || File.Exists(dest + ".tmp"))
        {
            try { if (Directory.Exists(dest)) Directory.Delete(dest); } catch { }
            try { if (File.Exists(dest)) File.Delete(dest); } catch { }
        }
        try
        {
            Directory.CreateSymbolicLink(dest, Path.GetDirectoryName(flat)!);
            chosenRoot = tmp;
            modelPath = Wd14OnnxTaggerService.GetLocalPath(tmp, repo, "model.onnx");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine("symlink failed: " + ex.Message);
        }
    }
}

if (chosenRoot is null || modelPath is null)
{
    Console.WriteLine("MODEL NOT FOUND");
    return 2;
}

Console.WriteLine($"modelsRoot={chosenRoot}");
Console.WriteLine($"modelPath={modelPath}");
Console.WriteLine($"modelSizeMB={new FileInfo(modelPath).Length / 1024.0 / 1024.0:F1}");

Banner("Manual CUDA session create");
try
{
    using var opt = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
    opt.AppendExecutionProvider_CUDA(0);
    using var s = new InferenceSession(modelPath, opt);
    Console.WriteLine("CUDA InferenceSession: SUCCESS");
}
catch (Exception ex)
{
    Console.WriteLine("CUDA InferenceSession: FAIL");
    Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
    if (ex.InnerException != null)
        Console.WriteLine("Inner: " + ex.InnerException.Message);
}

Banner("Manual CPU session create");
try
{
    using var opt = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
    opt.AppendExecutionProvider_CPU();
    using var s = new InferenceSession(modelPath, opt);
    Console.WriteLine("CPU InferenceSession: SUCCESS");
}
catch (Exception ex)
{
    Console.WriteLine("CPU InferenceSession: FAIL " + ex.Message);
}

Banner("OnnxSessionFactory path");
var logs = new List<string>();
var factory = new OnnxSessionFactory(msg => { logs.Add(msg); Console.WriteLine("[factory] " + msg); });
using (var session = factory.Create(modelPath))
{
    Console.WriteLine("ActiveProvider=" + factory.ActiveProvider);
}

Banner("Wd14 service load + optional tag");
factory = new OnnxSessionFactory(msg => Console.WriteLine("[factory2] " + msg));
using var tagger = new Wd14OnnxTaggerService(factory, chosenRoot);
var swLoad = Stopwatch.StartNew();
tagger.LoadModel(repo);
swLoad.Stop();
Console.WriteLine($"LoadModel ms={swLoad.ElapsedMilliseconds} provider={tagger.ActiveProvider} loaded={tagger.IsLoaded}");

if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
{
    // warmup
    var r0 = tagger.TagImage(imagePath, 0.52, 0.85);
    Console.WriteLine($"warmup tags={r0.Tags.Count} ms={r0.ElapsedMilliseconds:F1} provider={r0.Provider}");
    var sw = Stopwatch.StartNew();
    var r1 = tagger.TagImage(imagePath, 0.52, 0.85);
    sw.Stop();
    Console.WriteLine($"infer tags={r1.Tags.Count} reportedMs={r1.ElapsedMilliseconds:F1} wallMs={sw.ElapsedMilliseconds} provider={r1.Provider}");
    Console.WriteLine("top: " + string.Join(", ", r1.Tags.OrderByDescending(t => t.Confidence).Take(8).Select(t => $"{t.Tag}:{t.Confidence:F2}")));
}
else
{
    Console.WriteLine("No image arg; skip inference timing. Usage: OnnxEpProbe [modelsRoot] [repo] [imagePath]");
}

Banner("Done");
return factory.ActiveProvider == OnnxExecutionProvider.Cuda ? 0 : 1;
