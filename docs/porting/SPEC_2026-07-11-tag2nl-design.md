# P5.2 TAG2NL 设计规格

**日期：** 2026-07-11  
**状态：** 已批准（brainstorming）  
**分支：** `feature/avalonia-linux-mvp`  
**前置：** P5.1 LLM 视觉打标当前图（`OpenAiVisionTagger` + `LlmSettings`）  
**方案：** A — 原版 `CaptionGenerationService` 整体迁入 Core + Avalonia 确认/进度窗  

---

## 1. 目标与非目标

### 1.1 目标

在 Avalonia Linux 版实现与 WinForms Plus **行为对齐** 的整库 **TAG2NL**（Tags → Natural Language caption）：

1. 扫描当前数据集根目录下全部支持图片  
2. 确认窗展示摘要，可选「重新生成已有输出」  
3. 并发调用 OpenAI 兼容视觉 `/chat/completions`  
4. 输出到旁系 `{datasetName}_captioned/` 树：复制图片 + 写入 `原标签\nNL caption` 的 `.txt`  
5. 进度展示与可取消；结果汇总成功/跳过/失败  

### 1.2 已锁定需求

| 项 | 选择 |
|----|------|
| 范围 | 整库批量（非仅当前图） |
| 输出格式 | 原标签原文 + 换行 + NL caption |
| LLM 配置 | 共用 Endpoint / API Key / Vision 模型 / Timeout；**独立** T2NL System Prompt + 并发度 |

### 1.3 非目标（本阶段不做）

- 完整 i18n 五语言资源文件（UI 中文硬编码，与当前 Avalonia 一致）  
- 触发词 UI、自定义 OutputTemplate、Hybrid/自然语言2 模板切换  
- 生成后自动将 `_captioned` 加载为当前数据集  
- 角色审计向导、AppImage、抠图/裁剪  
- 第二套独立 Endpoint/Key/Model  

---

## 2. 架构与模块边界

### 2.1 分层

```
Bdtm.Avalonia
  菜单「工具 → TAG2NL…」
  Tag2NlConfirmWindow / Tag2NlProgressWindow
  MainViewModel.RunTag2NlAsync（编排）
        │
        ▼
Bdtm.Core
  CaptionGenerationService + PromptBuilder + OutputFormatter + ImagePreprocessor
  OpenAiVisionClient（通用 chat + image）
  OpenAiVisionTagger（打标适配，复用 Client；P5.1 行为不变）
  AppSettings.Llm + Tag2Nl* 字段
```

### 2.2 模块职责

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `CaptionGenerationService` | 扫描、并发、跳过、写盘、进度/结果 DTO | HTTP、UI |
| `CaptionPromptBuilder` | 清洗标签、拼 user prompt | 读全局设置 |
| `CaptionOutputFormatter` | 去 think 块；`原标签 + \n + caption` | 网络 |
| `CaptionImagePreprocessor` | MaxMP 缩图 → JPEG bytes | 业务策略 |
| `OpenAiVisionClient` | `/chat/completions` + data URL；返回纯文本 | ParseTags、写文件 |
| `OpenAiVisionTagger` | 现有打标：Client + ParseTags | TAG2NL 批处理 |
| Avalonia 确认/进度窗 | 摘要、重跑开关、进度与取消 | 算法 |

### 2.3 依赖

- `Bdtm.Core` 增加 `SixLabors.ImageSharp` **3.1.12**（与 Onnx/Avalonia 同版），支撑预处理与 Core 单测。  
- 不新增 OpenAI SDK；继续手写 JSON + `HttpClient`。

---

## 3. 设置与配置

### 3.1 `LlmSettings` 扩展

**已有（P5.1 共用连接与打标提示词）：**

- `Endpoint`, `ApiKey`, `VisionModel`, `TimeoutSeconds`, `Temperature`  
- `SystemPrompt`, `UserPrompt`, `SplitTags`, `Splitter` — **仅视觉打标**

**新增（仅 TAG2NL）：**

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `Tag2NlSystemPrompt` | string | 原版 NaturalLanguage 系统提示词全文 | 可在设置中编辑 |
| `Tag2NlConcurrency` | int | `5` | 保存时 clamp 到 1–100 |

### 3.2 默认 `Tag2NlSystemPrompt` 正文

与 `AiPromptTemplateCatalog` 中 NaturalLanguage 模板一致：

```
Describe this image in a detailed, objective, and realistic natural language paragraph for AI training.
Rules:
1. Start directly with the main subject and their action (e.g., "A photograph of a young woman with blue hair sitting at a desk...").
2. Describe details in order: subject (clothing, expression, hairstyle, posture), immediate surroundings, background elements, lighting, and style.
3. Avoid quality buzzwords (e.g. masterpiece, photorealistic, ultra-detailed) and subjective emotional opinions. Keep the description flowing naturally as a coherent paragraph.
4. Do not output a comma-separated tag list. Reference tags are hints only and must be rewritten as natural prose.
```

### 3.3 设置页 UI

在 LLM 视觉打标区块下方增加：

- 分组标题：`TAG2NL`  
- 多行：`TAG2NL 系统提示词` → `Tag2NlSystemPrompt`  
- `NumericUpDown`：`TAG2NL 并行数`（1–100）→ `Tag2NlConcurrency`  
- 说明：连接与 Vision 模型同上；本区仅影响 TAG2NL  

### 3.4 有效性校验

运行 TAG2NL 前：

- `Endpoint` 为绝对 `http`/`https` URI  
- `VisionModel` 非空  
- API Key 允许为空（本地/兼容网关）  

失败时状态栏提示，并引导打开设置（与现有 LLM 打标提示风格一致）。

---

## 4. HTTP 客户端

### 4.1 `OpenAiVisionClient`

从现有 `OpenAiVisionTagger` 抽出请求/响应逻辑：

```csharp
public sealed class OpenAiVisionClient : IDisposable
{
    public OpenAiVisionClient(LlmSettings settings, HttpClient? http = null);

    public Task<OpenAiVisionCompletionResult> CompleteAsync(
        OpenAiVisionCompletionRequest request,
        CancellationToken ct = default);
}

public sealed class OpenAiVisionCompletionRequest
{
    public string SystemPrompt { get; init; } = "";
    public string UserPrompt { get; init; } = "";
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "image/jpeg";
}

public sealed class OpenAiVisionCompletionResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = "";
    public string? ErrorMessage { get; init; };
}
```

### 4.2 协议行为（与 P5.1 对齐）

- URL：`Endpoint` 已含 `/chat/completions` 则原样使用，否则拼接 `/chat/completions`  
- Header：`Authorization: Bearer {ApiKey}`（Key 非空时）  
- Body：`model`、`temperature`（≥0 时）、`messages`（可选 system + user content 数组：text + image_url data URL）  
- 解析：`choices[0].message.content`（string 或 array parts）  
- **不** 在 Client 内 ParseTags  

### 4.3 适配

| 调用方 | 用法 |
|--------|------|
| `OpenAiVisionTagger` | `CompleteAsync` + 现有 `ParseTags` / `StripThinking`；对外 API 保持 `TagImageAsync` |
| TAG2NL | 委托：`CompleteAsync(system=Tag2NlSystemPrompt, user=动态 user prompt, image=JPEG)` → `CaptionModelResponse` |

P5.1 单图打标的用户可见行为不得回归。

---

## 5. Caption 服务（Core）

### 5.1 类型（命名空间 `Bdtm.Core`）

从 WinForms `CaptionGenerationService.cs` 迁入并去 UI 依赖，保留语义：

- `CaptionGenerationOptions`  
- `CaptionModelRequest` / `CaptionModelResponse`  
- `CaptionGenerationResult` / `CaptionGenerationProgress` / `CaptionProgressStage`  
- `CaptionScanResult`  
- `CaptionGenerationService`  
- `CaptionPromptBuilder` / `CaptionOutputFormatter` / `CaptionImagePreprocessor`  

### 5.2 Options 默认值

| 属性 | 默认 |
|------|------|
| `OutputSuffix` | `_captioned` |
| `SkipExisting` | `true` |
| `AddTriggerToAi` | `true` |
| `AddTagsToAi` | `true` |
| `ReplaceUnderscores` | `true` |
| `MaxPixelsMegapixels` | `1.0` |
| `TriggerWords` | `""`（首版 UI 不暴露） |
| `SystemPrompt` | 由编排填入 `Llm.Tag2NlSystemPrompt` |
| `OutputTemplate` | `{caption}`（写盘主路径用 `FormatOriginalTagsAndCaption`，与原版一致） |
| `MaxConcurrency` | `5`（编排填入 `Llm.Tag2NlConcurrency`） |

### 5.3 扫描

- 扩展名：`.png` `.jpg` `.jpeg` `.webp` `.bmp`（忽略大小写）  
- `SearchOption.AllDirectories`，路径排序  
- `OutputRoot = Path.Combine(parent, folderName + suffix)`  
- 若 `GetOutputTextPath` 对应 `.txt` 已存在 → Existing  
- `Pending = Total - Existing`  

### 5.4 单图处理

1. 读旁侧同名 `.txt`：`ReadTags`（逗号拆分）与 `ReadOriginalTags`（原文）  
2. `CaptionPromptBuilder.BuildUserPrompt`（过滤 masterpiece/best quality/highres/absurdres；下划线→空格；优先级排序；附加「一段连贯英文自然段」规则）  
3. `CaptionImagePreprocessor.LoadJpegAsync`（超 MaxMP 等比缩小，JPEG Q=90）  
4. 调用注入的 `requestCaptionAsync`  
5. 空 `Result` → 该图失败  
6. `FormatOriginalTagsAndCaption(originalTags, caption)`  
7. `WriteOutputAsync`  

### 5.5 写盘语义

| 项 | 行为 |
|----|------|
| 输出位置 | 源数据集**同级**旁系目录 |
| 结构 | 保留相对路径嵌套 |
| txt | `原标签.TrimEnd(CR/LF) + Environment.NewLine + caption`；无有效标签则仅 caption |
| 图片 | 目标图不存在才复制；**不覆盖**已有输出图 |
| 重跑 | `SkipExisting=false` 时覆盖 **txt**；图仍不强制重拷 |
| 原子性 | `*.tmp-{guid}` 写入后 `File.Move`；失败清理 tmp |

### 5.6 并发与取消

- `Parallel.ForEachAsync`，`MaxDegreeOfParallelism = Clamp(MaxConcurrency, 1, 100)`  
- 单图失败计入 Failed，继续其余；`Errors` 最多保留 20 条  
- 取消：`Canceled=true`；已完成计数保留；取消本身不额外计 Failed  

### 5.7 隔离

- TAG2NL **只写旁系目录**，不修改当前打开数据集的内存标签模型  
- 不自动切换工作区到 `_captioned`  

---

## 6. UI 与交互

### 6.1 入口

- 菜单 **工具 → TAG2NL…** → `RunTag2NlCommand`  
- **不** 放侧栏快捷按钮（整库任务）  
- **不** 与「LLM 视觉打标当前图」共用菜单项  

### 6.2 主流程 `RunTag2NlAsync`

1. 未加载数据集 → 提示并返回  
2. 有未保存修改 → 确认「保存全部并继续？」；否返回；是则走现有保存全部  
3. LLM 配置无效 → 提示并引导设置  
4. 扫描 `DatasetRoot`；失败则报错  
5. `Tag2NlConfirmWindow`；取消则返回  
6. `SkipExisting = !ReprocessExisting`；若跳过且 `Pending==0` → 直接结果提示  
7. `Tag2NlProgressWindow` 内跑 `ProcessAsync`  
8. 状态栏摘要；有错误时额外展示最多 5 条  

运行中设置 busy，防止重复启动第二轮 TAG2NL。

### 6.3 确认窗 `Tag2NlConfirmWindow`

- 标题：`确认 TAG2NL`  
- 只读摘要：根目录 / 总数 / 已有 / 待处理 / 输出目录  
- 复选框：`重新生成已有输出`（默认未勾选）  
- 按钮：`开始` / `取消`  
- `ShowDialog<bool?>`  

### 6.4 进度窗 `Tag2NlProgressWindow`

- 标题：`TAG2NL 进度`  
- 当前文件、计数（成功/跳过/失败）、进度条 `Completed/Total`  
- `取消` → 协作取消；文案「正在取消…」  
- 运行中关闭窗体 = 请求取消  
- 进度回调必须回到 UI 线程更新绑定  

### 6.5 结果

- 状态栏：`TAG2NL 完成 · 成功 n · 跳过 n · 失败 n` 或已取消变体；可附带输出路径  
- 失败/有 Errors 时信息窗最多 5 条  

### 6.6 编排位置

默认：`MainViewModel` 编排 + 独立 Confirm/Progress ViewModel（对齐 VideoTools/Wiki 打开方式）。  
若实现时 `MainViewModel` 增幅过大，可抽 `Tag2NlSession` 小类，规格不强制。

---

## 7. 测试计划

### 7.1 Core 单测（`tests/Bdtm.Core.Tests`，移植/改写自 `CaptionGenerationTests`）

| 用例 | 断言要点 |
|------|----------|
| PromptBuilder 清洗 | 含 reference tags；无 masterpiece/best quality；下划线转空格；含自然段规则 |
| 打标 vs T2NL 提示词分离 | 默认 `Llm.SystemPrompt` ≠ 默认 `Tag2NlSystemPrompt`；后者含 natural language paragraph |
| OutputFormatter think 块 | `<think>…</think>` 去除 |
| FormatOriginalTagsAndCaption | 恰好一个边界换行；无标签时仅 caption |
| 输出路径 | 旁系 `…/dataset_captioned/nested/image.txt` |
| Scan 计数 | Total / Pending / Existing / OutputRoot |
| 单图失败继续 | Succeeded+Failed 正确；成功文件存在 |
| 取消 | `Canceled`；Failed 不因取消虚增 |
| 不改源文件 | 源图字节与源 txt 不变 |
| 重跑覆盖 txt 不覆盖已有输出图 | SkipExisting=false |
| 并发上限 | MaxConcurrency=3 时观测峰值 ≤3 |
| Options 默认并发 | `MaxConcurrency == 5` |
| AppSettings 往返 | `Tag2NlSystemPrompt` / `Tag2NlConcurrency` 保存加载正确；并发 clamp |

### 7.2 Client / 打标回归

| 用例 | 断言 |
|------|------|
| 现有 `OpenAiVisionTagger.ParseTags_*` | 全部仍绿 |
| （可选）Client URL 拼接 | 给定 endpoint 拼出正确 chat/completions（纯函数级即可） |

### 7.3 手工验收

1. 配置 LLM Endpoint + Key + Vision 模型 +（可选）改 T2NL 提示词/并发 → 保存  
2. 打开带 `.txt` 标签的小数据集  
3. 工具 → TAG2NL… → 确认摘要路径正确 → 开始  
4. 进度可取消；完成后旁系目录有图+txt，内容为标签行+自然段  
5. 再跑默认跳过已有；勾选重跑则更新 txt  
6. 未配置模型时有清晰提示  
7. P5.1「LLM 视觉打标当前图」仍可用，且仍产出标签而非长段落（提示词未串用）  

### 7.4 自动化门槛

`./scripts/build-linux.sh`（或等价 Core + Onnx 测试）在合并本功能后保持绿色。  
预期 Core 测试数量在现有基础上增加 Caption/TAG2NL 相关用例（约十数条量级）。

---

## 8. 验收标准

- [ ] 已加载数据集可从 **工具 → TAG2NL…** 启动整库流程  
- [ ] 确认窗显示 Total / Existing / Pending / OutputRoot；默认跳过已有  
- [ ] 输出在旁系 `_captioned` 树；txt = 原标签 + 换行 + NL caption；图片复制且不覆盖已有输出图  
- [ ] 并发与取消可用；失败不中断整批  
- [ ] 设置可编辑 T2NL 系统提示词与并行数；连接配置与 P5.1 共用  
- [ ] Core 单测覆盖扫描/写盘/提示词/并发/源文件不变  
- [ ] P5.1 视觉打标无回归  
- [ ] 状态文档更新：`STATUS_*_post_p52.md`、`ROADMAP_REMAINING.md`、`MVP_STATUS.md` 勾选 P5.2  

---

## 9. 风险与缓解

| 风险 | 缓解 |
|------|------|
| Core 引入 ImageSharp 增大依赖面 | 锁定与 Onnx 同版本 3.1.12；仅预处理使用 |
| 高并发打爆 API / 本地网关 | 默认并发 5；设置可降；错误计入 Failed 不崩进程 |
| 大图 base64 内存 | 先缩到 ≤1.0 MP 再 JPEG |
| MainViewModel 继续膨胀 | 编排保持薄；对话框独立 VM；必要时抽 Session |
| 与 P5.1 提示词串用 | 字段分离 + 单测断言默认文案不同 |
| 未保存标签导致 caption 基于旧 txt | 运行前强制保存确认（对齐原版） |
| WinForms 测试依赖 ImageSharp 写 PNG | Core 测试项目同样引用 ImageSharp（经 Core 传递或测试项目直接引用） |

---

## 10. 实现顺序（供 writing-plans 展开）

1. Core：`LlmSettings` 字段 + 默认提示词常量  
2. Core：迁入 Caption* 类型与服务；csproj 加 ImageSharp  
3. Core：`OpenAiVisionClient`；`OpenAiVisionTagger` 改为委托 Client  
4. Core 测试：移植 CaptionGenerationTests + settings 往返 + 提示词分离  
5. Avalonia：设置页绑定  
6. Avalonia：Confirm / Progress 窗 + VM  
7. Avalonia：`RunTag2NlAsync` 菜单入口  
8. 全量 `build-linux` / 手工冒烟  
9. 状态文档 + commit / push  

协作可沿用 Controller → Implementer → QA 检查点（见 `docs/porting/PHASE4_P2.md`）。

---

## 11. 参考源码

| 路径 | 用途 |
|------|------|
| `BooruDatasetTagManager/CaptionGenerationService.cs` | 服务与写盘权威实现 |
| `BooruDatasetTagManager/Form1.cs` `RunLlmT2NlAsync` | UI 编排权威流程 |
| `BooruDatasetTagManager/Form_LlmT2NlConfirm.cs` / `Form_LlmT2NlProgress.cs` | 对话框行为 |
| `BooruDatasetTagManager/AiPromptTemplateCatalog.cs` | 默认 T2NL system prompt |
| `BooruDatasetTagManager.Tests/CaptionGenerationTests.cs` | 单测权威断言 |
| `src/Bdtm.Core/OpenAiVisionTagger.cs` | 现有 HTTP 实现，待抽出 Client |
| `src/Bdtm.Avalonia/ViewModels/MainViewModel.cs` | P5.1 入口与对话框模式 |
