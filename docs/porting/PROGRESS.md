# 开发进度记录

**更新日期：** 2026-07-11  
**分支：** `feature/avalonia-linux-mvp`  
**仓库：** `/home/buxinzi/Projects/BooruDatasetTagManager-linuxPlus`  
**路线：** B — Avalonia 原生 Linux 移植（核心 MVP）  
**相对 main 提交数：** 9

---

## 1. 总体结论

| 维度 | 状态 |
|------|------|
| 核心 MVP 目标 | **已达成并可日常使用** |
| 单元测试 | Core 19 + Onnx 5 = **24 全部通过** |
| 用户手测 | 打开数据集 / 编辑保存标签 / ONNX 打标 **通过** |
| CUDA | **可用**（需 `./scripts/run-linux.sh` 或正确 `LD_LIBRARY_PATH`） |
| 相对 WinForms Plus 全功能 | 约 **MVP ~40% 功能面**；领域底座已立，大量对话框/LLM/视频未移植 |
| 远程推送 | 本地分支，**尚未 push**（如需开 PR 再推） |

一句话：**Linux 上已经能完成「打开数据集 → 改标签 → 保存 → WD14 CUDA 打标」闭环；不是完整 Plus 克隆。**

---

## 2. 架构落地

```
src/Bdtm.Core      net8.0   数据集 / 标签 / 设置 / FFmpeg 定位
src/Bdtm.Onnx      net8.0   CUDA→CPU 会话工厂 / WD14 / 写回
src/Bdtm.Avalonia  net8.0   Avalonia 11 三栏 UI
tools/OnnxEpProbe           EP 探测（CUDA/CPU 实测）
BooruDatasetTagManager/     原 WinForms（保留对照，非 Linux 目标）
```

文档：

- `docs/porting/INVENTORY.md` — Windows 耦合清单  
- `docs/porting/ARCHITECTURE.md` — 模块边界与 CUDA 说明  
- `docs/porting/MVP_STATUS.md` — 完成/未完成勾选  
- `docs/porting/PROGRESS.md` — 本进度记录  

脚本：

| 脚本 | 作用 |
|------|------|
| `scripts/build-linux.sh` | 构建 Core/Onnx/Avalonia + 跑测试 |
| `scripts/publish-linux.sh` | 自包含 `dist/linux-x64` |
| `scripts/run-linux.sh` | 带 CUDA 12 库路径启动（推荐） |

---

## 3. 提交历史（本分支）

| SHA | 说明 |
|-----|------|
| `f7f3581` | Windows 耦合清单 |
| `39c423a` | Bdtm.Core 骨架 + xUnit |
| `95e07ab` | 可移植 AppSettings + FfmpegLocator |
| `4073b24` | DatasetManager / TagList / PromptParser |
| `ff1b5bb` | Bdtm.Onnx CUDA 优先 WD14 + TagWrite |
| `a84cd35` | Avalonia 三栏 MVP + 发布/文档 |
| `6b9d96a` | 启动检测模型文件 vs 会话状态 |
| `3be0947` | CUDA 回退原因 + run-linux.sh + OnnxEpProbe |
| `7ef6369` | 文案：就绪 ≠ 已加载会话 |

---

## 4. MVP 功能对照

### 已完成

- [x] 打开数据集文件夹（图片/视频扩展名扫描）
- [x] 三栏 UI：图列表 | 当前标签 | 全局标签 + ONNX 设置
- [x] 标签增删、保存 sidecar `.txt`
- [x] 设置 JSON（语言/分隔符/阈值/模型 repo/ModelsPath 等子集）
- [x] WD14 ONNX 打当前图（写回 Append/Replace）
- [x] CUDA 优先，失败回退 CPU，并显示原因
- [x] 懒加载会话：启动只查文件，点打标才占显存
- [x] 本机模型 symlink（sd-image-sorter / AnimaLoraStudio 缓存）
- [x] Linux 构建/发布/CUDA 启动脚本

### 明确未做（backlog）

- [ ] LLM 视觉打标 / TAG2NL
- [ ] 角色标签审计向导
- [ ] 视频工具 UI
- [ ] 裁剪 / 抠图 / AiApiServer 集成
- [ ] PixAI ONNX
- [ ] HF 模型下载 UI
- [ ] 列表缩略图预览
- [ ] 完整 i18n（多语言 txt 对齐）
- [ ] 批量 ONNX 整目录
- [ ] AppImage / AUR
- [ ] 与上游 Plus 持续同步策略自动化

---

## 5. 环境与已知问题

### 环境

- CachyOS · niri · .NET **8.0.128** · FFmpeg 系统包 · GPU **Tesla V100**
- 模型示例：`dist/linux-x64/Models/SmilingWolf/wd-eva02-large-tagger-v3` → symlink 到本机已有 1.2G ONNX

### 已知问题 / 注意

1. **直接跑 `Bdtm.Avalonia` 常落 CPU**：缺 `libcublasLt.so.12` 等；用 `./scripts/run-linux.sh`。  
2. **publish 会重建 dist/**：模型 symlink 可能丢失，需再链。  
3. **启动「就绪」≠ 会话已加载**：首次打标才创建 CUDA 会话（设计如此）。  
4. **标签模型为简化版**：无 WinForms BindingSource/完整权重括号历史；权重序列化简化。  
5. **无缩略图 / 无批量 ONNX UI**。  
6. **分支未 push** 到 GitHub。  
7. 对话中曾出现 PAT，应已轮换（安全）。

### CUDA 实测（OnnxEpProbe）

| 模式 | 单次推理量级 |
|------|----------------|
| CPU（无 CUDA 用户态库） | ~2.3 s |
| CUDA（补齐 LD_LIBRARY_PATH） | ~0.2 s |

---

## 6. 推荐日常命令

```bash
cd ~/Projects/BooruDatasetTagManager-linuxPlus

# 开发验证
./scripts/build-linux.sh

# 发布
./scripts/publish-linux.sh
ln -sfn ~/Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger/wd-eva02-large-tagger-v3 \
  dist/linux-x64/Models/SmilingWolf/wd-eva02-large-tagger-v3

# 运行（CUDA）
./scripts/run-linux.sh

# EP 探测
dotnet run --project tools/OnnxEpProbe -c Release -- \
  dist/linux-x64/Models SmilingWolf/wd-eva02-large-tagger-v3 /path/to.jpg
```

---

## 7. 建议下一步（优先级）

1. **P0** `git push -u origin feature/avalonia-linux-mvp`（备份/协作）  
2. **P1** 批量 ONNX（选中/全部）+ 进度条  
3. **P1** 列表缩略图（ImageSharp 异步）  
4. **P2** HF 下载或固定 Models 用户目录（`~/.local/share/bdtm/Models`）  
5. **P2** LLM / TAG2NL（已有 Core 侧可接 HTTP）  
6. **P3** AppImage 打包  

---

## 8. 进度快照（百分比主观）

| 工作包 | 完成度 |
|--------|--------|
| 工程骨架 / CI 本地脚本 | 95% |
| Core 领域（MVP 范围） | 85% |
| ONNX WD14 + CUDA 路径 | 80%（缺系统级 CUDA 安装文档/打包内置） |
| Avalonia UI MVP | 70% |
| Plus 功能对齐 | 15–25% |
| **整体 Linux 移植** | **约 35–40%**（MVP 可用；全量仍长） |

*记录人：实现会话 2026-07-11*


---

## 9. Phase 2（P0 工作台对齐）— 2026-07-11

对照原版截图后的差异补齐：

- [x] 左栏缩略图 + 可选路径（视图菜单）
- [x] 中栏中文翻译列（`danbooru-0-zh.csv` / `ChineseTagLookup`）
- [x] 右栏全局标签表（标签 | 中文 | 计数）+ 添加到当前
- [x] 选中图预览区

文档：`docs/porting/UI_GAP_AND_PHASE2.md`

测试：Core **21** + Onnx **5**。


---

## 10. Phase 3 / P1（效率）— 2026-07-11

用户确认 P0 布局与 CUDA 打标正常后进入：

- [x] 记录 P1 计划（`docs/porting/PHASE3_P1.md`）
- [x] 标签上移/下移、删除选中
- [x] 批量 ONNX（当前/多选/全部）+ 进度/取消
- [x] 当前/全局标签过滤搜索


---

## 11. 稳定快照（2026-07-11 · 用户确认 UI 正常）

详见 **`docs/porting/STATUS_2026-07-11.md`**。

- HEAD `ec5bf44`，分支较 main **20** 提交  
- 三栏布局 + 中栏右侧工具条 + 右栏 Tab（全部标签/预览/ONNX）  
- 测试 Core 23 + Onnx 5 绿  
- 整体移植约 **50–55%**（日常打标可用）  


---

## 12. Phase 4 / P2（2026-07-11）

- 远程分支已 push：`origin/feature/avalonia-linux-mvp`  
- 多子代理协作模式写入 `docs/porting/PHASE4_P2.md`  
- P2 初版：用户配置/Models 目录、设置页、Danbooru Wiki 弹窗  
