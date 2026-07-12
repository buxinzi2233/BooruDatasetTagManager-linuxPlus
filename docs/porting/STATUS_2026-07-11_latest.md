# 状态快照 · 2026-07-11（P4.2 后 · 预览窗 UI 修整后）

**分支：** `feature/avalonia-linux-mvp`  
**HEAD（记录时）：** `a89c41b`  
**远程：** 已同步  
**相对 main：** 35 提交  
**测试：** Core **35** + Onnx **5**  
**PR：** https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/1  

## 一句话

日常打标工作台 + CUDA 批量 ONNX + 设置/Wiki + 视频抽帧 + 模型下载 + **单图预览确认** 已可用。整体约 **65%**。

## 已完成阶段

| 阶段 | 状态 |
|------|------|
| MVP～P3 | ✅ |
| P4.1 模型下载 | ✅ |
| P4.2 单图预览确认 | ✅（UI 线程弹窗 + 选用列宽） |
| PR #1 | ✅ OPEN |
| P4.3 PixAI / P4.4 Wiki 缓存 / LLM / AppImage | 待办 |

## 运行

```bash
./scripts/run-linux.sh
```

配置 `~/.config/bdtm/settings.json` · 模型 `~/.local/share/bdtm/Models/`
