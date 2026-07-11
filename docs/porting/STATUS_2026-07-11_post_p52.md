# 状态 · P5.2 后

**TAG2NL 整库批量** 已接入（移植自 WinForms CaptionGenerationService）。

- Core：`CaptionGenerationService` + `OpenAiVisionClient`；`LlmSettings.Tag2Nl*`
- 设置：T2NL 系统提示词 / 并行数（连接共用 P5.1）
- UI：工具 → TAG2NL… · 确认窗 · 进度/取消
- 输出：旁系 `_captioned`（图复制 + 原标签\\n NL caption）
- 测试：Core Caption* + settings Tag2Nl + 既有 LLM/Onnx（build-linux 绿）
- 说明：运行前若有未保存标签修改则自动保存（较原版少一步确认）

整体约 **78–80%**。

## 使用

```bash
./scripts/run-linux.sh
# 设置… → 填 LLM Endpoint / Key / Vision 模型，可选改 TAG2NL 提示词与并行数
# 打开数据集 → 工具 → TAG2NL… → 确认 → 等待进度
```

## 下一批建议
角色审计 / AppImage / PR 收尾
