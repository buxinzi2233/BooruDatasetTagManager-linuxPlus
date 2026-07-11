# 状态快照 · 2026-07-11（P3 完成后）

**分支：** `feature/avalonia-linux-mvp`  
**HEAD：** `529981f`  
**远程：** `origin/feature/avalonia-linux-mvp`（已同步）  
**相对 main：** 25 提交  
**测试：** Core **32** + Onnx **5** 通过  

---

## 一句话

Linux Avalonia 移植已覆盖：**图片数据集日常打标 + 批量 CUDA ONNX + 用户配置/设置 + 联网 Wiki + FFmpeg 视频抽帧工具**。  
整体约 **60–65%**（相对 WinForms Plus 全功能）。

---

## 阶段

| 阶段 | 状态 |
|------|------|
| MVP 闭环 | ✅ |
| P0 工作台 | ✅ 用户确认 |
| P1 效率（批量/排序/过滤） | ✅ |
| UI 三栏对齐 + 表头/路径 | ✅ 用户确认 |
| P2 用户目录/设置/Wiki | ✅ |
| P3 视频工具（FFmpeg） | ✅ 初版 |
| 工具菜单补全 | ✅ `529981f` |
| PR / LLM / 审计 / AppImage | ❌ |

---

## 能力清单

**有：** 缩略图列表、中英标签、权重、上下移、过滤、全部标签表、预览、ONNX 当前/多选/全部+进度取消、CUDA（run-linux）、XDG 配置与 Models、设置窗、Wiki（**联网** Danbooru API）、视频信息/抽帧 UI。  

**无：** LLM/TAG2NL、角色审计、完整视频时间轴、裁剪抠图、HF 下载、完整 i18n、AppImage、已创建的 PR。

**Wiki：** 联网 `danbooru.donmai.us`，非本地文件。  
**中文列：** 本地 `danbooru-0-zh.csv`。  
**FFmpeg：** 视频工具使用；纯图片打标不依赖。

---

## 运行

```bash
cd ~/Projects/BooruDatasetTagManager-linuxPlus
./scripts/run-linux.sh
# 菜单：文件 | 打标 | 视图 | 工具（视频/Wiki/设置）
```

- 配置：`~/.config/bdtm/settings.json`  
- 模型：`~/.local/share/bdtm/Models/`  

---

## 协作

多子代理：Controller / Implementer / QA Spec / QA Quality（`PHASE4_P2.md`）。

---

## 建议下一步

1. 创建 GitHub PR  
2. 抽帧后一键加载输出目录为数据集  
3. LLM/TAG2NL 或 Wiki 缓存/翻译  
4. AppImage  

*记录：用户再次要求记录状态与进度汇报。*
