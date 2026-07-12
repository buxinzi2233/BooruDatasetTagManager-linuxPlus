# BDTM Linux Architecture (MVP)

## Projects

| Project | TFM | Role |
|---------|-----|------|
| `src/Bdtm.Core` | net8.0 | Dataset IO, tags, settings, FFmpeg locator |
| `src/Bdtm.Onnx` | net8.0 | ONNX session factory (CUDA→CPU), WD14 tagger, tag write |
| `src/Bdtm.Avalonia` | net8.0 | Avalonia 11 desktop UI |
| `BooruDatasetTagManager/` | net8.0-windows | Original WinForms (reference only on Linux) |

## Boundaries

```
Avalonia ViewModels  -->  Bdtm.Core (DatasetManager, AppSettings)
                     \->  Bdtm.Onnx (Wd14OnnxTaggerService, TagWriteService)
```

- No WinForms / System.Drawing in Core or Onnx.
- Image previews are UI-side via file path (ImageSharp used in Onnx preprocessor).
- Models expected at `<app>/Models/<org>/<repo>/{model.onnx,selected_tags.csv}`.

## ONNX providers

`OnnxSessionFactory` tries CUDA device 0, logs and falls back to CPU on failure.
Runtime package: `Microsoft.ML.OnnxRuntime.Gpu` 1.20.1.


## CUDA notes (Linux)

`Microsoft.ML.OnnxRuntime.Gpu` needs CUDA 12 user-mode libraries at runtime
(`libcublasLt.so.12`, `libcudart.so.12`, `libcudnn.so.9`, …). Drivers alone
(`libcuda.so`) are not enough.

If those libs are missing, session creation falls back to **CPU** and the UI
shows the fallback reason.

Prefer:

```bash
./scripts/run-linux.sh
```

which prepends known pip `nvidia/*/lib` trees (or `BDTM_CUDA_LIB_DIRS`) to
`LD_LIBRARY_PATH`. Verify with:

```bash
dotnet run --project tools/OnnxEpProbe -c Release -- dist/linux-x64/Models SmilingWolf/wd-eva02-large-tagger-v3 /path/to.jpg
```
