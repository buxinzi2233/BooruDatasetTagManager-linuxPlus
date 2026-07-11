# MVP Status

Branch: `feature/avalonia-linux-mvp`  
Last progress write-up: `docs/porting/PROGRESS.md`（2026-07-11）

## Done

- [x] Coupling inventory (`docs/porting/INVENTORY.md`)
- [x] `Bdtm.Core` skeleton + tests
- [x] Portable `AppSettings` + `FfmpegLocator`
- [x] Portable `DatasetManager` / `TagList` / `PromptParser`
- [x] `Bdtm.Onnx` CUDA-first session + WD14 service + tag write
- [x] Avalonia MVP three-pane UI (open / edit / save / ONNX current)
- [x] `scripts/build-linux.sh`, `scripts/publish-linux.sh`, `scripts/run-linux.sh` (CUDA LD path)
- [x] ONNX EP probe tool `tools/OnnxEpProbe`
- [x] CUDA fallback reason in UI; ready-vs-session-loaded wording
- [x] User-verified: dataset edit/save + CUDA tagging works with `run-linux.sh`

## Not in MVP (backlog)

- [ ] LLM vision / TAG2NL
- [ ] Character tag audit wizard
- [ ] Video tools UI
- [ ] Crop / background removal
- [ ] PixAI ONNX
- [ ] Model download UI (HF)
- [ ] Thumbnail previews in list
- [ ] Batch ONNX for selection/all
- [ ] Full i18n parity with WinForms language files
- [ ] AppImage / AUR package
- [ ] Push branch / open PR

## Verify

```bash
./scripts/build-linux.sh          # 24 tests
./scripts/publish-linux.sh
./scripts/run-linux.sh            # preferred (CUDA libs)
```

Tests last green: **Core 19 + Onnx 5**.
