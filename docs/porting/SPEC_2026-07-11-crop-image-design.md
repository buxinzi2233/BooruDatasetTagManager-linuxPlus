# P6.x 多区裁剪 · 设计规格

**日期：** 2026-07-11  
**状态：** 已批准（brainstorming 第 1–3 块）  
**分支：** `feature/avalonia-linux-mvp`  
**性质：** WinForms Plus → Avalonia/Linux **移植**  
**权威源：**  
- `BooruDatasetTagManager/Form_ImageCrop.cs`（手动多区）  
- `BooruDatasetTagManager/ImageCropExporter.cs`  
- `BooruDatasetTagManager/CropCanvasHelper.cs`  
- `BooruDatasetTagManager/CropRegion.cs`  
- `Form1.cropImageToolStripMenuItem_Click`（导出后 `AddImages`）  

**非本波：** `Form_CropImage`（moondream/AiApi 自动检测裁剪）。

---

## 1. 目标与非目标

### 1.1 目标

对**当前选中图**：

1. 打开裁剪窗  
2. 拖拽添加多个矩形区（完成时图像坐标最小约 8×8）  
3. 点击选中、Delete 删除、自动重编号 `#1…n`  
4. 导出到**原图同目录** `basename_r{index}{ext}`  
5. 导出成功后**自动加入当前数据集**并刷新列表  

### 1.2 已锁定

| 项 | 选择 |
|----|------|
| 范围 | 多区裁剪 + 回写数据集（非单区 MVP） |
| 路径 | Core 几何/导出 + Avalonia 画布窗 |
| 标签 | **不**自动复制源 `.txt`（对齐原版 ExportRegions 只写图） |
| 覆盖 | 同名 `_rN` 直接覆盖（对齐原版） |

### 1.3 非目标

- Moondream / AiApi 物体检测自动裁  
- 批量「全部图」自动裁  
- 导出时复制标签侧车  
- 像素级 WinForms 皮肤 / 完整 i18n  

---

## 2. 架构

```
Avalonia CropImageWindow / CropImageViewModel
  → CropCanvasHelper（坐标、HitTest、Clamp）
  → ImageCropExporter（ImageSharp 裁切写盘）
  → DatasetManager.AddImages + 主窗刷新 Images
```

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `CropCanvasHelper` | 缩放/居中/屏幕↔图像矩形 | 读盘、UI |
| `ImageCropExporter` | 路径规则 + 裁切保存 | 数据集 |
| `DatasetManager.AddImages` | 把新路径加入内存库 | 裁切 |
| 裁剪窗 | 交互与预览 | 全局标签业务 |

---

## 3. Core API

### 3.1 几何（无 System.Drawing）

```csharp
public readonly struct CropRect
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public sealed class CropRegion
{
    public CropRect Bounds { get; set; }
    public int Index { get; set; }
    public uint DisplayColorArgb { get; set; }
}
```

`CropCanvasHelper` 移植原逻辑，类型改为 `CropRect` / 宽高 `int`：

- `CalcZoomMod`, `CalcImageLocation`  
- `ScreenRectToImageRect`, `ImageRectToScreenRect`  
- `NormalizeDragRectangle`, `ScreenPointToImagePoint`  
- `HitTest`, `ClampToImage`  
- `RegionColors` 常量 ARGB 数组  

### 3.2 导出

```csharp
public static class ImageCropExporter
{
    public static string GetOutputPath(string imagePath, int regionIndex);
    public static string GetOutputDirectory(string imagePath);
    public static IReadOnlyList<string> ExportRegions(
        string imagePath,
        IReadOnlyList<CropRegion> regions);
}
```

- 路径：`{dir}/{base}_r{Index}{ext}`；无扩展名则 `.png`  
- ImageSharp 加载源图；每区 `Clamp` 后宽或高 &lt; 2 跳过  
- 保存覆盖已存在文件  
- 返回成功写出的完整路径列表  

### 3.3 DatasetManager.AddImages

```csharp
public IReadOnlyList<string> AddImages(IEnumerable<string> paths)
```

- 跳过：空、不存在、非支持媒体、已在 `DataSet`  
- `DataItem.Create(path, Options)` + `TryAdd`  
- 有新增则 `UpdateDatasetHash()`  
- 返回新加入的 `ImageFilePath`  

无侧车标签的新图 → 空 `TagList`（与 Create 行为一致）。

---

## 4. UI

### 4.1 入口

- **工具 → 裁剪当前图…**（可选：打标菜单双入口）  
- 需已选中图片；`IsBusy` 时拒绝  

### 4.2 窗口

| 项 | 值 |
|----|-----|
| 标题 | 裁剪图像 |
| 尺寸 | ~1000×700，可最大化 |
| 工具栏 | 导出并导入 · 删除选区 · 取消 |
| 快捷键 | Delete · Esc |
| 布局 | 画布 + 右侧区列表 |

### 4.3 交互

- 等比适应视口 + 居中  
- 拖拽新增区；点击选中；Delete 删除并重编号  
- 侧栏 `#n · w×h`（缩略图可选增强）  
- 导出：无区则提示；成功 `Close(true)` 带回路径；主窗 `AddImages` + 刷新  

### 4.4 错误

文件损坏、IO 失败、未选图：中文提示，不静默失败。

---

## 5. 数据流

```
选中图 → 打开 CropImageWindow(imagePath)
  → 用户添加/删除区
  → ExportRegions → disk _rN
  → Close(true, paths)
  → DatasetManager.AddImages(paths)
  → UI 插入 ImageListItem / 刷新缩略图
  → Status：已导入裁剪图 n 张
```

---

## 6. 测试

| 用例 | 断言 |
|------|------|
| GetOutputPath | `img.png` + 2 → `img_r2.png` |
| ClampToImage | 越界收入图内 |
| NormalizeDrag / Screen↔Image | 基本正确（int 误差可接受） |
| ExportRegions | 临时图导出文件存在、尺寸正确 |
| AddImages | 新增成功；重复跳过 |

门槛：`dotnet test` Core 绿；`build-linux` 绿。

---

## 7. 验收

- [ ] 菜单可开裁剪窗  
- [ ] 多区拖拽、选中、删除、重编号  
- [ ] 导出同目录 `_rN`，源图不变  
- [ ] 自动进入数据集列表  
- [ ] Core 单测覆盖路径/导出/AddImages  
- [ ] 状态文档更新  

---

## 8. 实现顺序（供 writing-plans）

1. Core：`CropRect` / `CropRegion` / `CropCanvasHelper` + 几何单测  
2. Core：`ImageCropExporter` + 导出单测  
3. Core：`DatasetManager.AddImages` + 单测  
4. Avalonia：画布控件 + 窗口/VM  
5. MainViewModel 菜单与导入刷新  
6. build-linux + publish 冒烟 + 状态文档  

---

## 9. 后续波次（本 spec 不实现）

| 波 | 内容 |
|----|------|
| B | 查找替换全部 + 标签图网格 |
| C | RMBG 抠图最小 UI + AiApiServer 文档 |
| D | 审计缩略图画廊 + TAG2NL/审计 UX 小补 |

协作：Controller / Implementer / QA（`PHASE4_P2.md`）。
