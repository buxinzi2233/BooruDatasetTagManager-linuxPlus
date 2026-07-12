# Wave C RMBG 抠图 · 设计规格

**日期：** 2026-07-12  
**状态：** 已批准  
**波次：** C（接替换+网格之后）

## 目标

1. 轻量 HTTP 客户端调用 AiApiServer 的 `editimage` 端点为 rmbg2 模型去除背景
2. 连接检查 + 模型列表 + 测试预览（可选）+ 批量/选中/当前图应用
3. 导出 `basename_bgremoved.png` + 导入数据集
4. 文档化 AiApiServer 安装启动

## 架构

```
Core: RmbgClient
  - GetConfigAsync(endpoint) → ConfigResponse
  - GetModelsByTypeAsync(endpoint, "rmbg2") → ModelName[]
  - RemoveBackgroundAsync(endpoint, imagePath, modelName) → byte[] (RGBA PNG)

Avalonia: BgRemovalWindow
  - 连接检查 + 模型下拉
  - 可选测试预览（简单 Image 显示）
  - 范围：当前图/选中/全部
  - 确定 → 循环 → SaveAsBgRemoved → AddImages → 刷新
  - 取消

MainViewModel: OpenBgRemovalCommand → dialog → add + refresh

AiApiServer 文档 → docs/porting/AIAPI_SERVER_CN.md
```

## 保存规则

- `{dir}/{base}_bgremoved.png`
- 如果已存在则不覆盖（或追加 _bgremoved_1.png — 首版直接覆盖）

## 非目标

- 完整 AiApi 全套翻译/auto-tag 能力
- 自定义 prompt / 参数调节
- backgroundReplace（颜色替换）
