# MVP / Port Status

Branch: `feature/avalonia-linux-mvp`  
Remote: `origin/feature/avalonia-linux-mvp`  
Latest status: `docs/porting/STATUS_2026-07-11_post_p3.md`

## Done

- [x] Coupling inventory (`docs/porting/INVENTORY.md`)
- [x] `Bdtm.Core` skeleton + tests
- [x] Portable `AppSettings` + `FfmpegLocator`
- [x] Portable `DatasetManager` / `TagList` / `PromptParser`
- [x] `Bdtm.Onnx` CUDA-first session + WD14 service + tag write
- [x] Avalonia three-pane workbench (open / edit / save / preview / all-tags)
- [x] Thumbnails + Chinese column + global tag table
- [x] Tag reorder, filters, batch ONNX (current / multi / all + cancel)
- [x] UI polish: rails, headers, ShowPaths TwoWay (user-confirmed OK)
- [x] `scripts/build-linux.sh`, `publish-linux.sh`, `run-linux.sh`
- [x] ONNX EP probe (`tools/OnnxEpProbe`)
- [x] CUDA fallback reason in UI; ready vs session-loaded wording
- [x] User-verified: dataset edit/save + CUDA tagging with `run-linux.sh`
- [x] P2: XDG config/Models (`AppPaths`), settings window, Wiki popup
- [x] Branch pushed to GitHub
- [x] Phase 5 P3: video tools (FFmpeg extract/info)
- [x] Tools menu restored (视频/Wiki/设置)

## Backlog (not done)

- [ ] Open GitHub PR (link ready)
- [ ] LLM vision / TAG2NL
- [ ] Character tag audit wizard
- [x] Video tools UI (info + frame extract; convert API ready)
- [ ] Crop / background removal
- [ ] PixAI ONNX
- [ ] Model download UI (HF)
- [ ] Full i18n parity with WinForms language files
- [ ] AppImage / AUR package

## Verify

```bash
./scripts/build-linux.sh          # Core 32 + Onnx 5
./scripts/publish-linux.sh
./scripts/run-linux.sh            # preferred (CUDA libs + user Models)
```

Tests last green: **Core 32 + Onnx 5**.
