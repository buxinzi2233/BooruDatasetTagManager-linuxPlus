# Px.x 查找替换 + 标签图网格 · 设计规格

**日期：** 2026-07-12  
**状态：** 已批准  
**性质：** WinForms Plus → Avalonia/Linux 移植  
**波次：** B（接裁剪之后）

## 目标

1. **查找替换全部**：选源标签 → 改新标签 → 替换全部/过滤数据集  
2. **标签图网格**：对指定标签展示其关联图片的缩略图网格，并支持切换「含/不含」并保存

## 架构

```
Avalonia
  ReplaceAllWindow（源标签 Combo + 新标签 + 自动完成候选 + OK/Cancel）
  TagImagesWindow（缩略图 FlowLayout + 缩放滑块 + 图片含/不含状态 + Save/Cancel）
  MainViewModel.ReplaceTagCommand / ShowTagImagesCommand
  MainViewModel rebuild tags after replace
Core
  QuickTagReplaceService（移植：同尾词分类、低频率候选）
  DatasetManager.ReplaceTagInAll（已有）
  TagList / DataItem（已有）
```

## 非目标

- 完整 i18n（中文 UI）
- 数据集过滤生效（当前 always all）
- 拖拽排序

## 实现顺序

1. Core: `QuickTagReplaceService` + 单测
2. Avalonia: 替换对话框
3. Avalonia: 标签图网格窗口
4. MainViewModel 菜单 + 刷新
