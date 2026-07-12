# 状态 · 多区裁剪后

**手动多区裁剪** 已移植（对齐 `Form_ImageCrop` + `ImageCropExporter`）。

## 交付

| 区域 | 内容 |
|------|------|
| Core | `CropGeometry`、`ImageCropExporter`、`DatasetManager.AddImages` |
| UI | 画布多区拖拽/选中/删除 · 导出 `_rN` · 导入数据集 |
| 入口 | 打标 / 工具 → **裁剪当前图…** |
| 测试 | Core **138** + Onnx **9** |
| 说明 | 不复制源标签 txt；同名覆盖；无 moondream 自动框 |

## 使用

```bash
./scripts/publish-linux.sh
./scripts/run-linux.sh
# 选中图片 → 工具或打标 → 裁剪当前图… → 拖拽多区 → 导出并导入
```

## 整体

| 阶段 | 状态 |
|------|------|
| MVP～P5.3 | ✅ |
| 多区裁剪 | ✅ |
| 查找替换/标签网格 · RMBG · 审计画廊 UX | 待办（波次 B/C/D） |
| 整体 | **~85–87%** |
