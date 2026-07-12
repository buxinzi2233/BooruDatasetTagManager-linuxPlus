# Multi-Region Crop Image Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port WinForms multi-region manual crop (`Form_ImageCrop`) to Avalonia: drag regions, export `basename_rN.ext` beside source, import into dataset.

**Architecture:** Pure geometry + ImageSharp export in `Bdtm.Core`; Avalonia crop window for pointer interaction; `DatasetManager.AddImages` + MainViewModel refresh. No System.Drawing.

**Tech Stack:** .NET 8, xUnit, SixLabors.ImageSharp 3.1.12, Avalonia 11 + CommunityToolkit.Mvvm

**Spec:** `docs/porting/SPEC_2026-07-11-crop-image-design.md`  
**Repo:** `/home/buxinzi/Projects/BooruDatasetTagManager-linuxPlus`  
**Branch:** `feature/avalonia-linux-mvp`  
**Nature:** PORT from `Form_ImageCrop` / `CropCanvasHelper` / `ImageCropExporter`.

---

## Multi-agent

Controller → Implementer (one task) → Spec QA → Quality QA → next.  
No WinForms edits. Core tests after each Core task. Final `./scripts/build-linux.sh`.

---

## File map

| Path | Action |
|------|--------|
| `src/Bdtm.Core/CropGeometry.cs` | Create — CropRect, CropRegion, CropCanvasHelper |
| `src/Bdtm.Core/ImageCropExporter.cs` | Create |
| `src/Bdtm.Core/DatasetManager.cs` | Modify — AddImages |
| `tests/Bdtm.Core.Tests/CropGeometryTests.cs` | Create |
| `tests/Bdtm.Core.Tests/ImageCropExporterTests.cs` | Create |
| `tests/Bdtm.Core.Tests/DatasetManagerTests.cs` | Modify — AddImages tests |
| `src/Bdtm.Avalonia/Controls/CropCanvasControl.cs` | Create — pointer + render |
| `src/Bdtm.Avalonia/ViewModels/CropImageViewModel.cs` | Create |
| `src/Bdtm.Avalonia/Views/CropImageWindow.axaml(.cs)` | Create |
| `src/Bdtm.Avalonia/ViewModels/MainViewModel.cs` | CropCurrent + import refresh |
| `src/Bdtm.Avalonia/Views/MainWindow.axaml` | Menu item |
| `docs/porting/STATUS_*_post_crop.md` | Status |

---

### Task 1: Crop geometry + tests

**Files:** Create `src/Bdtm.Core/CropGeometry.cs`, `tests/Bdtm.Core.Tests/CropGeometryTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class CropGeometryTests
{
    [Fact]
    public void ClampToImage_ClipsOverflow()
    {
        var r = CropCanvasHelper.ClampToImage(new CropRect { X = -10, Y = -5, Width = 50, Height = 40 }, 100, 80);
        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
        Assert.True(r.Right <= 100);
        Assert.True(r.Bottom <= 80);
    }

    [Fact]
    public void NormalizeDragRectangle_OrdersCorners()
    {
        var r = CropCanvasHelper.NormalizeDragRectangle(30, 40, 10, 15);
        Assert.Equal(10, r.X);
        Assert.Equal(15, r.Y);
        Assert.Equal(20, r.Width);
        Assert.Equal(25, r.Height);
    }

    [Fact]
    public void CalcZoomMod_FitsInsideViewport()
    {
        float mod = CropCanvasHelper.CalcZoomMod(imageW: 200, imageH: 100, viewW: 100, viewH: 100);
        Assert.True(mod * 200 <= 100 + 0.01f);
        Assert.True(mod * 100 <= 100 + 0.01f);
    }

    [Fact]
    public void ScreenRectToImageRect_RoundTripApprox()
    {
        int iw = 200, ih = 100, vw = 400, vh = 200;
        var imgLoc = CropCanvasHelper.CalcImageLocation(iw, ih, vw, vh);
        // screen rect covering full image location
        var screen = imgLoc;
        var image = CropCanvasHelper.ScreenRectToImageRect(screen, iw, ih, vw, vh);
        Assert.InRange(image.Width, iw - 2, iw + 2);
        Assert.InRange(image.Height, ih - 2, ih + 2);
    }

    [Fact]
    public void HitTest_ReturnsTopmostContaining()
    {
        var regions = new List<CropRegion>
        {
            new() { Index = 1, Bounds = new CropRect { X = 0, Y = 0, Width = 50, Height = 50 } },
            new() { Index = 2, Bounds = new CropRect { X = 10, Y = 10, Width = 20, Height = 20 } },
        };
        // Use known zoom: image 100x100 view 100x100 → mod=1, location 0,0
        var hit = CropCanvasHelper.HitTest(regions, screenX: 15, screenY: 15, imageW: 100, imageH: 100, viewW: 100, viewH: 100);
        Assert.NotNull(hit);
        Assert.Equal(2, hit!.Index);
    }
}
```

API shape: prefer static methods taking ints for sizes (no Drawing Size). Match names from WinForms helper where possible.

- [ ] **Step 2: Implement CropGeometry.cs** port from CropCanvasHelper + CropRegion without System.Drawing

- [ ] **Step 3: Tests pass + commit**

```bash
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --filter CropGeometry --nologo
git add src/Bdtm.Core/CropGeometry.cs tests/Bdtm.Core.Tests/CropGeometryTests.cs
git commit -m "feat(core): crop geometry helpers without System.Drawing"
```

---

### Task 2: ImageCropExporter + tests

**Files:** `src/Bdtm.Core/ImageCropExporter.cs`, `tests/Bdtm.Core.Tests/ImageCropExporterTests.cs`

- [ ] **Step 1: Tests**

```csharp
[Fact]
public void GetOutputPath_Uses_rIndexSuffix()
{
    string p = ImageCropExporter.GetOutputPath(Path.Combine("a", "b", "img.png"), 2);
    Assert.Equal(Path.Combine("a", "b", "img_r2.png"), p);
}

[Fact]
public void ExportRegions_WritesCroppedFiles()
{
    string dir = Path.Combine(Path.GetTempPath(), "bdtm-crop-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        string src = Path.Combine(dir, "src.png");
        using (var img = new Image<Rgba32>(40, 30, new Rgba32(10, 20, 30)))
            img.SaveAsPng(src);

        var regions = new[]
        {
            new CropRegion { Index = 1, Bounds = new CropRect { X = 5, Y = 5, Width = 10, Height = 8 } },
        };
        var paths = ImageCropExporter.ExportRegions(src, regions);
        Assert.Single(paths);
        Assert.True(File.Exists(paths[0]));
        using var outImg = Image.Load(paths[0]);
        Assert.Equal(10, outImg.Width);
        Assert.Equal(8, outImg.Height);
    }
    finally { Directory.Delete(dir, true); }
}

[Fact]
public void ExportRegions_SkipsTinyAfterClamp()
{
    // region outside / tiny → empty or skipped
}
```

- [ ] **Step 2: Implement with ImageSharp** (already on Core via Caption)

```csharp
public static IReadOnlyList<string> ExportRegions(string imagePath, IReadOnlyList<CropRegion> regions)
{
    using var image = Image.Load(imagePath);
    var list = new List<string>();
    foreach (var region in regions)
    {
        var bounds = CropCanvasHelper.ClampToImage(region.Bounds, image.Width, image.Height);
        if (bounds.Width < 2 || bounds.Height < 2) continue;
        string outPath = GetOutputPath(imagePath, region.Index);
        using var crop = image.Clone(ctx => ctx.Crop(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height)));
        // SixLabors.ImageSharp.Processing.Crop — use Rectangle from ImageSharp
        crop.Save(outPath); // or SaveAsPng depending on ext
        list.Add(outPath);
    }
    return list;
}
```

Use `SixLabors.ImageSharp.Rectangle` only inside exporter, not in public CropRect API.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(core): ImageCropExporter multi-region export"
```

---

### Task 3: DatasetManager.AddImages

**Files:** `src/Bdtm.Core/DatasetManager.cs`, `tests/Bdtm.Core.Tests/DatasetManagerTests.cs`

- [ ] **Step 1: Test**

```csharp
[Fact]
public void AddImages_AddsNewPathsAndSkipsDuplicates()
{
    // Load folder with one image, AddImages second file path, count++, AddImages again same → no double
}
```

- [ ] **Step 2: Implement**

```csharp
public IReadOnlyList<string> AddImages(IEnumerable<string> paths)
{
    if (paths is null) return Array.Empty<string>();
    var added = new List<string>();
    foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
        if (!MediaExtensions.IsSupportedMedia(path)) continue;
        string full = Path.GetFullPath(path);
        if (_dataSet.ContainsKey(full)) continue;
        var item = DataItem.Create(full, Options);
        if (_dataSet.TryAdd(item.ImageFilePath, item))
            added.Add(item.ImageFilePath);
    }
    if (added.Count > 0) UpdateDatasetHash();
    return added;
}
```

Note: `DataItem.Create` keys by `imagePath` as given — use consistent full path as ImageFilePath in Create or normalize in AddImages before Create.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(core): DatasetManager.AddImages for crop import"
```

---

### Task 4: Avalonia CropCanvasControl + CropImageWindow

**Files:**
- `src/Bdtm.Avalonia/Controls/CropCanvasControl.cs` (or Views/)
- `src/Bdtm.Avalonia/ViewModels/CropImageViewModel.cs`
- `src/Bdtm.Avalonia/Views/CropImageWindow.axaml` + `.cs`

**ViewModel:**

```csharp
public partial class CropImageViewModel : ViewModelBase
{
    public string ImagePath { get; }
    public ObservableCollection<CropRegion> Regions { get; } = new();
    [ObservableProperty] private CropRegion? selectedRegion;
    public IReadOnlyList<string> ExportedPaths { get; private set; } = Array.Empty<string>();
    public const int MinimumCropSize = 8;

    public void AddRegionFromImageRect(CropRect rect) { /* clamp, min size, index, color, add */ }
    public void DeleteSelected() { /* renumber */ }
    public bool TryExport()
    {
        if (Regions.Count == 0) return false;
        ExportedPaths = ImageCropExporter.ExportRegions(ImagePath, Regions.ToList());
        return ExportedPaths.Count > 0;
    }
}
```

**Canvas control:**

- Properties: `SourceBitmap` (Avalonia Bitmap), bindable regions, selected
- On render: draw image fitted via helper; draw region overlays
- Pointer: hit-test select OR drag new rect
- Raise events/callbacks to VM for AddRegion

**Window:**

- Toolbar buttons bound to Export (Close true if ok), Delete, Cancel (Close false)
- KeyBinding Delete/Escape
- Right list: ItemsControl of regions

**Build:**

```bash
dotnet build src/Bdtm.Avalonia/Bdtm.Avalonia.csproj -c Release --nologo
```

**Commit:**

```bash
git commit -m "feat(ui): multi-region crop canvas window"
```

---

### Task 5: MainViewModel menu + import

**Files:** MainViewModel.cs, MainWindow.axaml

```csharp
[RelayCommand]
private async Task CropCurrentImageAsync()
{
    if (IsBusy) return;
    if (SelectedImage is null) { StatusText = "请先选择一张图片。"; return; }
    var window = GetMainWindow();
    if (window is null) return;
    string path = SelectedImage.Data.ImageFilePath;
    if (!File.Exists(path)) { StatusText = "图片不存在。"; return; }

    var vm = new CropImageViewModel(path);
    var dlg = new Views.CropImageWindow { DataContext = vm };
    var ok = await dlg.ShowDialog<bool?>(window);
    if (ok != true || vm.ExportedPaths.Count == 0) { StatusText = "已取消裁剪。"; return; }

    var added = _dataset.AddImages(vm.ExportedPaths);
    bool showPaths = ShowPaths;
    foreach (string p in added)
    {
        if (_dataset.DataSet.TryGetValue(p, out var data))
            Images.Add(new ImageListItem(data) { ShowFullPath = showPaths });
    }
    HasNoImages = Images.Count == 0;
    StatusText = $"已导入裁剪图 {added.Count} 张 · 导出 {vm.ExportedPaths.Count} 张";
    // optional: LoadThumbnails for new items only
    _ = LoadThumbnailsAsync(); // if exists and is safe
}
```

Menu under 工具:

```xml
<MenuItem Header="裁剪当前图…" Command="{Binding CropCurrentImageCommand}" />
```

Also under 打标 optional.

**Commit:**

```bash
git commit -m "feat(ui): wire crop window and dataset import"
```

---

### Task 6: Verify + docs

```bash
./scripts/build-linux.sh
./scripts/publish-linux.sh
```

Write `docs/porting/STATUS_2026-07-11_post_crop.md`, update ROADMAP (crop item), commit + push.

---

## Spec coverage

| Spec | Task |
|------|------|
| Geometry helpers | 1 |
| Export _rN | 2 |
| AddImages | 3 |
| Multi-region UI | 4 |
| Menu + import | 5 |
| Tests + docs | 1–3, 6 |
| No tag copy / no moondream | intentional |

---

## Execution

User wants continued multi-agent work: **Subagent-Driven** after plan commit.
