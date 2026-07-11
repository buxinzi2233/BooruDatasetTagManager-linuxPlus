# 状态 · P5.2 后

**TAG2NL 整库批量** 已接入（移植自 WinForms CaptionGenerationService）。

- Core：`CaptionGenerationService` + `OpenAiVisionClient`；`LlmSettings.Tag2Nl*`
- 设置：T2NL 系统提示词 / 并行数（连接共用 P5.1）
- UI：打标 + 工具 → TAG2NL… · 确认窗 · 进度/取消
- 输出：旁系 `_captioned`（图复制 + 原标签 + NL caption）
- 测试：Core **62** + Onnx **9**
- 说明：运行前若有未保存标签修改则自动保存（较原版少一步确认）
- **运行：** 须 `./scripts/publish-linux.sh` 后再 `./scripts/run-linux.sh`（dist 非 bin）

整体约 **78–80%**。用户已确认菜单与并行数可见。详见 `STATUS_2026-07-11_post_p52_ui.md`。

## 使用

```bash
./scripts/publish-linux.sh
./scripts/run-linux.sh
# 设置… → Endpoint / Key / Vision 模型；向下滚动改 TAG2NL 并行数/提示词 → 保存
# 打开数据集 → 打标或工具 → TAG2NL… → 确认 → 等待进度
```

## 下一批建议

AppImage / 角色审计 / PR 收尾
