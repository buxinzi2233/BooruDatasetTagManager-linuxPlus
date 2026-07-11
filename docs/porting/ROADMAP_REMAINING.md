# 剩余进度与路线图

**更新日期：** 2026-07-11  
**基线：** PR #1 · HEAD 以 `git log -1` 为准 · 约 60–65% 整体完成度  

## 已完成基线

- MVP～P3：打标工作台、CUDA ONNX 批量、设置/Wiki/XDG、视频抽帧与加载为数据集  
- 远程：`origin/feature/avalonia-linux-mvp` · [PR #1](https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/1)  

## 剩余清单

### A. 发布与工程
1. PR #1 审阅合并  
2. AppImage / `.desktop`  
3. CI（build-linux）  
4. README Linux 完整说明  

### B. 打标体验
5. **HF/镜像模型下载 UI**（P4.1）  
6. PixAI ONNX  
7. ONNX 写入前预览确认  
8. Wiki 缓存/翻译  
9. 快捷键 / Zoom / 标签多选删  

### C. Plus 大功能
10. LLM 视觉打标  
11. TAG2NL  
12. 角色审计  
13. 裁剪 / 抠图  
14. 视频时间轴增强  

### D. 质量债
15. 权重序列化对齐原版  
16. 完整 i18n  
17. 旧 settings 迁移  
18. 上游同步流程  

## 建议执行顺序

**第 1 波** README + CI → **第 2 波** P4.1 模型下载 → P4.2 预览确认 → **第 3 波** LLM → **第 4 波** AppImage  

协作：Controller / Implementer / QA（见 PHASE4_P2.md）。


## 进度更新

- [x] P4.1 模型下载 UI  
- [x] P4.2 单图 ONNX 预览确认（批量仍直接写；ONNX 页可关预览）
- [x] P4.4 Wiki 本地缓存（7 天，可刷新联网）  
