# Phase 5 / P3 — 视频工具（FFmpeg）

**日期：** 2026-07-11  
**分支：** `feature/avalonia-linux-mvp`  
**协作：** 保持 Controller / Implementer / QA 模式（见 PHASE4_P2.md）

## 范围（本阶段）

1. Core：`VideoProcessingService` 可移植版（ffprobe 信息 + 抽帧 + 可选转码）  
2. Avalonia：视频工具窗口（选视频、显示信息、按 FPS/全部抽帧、进度）  
3. 菜单「工具 → 视频工具…」；抽帧后可选刷新/提示导入数据集  

非本阶段：完整时间轴预览播放器、锁定帧 UI 1:1、GPU 编码预设。

## 依赖

系统 `ffmpeg` / `ffprobe`（本机已装 n8.x），或设置页 FFmpeg 路径。


## 状态

- [x] Core `VideoProcessingService`（info / 抽帧 / 转码）
- [x] Avalonia `VideoToolsWindow`
- [x] 菜单 工具 → 视频工具…
- [x] 测试含 Video 相关用例；全量 Core 测试绿


## 后续增量

- [x] 抽帧后「加载为数据集」一键打开输出目录（关闭视频工具窗后主窗口加载）
- [x] GitHub PR #1
