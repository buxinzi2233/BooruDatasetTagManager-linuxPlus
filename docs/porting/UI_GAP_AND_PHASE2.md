# UI 差异与 Phase 2（P0 工作台对齐）

**日期：** 2026-07-11  
**分支：** `feature/avalonia-linux-mvp`  
**对照：** 原版 WinForms Plus（左图）vs Linux Avalonia MVP（右图）

## 差异摘要

| 区域 | 原版 | MVP（Phase1） | Phase2 P0 目标 |
|------|------|---------------|----------------|
| 左栏 | 缩略图+名称+路径 | 纯文件名 | 缩略图+名称（路径 tooltip） |
| 中栏 | 标签+中文+权重工具 | 标签+权重 | 标签+中文+权重 |
| 右栏 | 标签/中文/计数表+Tabs | `tag (n)` 文本 | 标签\|计数 表 |
| 预览 | Preview Tab | 无 | 选中图预览 |
| Wiki/菜单/批量 | 完整 | 无 | 仍 backlog |

## Phase 2 P0 范围（本阶段）

1. **P0.1** 左栏缩略图（异步 ImageSharp→Avalonia Bitmap）
2. **P0.2** 中文翻译列（`danbooru-0-zh.csv` → `ChineseTagLookup`）
3. **P0.3** 全局标签 DataGrid（Tag, Count），点击可高亮/参考
4. **P0.4** 选中图片预览区

## 非本阶段

批量 ONNX、Wiki、LLM、视频、完整菜单、拖拽排序。

## 协同方式

- Controller：进度与验收  
- Implementer：Core 查询 + Avalonia UI  
- QA：构建/测试/对照清单  
