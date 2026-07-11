# 状态 · P5.3 后

**角色标签审计向导** 已移植（Core + Avalonia 三页向导）。

## 交付

| 区域 | 内容 |
|------|------|
| Core | `CharacterTagAudit` 服务、解析/策略/变换、`CharacterTagFileTransaction`（STJ）、`CharacterTagOpenAiAdapter`、ReasonLocalizer |
| 技能 | `Agent/skills/character-tag-auditor` + `prompt-pyramid` 随 publish 拷贝到 dist |
| 设置 | 审计模型（空则 Vision）；Style/Mode/MinCount 记忆字段 |
| UI | 打标/工具 → 角色标签审计… · 选择 → 进度 → 审阅/应用 |
| 写盘 | 事务 Commit + 内存标签刷新 |
| 测试 | Core **127** + Onnx **9** |
| HEAD 参考 | 以 `git log -1` 为准（含 `76c0d19` 一带） |

## 使用

```bash
./scripts/publish-linux.sh   # 必须：更新 dist 并带上 Agent/skills
./scripts/run-linux.sh
# 设置… → Endpoint/Key/Vision（可选审计模型）→ 保存
# 打开数据集 → 打标或工具 → 角色标签审计…
# 选触发词 + 参考图 → 下一步 → 审阅决策 → 应用
```

## 相对原版差异

- 参考图为列表（无缩略图画廊）
- 触发词：TextBox + 候选 Combo（Avalonia 不可编辑 Combo）
- 原因文案未做机翻
- 打开前未保存标签自动保存（同 TAG2NL）

## 整体进度

| 阶段 | 状态 |
|------|------|
| MVP～P5.2 | ✅ |
| P5.3 角色审计 | ✅ |
| AppImage / PR 收尾 | 待办 |
| 整体 | **~82–85%** |

## 下一批建议

AppImage · README Linux · PR #1 收尾
