# 状态 · P5.2 后（UI 可见性 + dist 重发）

**日期：** 2026-07-11  
**HEAD：** `39f49d0`（本地；若已 push 以 `git log -1` 为准）  
**分支：** `feature/avalonia-linux-mvp`  
**PR：** https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus/pull/1  

## 已确认（用户验收）

| 项 | 状态 |
|----|------|
| 打标 → TAG2NL 整库 caption… | ✅ 源码 + dist 含 `RunTag2Nl` |
| 工具 → TAG2NL… | ✅ |
| 设置 → TAG2NL 并行数 (1–100) | ✅ 在 LLM 区块下方（需滚动）；并行数在提示词上方 |
| 设置 → TAG2NL 系统提示词 | ✅ |
| `./scripts/publish-linux.sh` | ✅ dist 时间约 21:45（含 P5.2） |
| 测试 | ✅ Core **62** + Onnx **9** |

## 本阶段交付（P5.2 全链路）

| Commit | 内容 |
|--------|------|
| `1bffd14` | TAG2NL 设计 spec |
| `8f7dfa6` | LlmSettings Tag2Nl 提示词/并发 |
| `f66b3be` | 移植 CaptionGenerationService |
| `d1fdb6e` | OpenAiVisionClient 抽取 |
| `0ba7404` | 设置页 TAG2NL |
| `6382a60` | 确认/进度窗 |
| `567b60b` | MainViewModel 整库编排 |
| `b037ab5` | 状态/路线图 |
| `39f49d0` | 菜单双入口 + 设置滚动/并行数前置 |

## 使用（必须先 publish 再 run）

```bash
./scripts/publish-linux.sh   # 更新 dist/linux-x64（run-linux 读的是 dist，不是 bin/）
./scripts/run-linux.sh
# 设置… → Endpoint / Key / Vision 模型 → 向下滚动改 TAG2NL 并行数与提示词 → 保存
# 打开数据集 → 打标/工具 → TAG2NL… → 确认 → 进度
```

**注意：** 仅 `dotnet build` 不会更新 `run-linux.sh` 使用的 `dist/`。改 UI 后务必 `publish-linux.sh`。

## 已知差异 / 说明

- 未保存标签修改时 TAG2NL **自动保存**后继续（原版有确认框）
- 输出旁系 `{dataset}_captioned/`，不改当前库内存标签
- 连接配置与 P5.1 共用；T2NL 提示词/并发独立

## 整体进度

| 阶段 | 状态 |
|------|------|
| MVP～P4（PixAI、Wiki 缓存、预览、下载） | ✅ |
| P5.1 LLM 当前图 | ✅ |
| P5.2 TAG2NL | ✅（用户已确认菜单/并行数可见） |
| 角色审计 / AppImage | 待办 |
| 整体 | **~78–80%** |

## 下一批

默认：**AppImage + 可移植 `.desktop`**（P6.3 前移，降低分发门槛；衔接刚修好的 publish/dist 路径）。  
备选：角色审计向导 / PR 收尾与 README Linux 段。
