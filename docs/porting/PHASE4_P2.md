# Phase 4 / P2 + 多子代理协作

**日期：** 2026-07-11  
**分支：** `feature/avalonia-linux-mvp`（已 push `origin`）  
**PR：** https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/new/feature/avalonia-linux-mvp

## 多子代理协作模式（保持）

| 角色 | 职责 |
|------|------|
| **Controller（本会话）** | 拆任务、Todo、串审查、答疑、进度文档、push |
| **Implementer** | 单任务实现 + 测试 + 自检（可本会话或子代理） |
| **QA Spec** | 对照验收清单，禁止只信报告 |
| **QA Quality** | 线程安全、绑定、可维护性 |

流程：`实现 → Spec 审查 →（修）→ Quality 审查 →（修）→ 提交 → 下一任务`  
一次只推进一个大任务的实现流；审查可并行。

## P2 范围

1. **固定用户 Models / 配置目录**（`~/.local/share/bdtm`、`~/.config/bdtm`）  
2. **基础设置页**（语言相关占位、分隔符、模型路径、阈值、FFmpeg）  
3. **Danbooru Wiki 弹窗**（查英文 body + 浏览器打开）  

非本阶段：LLM、审计、视频、完整 i18n、HF 下载 UI。

## 状态（实现中/已完成初版）

- [x] 多子代理协作约定写入本文  
- [x] `AppPaths`：`~/.config/bdtm`、`~/.local/share/bdtm`  
- [x] `AppSettings.LoadUserSettings()`  
- [x] `run-linux.sh` 默认 `BDTM_MODELS_DIR` → 用户 Models  
- [x] 设置窗口（文件/工具菜单）  
- [x] Wiki 弹窗（W 按钮 / 工具菜单）  
- [x] Core 26 tests + Avalonia build  

## 验收

- [x] 默认设置写到用户 config，不绑死 dist/  
- [x] Models 默认 `~/.local/share/bdtm/Models`，仍可用 env/设置覆盖  
- [x] 设置窗口可改并保存  
- [x] 选中标签可开 Wiki  
- [x] Core/Onnx 测试绿，Avalonia 可构建  
