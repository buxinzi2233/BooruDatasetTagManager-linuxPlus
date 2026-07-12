# Windows Coupling Inventory

Generated: 2026-07-11T08:16:56+08:00
Repo: BooruDatasetTagManager-linuxPlus
Branch: feature/avalonia-linux-mvp

## Summary counts

| Pattern | Count |
|---------|-------|
| Form_*.cs | 44 |
| *.Designer.cs | 20 |
| System.Windows.Forms | 65 |
| System.Drawing | 67 |
| DirectML / OnnxRuntime | 2 |
| DllImport | 5 |
| BindingSource | 2 |
| .exe hardcode | 3 |

## TargetFramework / WinForms project
```
3:    <TargetFrameworks>net8.0-windows</TargetFrameworks>
4:    <OutputType>WinExe</OutputType>
27:    <UseWindowsForms>true</UseWindowsForms>
70:    <PackageReference Include="MetadataExtractor" Version="2.9.0" />
71:    <PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" Version="1.20.1" />
72:    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
73:    <PackageReference Include="OpenAI" Version="2.7.0" />
74:    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
75:    <PackageReference Include="TabControl" Version="2.1.2" />
```

## DllImport / native
```
BooruDatasetTagManager/FileNamesComparer.cs:12:        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
BooruDatasetTagManager/Form1.cs:2312:        [DllImport("shell32.dll", ExactSpelling = true)]
BooruDatasetTagManager/Form1.cs:2315:        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
BooruDatasetTagManager/Form1.cs:2318:        [DllImport("shell32.dll", ExactSpelling = true)]
BooruDatasetTagManager/OpenFolderDialog.cs:219:        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
BooruDatasetTagManager/Program.cs:123:            var r = AddDllDirectory(dllDirectory);
BooruDatasetTagManager/Program.cs:124:            Trace.WriteLine($"AddDllDirectory {dllDirectory} {r}");
BooruDatasetTagManager/Program.cs:135:        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
BooruDatasetTagManager/Program.cs:136:        static extern int AddDllDirectory(string NewDirectory);
BooruDatasetTagManager/Program.cs:143:            var loaded = NativeLibrary.TryLoad(libraryName,
BooruDatasetTagManager/Program.cs:145:                DllImportSearchPath.SafeDirectories |
BooruDatasetTagManager/Program.cs:146:                DllImportSearchPath.UserDirectories,
BooruDatasetTagManager/WebPWrapper.cs:910:        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
BooruDatasetTagManager/WebPWrapper.cs:932:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigInitInternal")]
BooruDatasetTagManager/WebPWrapper.cs:934:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigInitInternal")]
BooruDatasetTagManager/WebPWrapper.cs:954:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetFeaturesInternal")]
BooruDatasetTagManager/WebPWrapper.cs:956:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetFeaturesInternal")]
BooruDatasetTagManager/WebPWrapper.cs:975:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigLosslessPreset")]
BooruDatasetTagManager/WebPWrapper.cs:977:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigLosslessPreset")]
BooruDatasetTagManager/WebPWrapper.cs:995:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPValidateConfig")]
BooruDatasetTagManager/WebPWrapper.cs:997:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPValidateConfig")]
BooruDatasetTagManager/WebPWrapper.cs:1015:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureInitInternal")]
BooruDatasetTagManager/WebPWrapper.cs:1017:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureInitInternal")]
BooruDatasetTagManager/WebPWrapper.cs:1037:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1039:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1059:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1061:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1081:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRX")]
BooruDatasetTagManager/WebPWrapper.cs:1083:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRX")]
BooruDatasetTagManager/WebPWrapper.cs:1111:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncode")]
BooruDatasetTagManager/WebPWrapper.cs:1113:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncode")]
BooruDatasetTagManager/WebPWrapper.cs:1134:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureFree")]
BooruDatasetTagManager/WebPWrapper.cs:1136:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureFree")]
BooruDatasetTagManager/WebPWrapper.cs:1157:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
BooruDatasetTagManager/WebPWrapper.cs:1159:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
BooruDatasetTagManager/WebPWrapper.cs:1184:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRInto")]
BooruDatasetTagManager/WebPWrapper.cs:1186:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRInto")]
BooruDatasetTagManager/WebPWrapper.cs:1211:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
BooruDatasetTagManager/WebPWrapper.cs:1213:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
BooruDatasetTagManager/WebPWrapper.cs:1238:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeARGBInto")]
BooruDatasetTagManager/WebPWrapper.cs:1240:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeARGBInto")]
BooruDatasetTagManager/WebPWrapper.cs:1258:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPInitDecoderConfigInternal")]
BooruDatasetTagManager/WebPWrapper.cs:1260:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPInitDecoderConfigInternal")]
BooruDatasetTagManager/WebPWrapper.cs:1280:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecode")]
BooruDatasetTagManager/WebPWrapper.cs:1282:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecode")]
BooruDatasetTagManager/WebPWrapper.cs:1301:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFreeDecBuffer")]
BooruDatasetTagManager/WebPWrapper.cs:1303:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFreeDecBuffer")]
BooruDatasetTagManager/WebPWrapper.cs:1326:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1328:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1351:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1353:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1375:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1377:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGR")]
BooruDatasetTagManager/WebPWrapper.cs:1399:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1401:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGRA")]
BooruDatasetTagManager/WebPWrapper.cs:1420:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
BooruDatasetTagManager/WebPWrapper.cs:1422:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
BooruDatasetTagManager/WebPWrapper.cs:1439:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetDecoderVersion")]
BooruDatasetTagManager/WebPWrapper.cs:1441:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetDecoderVersion")]
BooruDatasetTagManager/WebPWrapper.cs:1462:        [DllImport(_sPathDLL86, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureDistortion")]
BooruDatasetTagManager/WebPWrapper.cs:1464:        [DllImport(_sPathDLL64, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureDistortion")]
```

## ONNX / DirectML hits
```
BooruDatasetTagManager/Form1.cs:46:        private ToolStripMenuItem menuOnnxTagger;
BooruDatasetTagManager/Form1.cs:48:        private ToolStripMenuItem menuContextDSRetagOnnx;
BooruDatasetTagManager/Form1.cs:196:            menuOnnxTagger = new ToolStripMenuItem { Name = "menuOnnxTagger", Text = "ONNX tagger" };
BooruDatasetTagManager/Form1.cs:197:            menuOnnxTagger.Click += (_, _) => ShowOnnxTaggerForSelectedImages(autoRun: false);
BooruDatasetTagManager/Form1.cs:213:            menuContextDSRetagOnnx = new ToolStripMenuItem { Name = "menuContextDSRetagOnnx", Text = "Retag ONNX" };
BooruDatasetTagManager/Form1.cs:214:            menuContextDSRetagOnnx.Click += (_, _) => ShowOnnxTaggerForSelectedImages(autoRun: true);
BooruDatasetTagManager/Form1.cs:219:            contextMenuStrip1.Items.Add(menuContextDSRetagOnnx);
BooruDatasetTagManager/Form1.cs:226:            toolsToolStripMenuItem.DropDownItems.Add(menuOnnxTagger);
BooruDatasetTagManager/Form1.cs:513:        private void ShowOnnxTaggerForSelectedImages(bool autoRun)
BooruDatasetTagManager/Form1.cs:527:            using Form_OnnxTagger form = new Form_OnnxTagger(this, autoRun);
BooruDatasetTagManager/Form1.cs:2281:            if (menuContextDSRetagOnnx != null)
BooruDatasetTagManager/Form1.cs:2282:                menuContextDSRetagOnnx.Visible = !isVideo;
BooruDatasetTagManager/Form1.cs:2520:            if (menuOnnxTagger != null)
BooruDatasetTagManager/Form1.cs:2521:                menuOnnxTagger.Text = I18n.GetText("MenuOnnxTagger");
BooruDatasetTagManager/Form1.cs:2535:            if (menuContextDSRetagOnnx != null)
BooruDatasetTagManager/Form1.cs:2536:                menuContextDSRetagOnnx.Text = I18n.GetText("MenuContextDSRetagOnnx");
BooruDatasetTagManager/OnnxTaggerCatalog.cs:7:    public enum OnnxTaggerModelKind
BooruDatasetTagManager/OnnxTaggerCatalog.cs:9:        Wd14,
BooruDatasetTagManager/OnnxTaggerCatalog.cs:10:        PixAi
BooruDatasetTagManager/OnnxTaggerCatalog.cs:13:    public sealed class OnnxTaggerModelEntry
BooruDatasetTagManager/OnnxTaggerCatalog.cs:16:        public OnnxTaggerModelKind Kind { get; init; }
BooruDatasetTagManager/OnnxTaggerCatalog.cs:28:    public static class OnnxTaggerCatalog
BooruDatasetTagManager/OnnxTaggerCatalog.cs:30:        public const string PixAiModelId = "pixai:v0.9";
BooruDatasetTagManager/OnnxTaggerCatalog.cs:32:        public static IReadOnlyList<OnnxTaggerModelEntry> AllModels { get; }
BooruDatasetTagManager/OnnxTaggerCatalog.cs:34:        static OnnxTaggerCatalog()
BooruDatasetTagManager/OnnxTaggerCatalog.cs:36:            var models = new List<OnnxTaggerModelEntry>();
BooruDatasetTagManager/OnnxTaggerCatalog.cs:37:            foreach (Wd14ModelDefinition model in Wd14OnnxTaggerService.Models)
BooruDatasetTagManager/OnnxTaggerCatalog.cs:39:                models.Add(new OnnxTaggerModelEntry
BooruDatasetTagManager/OnnxTaggerCatalog.cs:42:                    Kind = OnnxTaggerModelKind.Wd14,
BooruDatasetTagManager/OnnxTaggerCatalog.cs:50:            models.Add(new OnnxTaggerModelEntry
BooruDatasetTagManager/OnnxTaggerCatalog.cs:52:                Id = PixAiModelId,
BooruDatasetTagManager/OnnxTaggerCatalog.cs:53:                Kind = OnnxTaggerModelKind.PixAi,
BooruDatasetTagManager/OnnxTaggerCatalog.cs:55:                Repo = PixAiOnnxTaggerService.ModelRepo,
BooruDatasetTagManager/OnnxTaggerCatalog.cs:63:        public static OnnxTaggerModelEntry GetById(string id)
BooruDatasetTagManager/OnnxTaggerCatalog.cs:67:                OnnxTaggerModelEntry match = AllModels.FirstOrDefault(model =>
BooruDatasetTagManager/OnnxTaggerCatalog.cs:85:                ? Wd14OnnxTaggerService.Models[^1].Repo
BooruDatasetTagManager/HuggingFaceModelDownloader.cs:13:        private const long MinOnnxFileBytes = 1024 * 1024;
BooruDatasetTagManager/HuggingFaceModelDownloader.cs:39:        public static string NormalizePathForOnnx(string path)
BooruDatasetTagManager/HuggingFaceModelDownloader.cs:69:                    return info.Length >= MinOnnxFileBytes;
BooruDatasetTagManager/TagPostProcessor.cs:71:                Wd14TaggerSettings wd => wd.ReplaceUnderscoresWithSpaces,
BooruDatasetTagManager/TagPostProcessor.cs:72:                PixAiTaggerSettings pix => pix.ReplaceUnderscoresWithSpaces,
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:12:using Microsoft.ML.OnnxRuntime;
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:13:using Microsoft.ML.OnnxRuntime.Tensors;
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:18:    public sealed class PixAiOnnxTaggerService : IDisposable
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:33:        private InferenceSession session;
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:81:            labels = PixAiSelectedTagsCsvLoader.Load(HuggingFaceModelDownloader.GetLocalPath(ModelRepo, "selected_tags.csv"));
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:88:        public OnnxTagResult TagImageWithTiming(string imagePath, double generalThreshold, double characterThreshold)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:99:            return new OnnxTagResult
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:116:            DenseTensor<float> input = PixAiOnnxImagePreprocessor.CreateInputTensor(image, inputWidth, inputHeight);
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:139:        internal static (string InputName, string OutputName, bool RequiresSigmoid) ResolveSessionMetadata(InferenceSession session)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:164:        internal static float[] ExtractFloatVector(NamedOnnxValue result)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:183:        private void ConfigureSessionMetadata(InferenceSession loadedSession)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:188:        private float[] RunPrediction(InferenceSession activeSession, DenseTensor<float> input)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:194:            catch (OnnxRuntimeException ex) when (usesDirectMlProvider)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:210:        private float[] RunPredictionCore(InferenceSession activeSession, DenseTensor<float> input)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:212:            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = activeSession.Run(
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:213:                new[] { NamedOnnxValue.CreateFromTensor(inputName, input) },
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:239:        private static InferenceSession CreateSession(string modelPath, bool forceCpu, out bool usesDirectMl)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:264:            return new InferenceSession(HuggingFaceModelDownloader.NormalizePathForOnnx(modelPath), options);
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:267:        private InferenceSession CreateSession(string modelPath, bool forceCpu = false)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:318:    internal static class PixAiSelectedTagsCsvLoader
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:337:                if (TryParsePixAiRow(parts, out string name, out int category))
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:356:        private static bool TryParsePixAiRow(string[] parts, out string name, out int category)
BooruDatasetTagManager/PixAiOnnxTaggerService.cs:380:    internal static class PixAiOnnxImagePreprocessor
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:12:using Microsoft.ML.OnnxRuntime;
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:13:using Microsoft.ML.OnnxRuntime.Tensors;
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:17:    public sealed class Wd14ModelDefinition
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:25:    public sealed class Wd14OnnxTaggerService : IDisposable
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:30:        public static IReadOnlyList<Wd14ModelDefinition> Models { get; } = new[]
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:32:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-convnext-tagger", DefaultThreshold = 0.35, ShortName = "convnext v1" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:33:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-convnext-tagger-v2", DefaultThreshold = 0.35, ShortName = "convnext v2" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:34:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-convnextv2-tagger-v2", DefaultThreshold = 0.35, ShortName = "convnextv2 v2" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:35:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-swinv2-tagger-v2", DefaultThreshold = 0.35, ShortName = "swinv2 v2" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:36:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-vit-tagger", DefaultThreshold = 0.35, ShortName = "vit v1" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:37:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-vit-tagger-v2", DefaultThreshold = 0.35, ShortName = "vit v2" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:38:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-v1-4-moat-tagger-v2", DefaultThreshold = 0.35, ShortName = "moat v2" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:39:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-vit-tagger-v3", DefaultThreshold = 0.25, ShortName = "vit v3" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:40:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-swinv2-tagger-v3", DefaultThreshold = 0.25, ShortName = "swinv2 v3" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:41:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-convnext-tagger-v3", DefaultThreshold = 0.25, ShortName = "convnext v3" },
BooruDatasetTagManager/Wd14OnnxTaggerService.cs:42:            new Wd14ModelDefinition { Repo = "SmilingWolf/wd-vit-large-tagger-v3", DefaultThreshold = 0.26, ShortName = "vit-large v3" },
```

## FFmpeg / win-x64
```
BooruDatasetTagManager.Tests/VideoProcessingServiceTests.cs:57:        string bundled = Path.Combine(appDir, "ThirdParty", "ffmpeg", "win-x64", "ffmpeg.exe");
BooruDatasetTagManager.Tests/VideoProcessingServiceTests.cs:65:            Assert.EndsWith("ffmpeg.exe", locator.FfmpegExe, StringComparison.OrdinalIgnoreCase);
BooruDatasetTagManager/FfmpegLocator.cs:22:        public string FfmpegExe => ResolveExecutable("ffmpeg.exe");
BooruDatasetTagManager/FfmpegLocator.cs:24:        public string FfprobeExe => ResolveExecutable("ffprobe.exe");
BooruDatasetTagManager/FfmpegLocator.cs:46:            string bundled = Path.Combine(appDirectory, "ThirdParty", "ffmpeg", "win-x64", fileName);
BooruDatasetTagManager/Program.cs:122:                Environment.Is64BitProcess ? "win-x64" : "win-x86");
BooruDatasetTagManager/WebPWrapper.cs:907:        private const string _sPathDLL64 = "win-x64\\libwebp.dll";
BooruDatasetTagManager/WebPWrapper.cs:908:        private const string _sPathDLL86 = "win-x86\\libwebp.dll";
BooruDatasetTagManager/Extensions.cs:267:                    // ffmpeg unavailable or extraction failed
```

## Form files
```
BooruDatasetTagManager/Form1.cs
BooruDatasetTagManager/Form1.Designer.cs
BooruDatasetTagManager/Form_addTag.cs
BooruDatasetTagManager/Form_addTag.Designer.cs
BooruDatasetTagManager/Form_AiServerSet.cs
BooruDatasetTagManager/Form_AutoTaggerOpenAiSettings.cs
BooruDatasetTagManager/Form_AutoTaggerOpenAiSettings.Designer.cs
BooruDatasetTagManager/Form_AutoTaggerSettings.cs
BooruDatasetTagManager/Form_AutoTaggerSettings.Designer.cs
BooruDatasetTagManager/Form_backgroundReplace.cs
BooruDatasetTagManager/Form_backgroundReplace.Designer.cs
BooruDatasetTagManager/Form_BGRemover.cs
BooruDatasetTagManager/Form_BGRemover.Designer.cs
BooruDatasetTagManager/Form_CharacterTagAuditWizard.cs
BooruDatasetTagManager/Form_CropImage.cs
BooruDatasetTagManager/Form_CropImage.Designer.cs
BooruDatasetTagManager/Form_Edit.cs
BooruDatasetTagManager/Form_Edit.Designer.cs
BooruDatasetTagManager/Form_filter.cs
BooruDatasetTagManager/Form_filter.Designer.cs
BooruDatasetTagManager/Form_ImageCrop.cs
BooruDatasetTagManager/Form_ImageSorter.cs
BooruDatasetTagManager/Form_ImageSorter.Designer.cs
BooruDatasetTagManager/Form_ImageSorterSettings.cs
BooruDatasetTagManager/Form_ImageSorterSettings.Designer.cs
BooruDatasetTagManager/Form_LlmT2NlConfirm.cs
BooruDatasetTagManager/Form_LlmT2NlProgress.cs
BooruDatasetTagManager/Form_LoadingSettings.cs
BooruDatasetTagManager/Form_LoadingSettings.Designer.cs
BooruDatasetTagManager/Form_manualCrop.cs
BooruDatasetTagManager/Form_manualCrop.Designer.cs
BooruDatasetTagManager/Form_OnnxTagger.cs
BooruDatasetTagManager/Form_preview.cs
BooruDatasetTagManager/Form_preview.Designer.cs
BooruDatasetTagManager/Form_replaceAll.cs
BooruDatasetTagManager/Form_replaceAll.Designer.cs
BooruDatasetTagManager/Form_settings.cs
BooruDatasetTagManager/Form_settings.Designer.cs
BooruDatasetTagManager/Form_TagImagesGrid.cs
BooruDatasetTagManager/Form_TagImagesGrid.Designer.cs
BooruDatasetTagManager/Form_TagWikiPopup.cs
BooruDatasetTagManager/Form_TestModule.cs
BooruDatasetTagManager/Form_UpdateInfo.cs
BooruDatasetTagManager/Form_UpdateInfo.Designer.cs
BooruDatasetTagManager/Form_VideoConvert.cs
BooruDatasetTagManager/Form_VideoTools.cs
```

## Likely domain candidates (non-Form .cs at root)
```
BooruDatasetTagManager/AbstractTranslator.cs
BooruDatasetTagManager/Adler32.cs
BooruDatasetTagManager/AiPromptTemplateCatalog.cs
BooruDatasetTagManager/AiPromptTemplateEditorPanel.cs
BooruDatasetTagManager/AiServerSetSettingsMigration.cs
BooruDatasetTagManager/AiServerSetSettingsService.cs
BooruDatasetTagManager/AllTagsItem.cs
BooruDatasetTagManager/AllTagsList.cs
BooruDatasetTagManager/AppSettings.cs
BooruDatasetTagManager/AutoCompleteTextBox.cs
BooruDatasetTagManager/AutoTagProviderAdapters.cs
BooruDatasetTagManager/AutoTagProvider.cs
BooruDatasetTagManager/BufferedControls.cs
BooruDatasetTagManager/CaptionGenerationService.cs
BooruDatasetTagManager/CharacterTagAudit.cs
BooruDatasetTagManager/CharacterTagFileTransaction.cs
BooruDatasetTagManager/CharacterTagReasonLocalizer.cs
BooruDatasetTagManager/ChineseTagLookupService.cs
BooruDatasetTagManager/ChineseTranslator.cs
BooruDatasetTagManager/ColorScheme.cs
BooruDatasetTagManager/ColouredCheckedListBox.cs
BooruDatasetTagManager/CropCanvasHelper.cs
BooruDatasetTagManager/CropRegion.cs
BooruDatasetTagManager/CustomPictureBoxWithYN.cs
BooruDatasetTagManager/CustomTextBoxColumn.cs
BooruDatasetTagManager/DanbooruDTextFormatter.cs
BooruDatasetTagManager/DanbooruWikiClient.cs
BooruDatasetTagManager/DatasetManager.cs
BooruDatasetTagManager/EditableTag.cs
BooruDatasetTagManager/EditableTagHistory.cs
BooruDatasetTagManager/EditableTagList.cs
BooruDatasetTagManager/Extensions.cs
BooruDatasetTagManager/FallbackTranslator.cs
BooruDatasetTagManager/FfmpegLocator.cs
BooruDatasetTagManager/FileNamesComparer.cs
BooruDatasetTagManager/GithubClasses.cs
BooruDatasetTagManager/GlobalEnums.cs
BooruDatasetTagManager/GoogleJsonTranslator.cs
BooruDatasetTagManager/GoogleTranslator.cs
BooruDatasetTagManager/HotkeyData.cs
BooruDatasetTagManager/HuggingFaceModelDownloader.cs
BooruDatasetTagManager/I18n.cs
BooruDatasetTagManager/ImageCropExporter.cs
BooruDatasetTagManager/ImageLoader.cs
BooruDatasetTagManager/ImageSorter.cs
BooruDatasetTagManager/LanguageManager.cs
BooruDatasetTagManager/MoondreamRect.cs
BooruDatasetTagManager/MultiSelectDataTable.cs
BooruDatasetTagManager/MyMemoryTranslator.cs
BooruDatasetTagManager/OnnxTaggerCatalog.cs
BooruDatasetTagManager/OnnxTaggerProgressTracker.cs
BooruDatasetTagManager/OpenAiConnectionErrorClassifier.cs
BooruDatasetTagManager/OpenAiSpeedTestService.cs
BooruDatasetTagManager/OpenFolderDialog.cs
BooruDatasetTagManager/PixAiOnnxTaggerService.cs
BooruDatasetTagManager/PromptParser.cs
BooruDatasetTagManager/QuickTagReplaceService.cs
BooruDatasetTagManager/RectangleOperations.cs
BooruDatasetTagManager/TagPostProcessor.cs
BooruDatasetTagManager/TagsDB.cs
BooruDatasetTagManager/TagValue.cs
BooruDatasetTagManager/TagWriteService.cs
BooruDatasetTagManager/ToolStripTrackBarItem.cs
BooruDatasetTagManager/TranslationManager.cs
BooruDatasetTagManager/VideoProcessingService.cs
BooruDatasetTagManager/VideoProgressReporter.cs
BooruDatasetTagManager/Wd14OnnxTaggerService.cs
BooruDatasetTagManager/WebPWrapper.cs
```

## Notes for port

- Primary UI: WinForms on net8.0-windows
- Replace DirectML with CUDA/CPU OnnxRuntime on Linux
- Extract DatasetManager/AppSettings/FfmpegLocator into Bdtm.Core
- New UI: Avalonia 11 in src/Bdtm.Avalonia
