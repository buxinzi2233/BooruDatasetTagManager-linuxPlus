# P5.3 角色标签审计 · 设计规格

**日期：** 2026-07-11  
**状态：** 已批准（brainstorming 第 1–3 块）  
**分支：** `feature/avalonia-linux-mvp`  
**性质：** WinForms Plus → Avalonia/Linux **移植**  
**交付形态：** 单 PR 全量（Core + 完整向导主路径）  

**前置：** P5.1 LLM 视觉打标、P5.2 TAG2NL、`OpenAiVisionClient`  
**权威源：**  
- `BooruDatasetTagManager/CharacterTagAudit.cs`  
- `BooruDatasetTagManager/CharacterTagFileTransaction.cs`  
- `BooruDatasetTagManager/CharacterTagReasonLocalizer.cs`  
- `BooruDatasetTagManager/Form_CharacterTagAuditWizard.cs`  
- `BooruDatasetTagManager.Tests/CharacterTagAudit*.cs`  
- `Agent/skills/character-tag-auditor`、`Agent/skills/prompt-pyramid`  

---

## 1. 目标与非目标

### 1.1 目标

在 Avalonia Linux 版实现与原版 **主路径对齐** 的角色标签审计向导：

1. **选择**：触发词、Sparse/Full、Review/SummaryApply、最小出现次数、审计模型、从当前数据集选 **1 张** 参考图  
2. **进度**：文本筛选 → 视觉复审（可取消）；可 **重跑视觉**  
3. **审阅**：表格编辑 keep/delete/replace/uncertain、分类保护、替换标签、include_in_prompt、最终 prompt、指标、排除列表  
4. **应用**：确认后事务写盘 + 更新内存数据集 + 刷新主界面  

### 1.2 已锁定决策

| 项 | 选择 |
|----|------|
| 范围档位 | 完整向导对齐（非仅 Core） |
| 交付切分 | 单 PR 全量 |
| 实现路径 | Core 整迁 + Avalonia 三页向导（方案 A） |
| 连接配置 | 共用 LLM Endpoint/Key/Timeout；独立审计模型名（可回落 VisionModel） |
| JSON | Core 用 `System.Text.Json`，不引入 Newtonsoft |
| UI 文案 | 中文硬编码（与当前 Avalonia 一致） |

### 1.3 非目标 / 可弱化

| 项 | 处理 |
|----|------|
| 五语言 i18n 资源文件 | 不做 |
| 原因机翻完整链路 | Core 可迁 `CharacterTagReasonLocalizer`；UI 首版显示英文/原文 reason |
| 预览布局像素级对齐 | 不要求 |
| AppImage / AUR | 不在本 PR |
| 改写 skill 业务规则 | 只搬文件，不改语义 |

---

## 2. 架构与模块边界

```
Bdtm.Avalonia
  菜单「工具 / 打标 → 角色标签审计…」
  CharacterTagAuditWizardWindow
  CharacterTagAuditWizardViewModel (+ Setup / ReviewRow)
        │
        ▼
Bdtm.Core
  CharacterTagInventory / Policy / AuditService
  CharacterTagFileTransaction
  CharacterTagSkillLoader (+ Agent/skills 文件)
  CharacterTag* 解析、变换、错误格式化
  OpenAiVisionClient（扩展：无图 + 有图 completion，无 ParseTags）
  AppSettings 审计相关字段
```

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `CharacterTagAuditService` | 文本筛选、视觉复审、进度、结果 DTO | UI、写盘 |
| `CharacterTagFileTransaction` | 事务写 txt + manifest | 业务决策 |
| SkillLoader | 读 SKILL.md | 网络 |
| OpenAi 委托 | system/user/(images) → 文本 | ParseTags |
| 向导 VM | 校验、编排、表格、应用确认 | 算法细节 |
| MainViewModel | 打开向导、应用后刷新 | 审计内部 |

---

## 3. Core 移植清单

### 3.1 文件

| 源 | 目标 |
|----|------|
| `CharacterTagAudit.cs` | `src/Bdtm.Core/CharacterTagAudit.cs`（可后续再拆文件，首版允许单文件） |
| `CharacterTagFileTransaction.cs` | `src/Bdtm.Core/CharacterTagFileTransaction.cs` |
| `CharacterTagReasonLocalizer.cs` | `src/Bdtm.Core/CharacterTagReasonLocalizer.cs` |
| `Agent/skills/**` | 仓库根 `Agent/skills/**`（若尚不在可发布树，从 WinForms 侧拷贝） |

命名空间：`Bdtm.Core`。去除 `System.Windows.Forms` / WinForms 专用类型引用。

### 3.2 行为必须保留（由测试锁定）

- 枚举：`CharacterTagAuditStyle`、`ExecutionMode`、`Decision`、`Stage`、`Category`  
- `CharacterTagAuditPolicy.CanDelete`：仅 hair/eyes/face/body/clothing/footwear/legwear/wearable_accessory 可删/换  
- Inventory 聚合与 `WhereMinimumCount`  
- Trigger 候选排序（次数降序）  
- `ExecuteAsync`：TextScreening → TextScreeningCompleted → VisualReview（及原版 Repair 若存在则保留）  
- `ExecuteVisualReviewAsync`：基于初审结果重跑视觉  
- 响应解析：keep/delete/replace/uncertain；replacement 规则；protected 类不可删换  
- Transformation / DeletionPlanner 生成最终标签序列  
- FileTransaction：staging、backup、manifest、原子替换；失败语义对齐测试  

### 3.3 JSON

- 事务 `manifest.json`：`System.Text.Json` 序列化，字段名与原版兼容（实现时对照原 Newtonsoft 输出与测试）  
- 模型 JSON 标签决策：字段名与 skill / 原解析器一致（`tag`、`decision`、`replacement_tag`、`category`、`include_in_prompt`、`prompt_order`、`reason` 等）  

### 3.4 技能路径

```
{AppContext.BaseDirectory}/Agent/skills/character-tag-auditor/SKILL.md
{AppContext.BaseDirectory}/Agent/skills/prompt-pyramid/SKILL.md
```

- Avalonia csproj：`CopyToOutputDirectory=PreserveNewest`  
- publish / dist 必须带上；缺失 → 明确错误（`FileNotFoundException` 风格）  

---

## 4. LLM 集成

### 4.1 请求

原版 `CharacterTagModelRequest`：

- `Stage`, `Model`, `SystemPrompt`, `UserPrompt`, `ImagePaths`

Linux：注入

```csharp
Func<CharacterTagModelRequest, CancellationToken, Task<CharacterTagModelResponse>>
```

实现：

1. 解析 `ImagePaths` → 读文件 bytes → data URL（0 张则纯文本 chat）  
2. `OpenAiVisionClient`（扩展）或共享 HTTP 层：`CompleteAsync` **不做** ParseTags  
3. 可选解析 `usage` → `CharacterTagTokenUsage`  

### 4.2 连接与模型

| 配置 | 来源 |
|------|------|
| Endpoint / ApiKey / Timeout / Temperature | `LlmSettings`（与 P5.1/P5.2 共用） |
| 审计模型 | `CharacterTagAuditModel`；空则 `Llm.VisionModel` |

### 4.3 与 TAG2NL / 视觉打标隔离

- 审计 **不** 使用 TAG2NL 系统提示词  
- 审计 **不** 使用视觉打标 UserPrompt / ParseTags  
- System 内容来自 skill + 服务内建 prompt 构造  

---

## 5. 设置

| 字段 | 默认 | 说明 |
|------|------|------|
| `CharacterTagAuditModel` | `""` | 空 → VisionModel |
| `CharacterTagAuditStyle` | Sparse | 向导记忆 |
| `CharacterTagAuditExecutionMode` | Review | 向导记忆 |
| `CharacterTagAuditMinimumCount` | 与原版默认一致（实现时读 WinForms `AppSettings`） | 向导记忆 |

设置页：LLM/TAG2NL 下增加「角色审计」— 模型名 + 说明（连接同上）。风格/次数以向导为主，成功跑完或应用前可写回。

---

## 6. UI 与交互

### 6.1 入口

- **工具 → 角色标签审计…**  
- **打标 → 角色标签审计…**（双入口，同命令）  
- 未加载数据集 → 提示返回  
- 未保存修改 → **自动保存** 后继续（与 TAG2NL 一致；状态栏可提示）  

### 6.2 三页向导

**页 0 选择**

- 触发词 Combo（可编辑 + 候选）  
- 风格 Sparse/Full  
- 模式 Review/SummaryApply  
- 最小次数  
- 审计模型  
- 参考图：数据集图库（缩略图优先，可降级列表）；**必选 1**  
- 下一步：校验 → 页 1 → `ExecuteAsync`  

**页 1 进度**

- 进度条与阶段文案  
- 取消 → CTS；取消后回页 0 或关闭并提示  

**页 2 审阅**

- 参考图预览  
- 结果表：Tag、Count、Category、Initial、Final、Replacement、IncludeInPrompt、Reason  
- 保护行不可改为 delete/replace  
- 筛选（仅变更 / 搜索）  
- 重跑视觉（可重选参考图 → `ExecuteVisualReviewAsync`）  
- 排除列表（低于最小次数）  
- 最终 prompt + 复制  
- 指标（时长 / token）  
- 应用：校验 → 影响文件数确认 → `CharacterTagFileTransaction.CommitAsync` → 内存更新 → 主窗刷新 → 关闭  

禁止用户点击 Tab 随意跳步；仅程序 `ShowPage`。

### 6.3 应用后刷新

1. 受影响 `DataItem`：替换 Tags、`AcceptCurrentTagsAsSaved`  
2. `ReloadCurrentTags` / `RebuildGlobalTags`  
3. `StatusText`：已保存 · 修改 n 个文件  
4. 不强制整库磁盘重载  

### 6.4 ViewModel 切分

- `CharacterTagAuditWizardViewModel` — 编排  
- `CharacterTagAuditSetupViewModel` — 选择页  
- `CharacterTagAuditReviewRow` — 表格行  
- 进度更新走 `Dispatcher.UIThread`  

---

## 7. 数据流（一次完整运行）

```
打开向导
  → 建 Inventory（当前数据集标签）
  → 用户选 trigger / 风格 / 参考图 / 模型
  → SkillLoader.Load(BaseDirectory)
  → service.ExecuteAsync(options, progress, ct)
       → TextScreening (LLM)
       → VisualReview (LLM + 参考图)
  → 填审阅表（用户可改 Final/Replacement）
  → [可选] ExecuteVisualReviewAsync
  → Apply: Transform 每图标签 → FileTransaction → 内存 Accept → 刷新 UI
```

---

## 8. 测试计划

### 8.1 Core（移植原版测试）

- Inventory / trigger candidates / policy  
- 解析合法/非法 JSON、protected 决策、replacement 规则  
- Transformation / deletion / canonicalizer  
- FileTransaction commit（临时目录）  
- Service 与 mock `Func<..., CharacterTagModelResponse>` 的阶段顺序（integration）  
- ReasonLocalizer（若迁入）  

### 8.2 回归

- 现有 Core 62 + Onnx 9 保持绿  
- OpenAiVisionTagger / CaptionGeneration 行为不变  

### 8.3 手工验收

1. `publish-linux.sh` + `run-linux.sh`（确认 dist 含 `Agent/skills`）  
2. 打开带角色标签的数据集 → 角色审计 → 选触发词与参考图 → 跑通  
3. 改决策 → 应用 → 磁盘 txt 与 UI 一致  
4. 取消 / 缺 skill / 无效模型 有清晰错误  
5. TAG2NL、LLM 当前图仍可用  

### 8.4 自动化门槛

`./scripts/build-linux.sh` 全绿。

---

## 9. 验收标准

- [ ] 菜单可打开三页向导  
- [ ] 文本筛选 + 视觉复审 + 可取消  
- [ ] 可重跑视觉  
- [ ] 分类保护生效  
- [ ] 事务应用写盘且可刷新主界面  
- [ ] Skill 随 publish 分发  
- [ ] Core 审计相关测试绿；P5.1/P5.2 无回归  
- [ ] 状态文档更新（`STATUS_*_post_p53.md`、roadmap）  

---

## 10. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 单 PR 体量大 | 任务序：Core+测 → Client 扩展 → 技能拷贝 → 向导 UI → 应用刷新；多 agent 分任务 |
| STJ 与 Newtonsoft 差异 | 以原版测试为契约；对照 manifest/响应样例 |
| MainViewModel 膨胀 | 向导独立窗口；主窗只 Open + Refresh |
| dist 缺 skill | csproj Copy + publish 后路径检查 |
| 用户跑旧 dist | 文档强调 publish-before-run（P5.2 已踩坑） |
| 原因翻译缺失 | 明确为可弱化；不阻塞主路径 |

---

## 11. 建议实现顺序（供 writing-plans）

1. 拷贝/对齐 `Agent/skills` 到可发布位置  
2. 迁 FileTransaction + 测试  
3. 迁 CharacterTagAudit 核心类型与 Service + 测试（mock LLM）  
4. 扩展 OpenAiVisionClient（无图/有图）+ 审计委托适配  
5. AppSettings 审计字段 + 设置页  
6. Avalonia 向导三页 + 表格 + 进度  
7. Apply + 主窗刷新  
8. build-linux + 手工冒烟 + 状态文档  

协作：Controller → Implementer → Spec QA → Quality QA（见 `docs/porting/PHASE4_P2.md`）。

---

## 12. 与路线图关系

- 完成 P5.3 后整体约 **82–85%**（估）  
- 下一批仍可：AppImage、PR 收尾、README Linux  
