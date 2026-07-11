# MVP Status

Branch: `feature/avalonia-linux-mvp`

## Done

- [x] Coupling inventory (`docs/porting/INVENTORY.md`)
- [x] `Bdtm.Core` skeleton + tests
- [x] Portable `AppSettings` + `FfmpegLocator`
- [x] Portable `DatasetManager` / `TagList` / `PromptParser`
- [x] `Bdtm.Onnx` CUDA-first session + WD14 service + tag write
- [x] Avalonia MVP three-pane UI (open / edit / save / ONNX current)
- [x] `scripts/build-linux.sh`, `scripts/publish-linux.sh`

## Not in MVP (backlog)

- [ ] LLM vision / TAG2NL
- [ ] Character tag audit wizard
- [ ] Video tools UI
- [ ] Crop / background removal
- [ ] PixAI ONNX
- [ ] Model download UI (HF)
- [ ] Thumbnail previews in list
- [ ] Full i18n parity with WinForms language files
- [ ] AppImage / AUR package

## Verify

```bash
./scripts/build-linux.sh
./scripts/publish-linux.sh
# run: dist/linux-x64/Bdtm.Avalonia
```
