# 剩余进度与路线图

**更新日期：** 2026-07-11  
**基线：** PR #1 · HEAD 以 `git log -1` 为准 · 约 78–80% 整体完成度  

## 已完成基线

- MVP～P3：打标工作台、CUDA ONNX 批量、设置/Wiki/XDG、视频抽帧与加载为数据集  
- P4：模型下载、ONNX 预览、PixAI、Wiki 缓存  
- P5.1 LLM 视觉打标当前图；P5.2 TAG2NL（菜单双入口 + 设置并行数，用户已确认可见）  
- 远程：`origin/feature/avalonia-linux-mvp` · [PR #1](https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/1)  

## 剩余清单

### A. 发布与工程
1. PR #1 审阅合并  
2. **AppImage / `.desktop`**（下一批默认）  
3. CI（build-linux）— 已有 workflow 可加固  
4. README Linux 完整说明  

### B. 打标体验
5. HF/镜像模型下载 UI ✅  
6. PixAI ONNX ✅  
7. ONNX 写入前预览确认 ✅  
8. Wiki 缓存 ✅（翻译仍可选）  
9. 快捷键 / Zoom / 标签多选删  

### C. Plus 大功能
10. LLM 视觉打标 ✅  
11. TAG2NL ✅  
12. 角色审计  
13. 裁剪 / 抠图  
14. 视频时间轴增强  

### D. 质量债
15. 权重序列化对齐原版  
16. 完整 i18n  
17. 旧 settings 迁移  
18. 上游同步流程  

## 建议执行顺序（当前）

**下一批：AppImage** → README Linux 段 → 角色审计（或 PR 收尾）  

协作：Controller / Implementer / QA（见 PHASE4_P2.md）。

## 进度更新

- [x] P4.1 模型下载 UI  
- [x] P4.2 单图 ONNX 预览确认（批量仍直接写；ONNX 页可关预览）
- [x] P4.4 Wiki 本地缓存（7 天，可刷新联网）  
- [x] P4.3 PixAI ONNX 引擎切换 + 多文件下载
- [x] P5.1 LLM 视觉打标当前图
- [x] P5.2 TAG2NL 整库批量（旁系 `_captioned`）
- [x] P5.2 UI：打标/工具双入口 + 设置并行数前置 + dist 重发说明
- [ ] AppImage / 可移植 desktop
- [ ] 角色审计向导
