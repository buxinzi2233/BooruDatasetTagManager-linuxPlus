# 状态快照 · 2026-07-11（P2 完成后 · 文档同步）

**分支：** `feature/avalonia-linux-mvp`  
**HEAD（记录时）：** `3df4133`  
**远程：** `origin/feature/avalonia-linux-mvp`（已跟踪并 push）  
**相对 main 提交数：** 22  
**开 PR：** https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/new/feature/avalonia-linux-mvp  

**验证：** Core **26** + Onnx **5** 测试通过；用户确认三栏 UI 显示正常；P2 初版已合入分支  

---

## 1. 一句话结论

Linux 原生 Avalonia 工作台已覆盖**日常图片数据集打标主路径**（打开 → 编辑/保存 → 中文对照 → 批量 WD14 CUDA 打标 → 用户配置/设置页 → Wiki 查询）。  
**不是** WinForms Plus 全功能 1:1 移植。  
**主观整体进度：约 55–60%。**

---

## 2. 架构

```
src/Bdtm.Core      数据集 / 标签 / AppSettings / AppPaths / FfmpegLocator /
                   ChineseTagLookup / TagStatistics / DanbooruWikiClient
src/Bdtm.Onnx      CUDA→CPU / WD14 / TagWrite
src/Bdtm.Avalonia  三栏 UI + SettingsWindow + WikiWindow
tools/             OnnxEpProbe, LoadStress
scripts/           build-linux.sh, publish-linux.sh, run-linux.sh
docs/porting/      清单 / 架构 / 阶段 / 快照
BooruDatasetTagManager/  原 WinForms（对照，非 Linux 目标）
```

---

## 3. 阶段完成度

| 阶段 | 内容 | 状态 |
|------|------|------|
| Phase 0 | 可行性、耦合清单、SDK、clone | ✅ |
| Phase 1 MVP | Core + Onnx + 最小 UI 闭环 | ✅ |
| Phase 2 P0 | 缩略图、中文列、全局表、预览 | ✅ 用户确认 |
| Phase 3 P1 | 批量 ONNX、排序、过滤 | ✅ |
| UI 对齐 | 三栏 + 中栏右侧工具条 + 表头/路径 | ✅ 用户确认 |
| Phase 4 P2 | 用户 config/Models、设置页、Wiki | ✅ 初版 |
| 远程备份 | push 分支 | ✅ |
| Plus 全功能 | LLM/审计/视频/裁剪/HF 下载/AppImage… | ❌ backlog |

---

## 4. 已具备能力

### 数据集与标签
- 打开文件夹、缩略图列表、可选完整路径  
- 当前标签：中文 / 权重 / 增删 / 上下移 / 过滤  
- 全部标签：中文 / 计数 / 过滤 / 添加到当前  
- 保存 sidecar `.txt`（权重可保留）  
- 右侧 Tab：全部标签 · 预览 · ONNX  

### ONNX
- WD14，CUDA 优先、失败回退 CPU 并显示原因  
- 当前 / 多选 / 全部 + 进度 + 取消  
- 懒加载会话（启动不占显存）  
- `run-linux.sh` 注入 CUDA 用户态库路径  

### P2 桌面集成
- 配置：`~/.config/bdtm/settings.json`  
- Models：`~/.local/share/bdtm/Models`（可 env / 设置覆盖）  
- 设置窗口（分隔符、Models、阈值、FFmpeg 路径等）  
- Danbooru Wiki 弹窗（W / 工具菜单）  

### FFmpeg
- **仅视频管线预留**（定位器 + 设置项）  
- **当前图片打标不依赖 FFmpeg**  
- 视频转码/抽帧 UI **未移植**  

---

## 5. 未做（P3+ backlog）

- 开 GitHub PR（链接已有，未点创建）  
- LLM 视觉 / TAG2NL / 角色审计  
- 视频工具 UI（真正用上 FFmpeg）  
- 裁剪 / 抠图 / AiApiServer  
- PixAI ONNX、HF 下载 UI  
- 完整 i18n、列表 Zoom、完整快捷键  
- AppImage / AUR  
- 与上游 Plus 自动同步  

---

## 6. 多子代理协作（保持）

见 `docs/porting/PHASE4_P2.md`：

Controller → Implementer → QA Spec → QA Quality → 提交  

---

## 7. 运行

```bash
cd ~/Projects/BooruDatasetTagManager-linuxPlus
./scripts/build-linux.sh
./scripts/publish-linux.sh
./scripts/run-linux.sh
```

- 配置：`~/.config/bdtm/settings.json`  
- 模型：`~/.local/share/bdtm/Models/<org>/<repo>/`  

---

## 8. 进度百分比（主观）

| 工作包 | 完成度 |
|--------|--------|
| 工程骨架 / 脚本 | ~95% |
| Core（日常打标） | ~90% |
| ONNX WD14 + CUDA | ~85% |
| Avalonia 工作台 UI | ~80% |
| 桌面集成（路径/设置/Wiki） | ~70% |
| Plus 扩展功能 | ~15–25% |
| **整体 Linux 移植** | **~55–60%** |

---

*记录原因：用户要求「记录当前状态，回报项目总体进度」；同步过时文档（原 PROGRESS 仍写未 push、测试数偏旧）。*
