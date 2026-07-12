# P5.3 Character Tag Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the WinForms character tag audit engine and full three-page wizard to Avalonia Linux so users can run text+visual LLM audit and transactionally apply keep/delete/replace decisions.

**Architecture:** Migrate `CharacterTagAudit*` + `CharacterTagFileTransaction` into `Bdtm.Core` (System.Text.Json instead of Newtonsoft). Extend `OpenAiVisionClient` for text-only and model override. Avalonia hosts a three-page wizard (setup → progress → review/apply) that injects an LLM delegate into `CharacterTagAuditService` and commits via file transaction + in-memory dataset refresh.

**Tech Stack:** .NET 8, xUnit, System.Text.Json, Avalonia 11 + CommunityToolkit.Mvvm, existing OpenAI-compatible HTTP client

**Spec:** `docs/porting/SPEC_2026-07-11-character-tag-audit-design.md`  
**Repo:** `/home/buxinzi/Projects/BooruDatasetTagManager-linuxPlus`  
**Branch:** `feature/avalonia-linux-mvp`  
**Nature:** PORT — prefer original behavior over redesign.

---

## Multi-agent collaboration

| Role | Duty |
|------|------|
| **Controller** | Dispatch one task at a time, TodoWrite, Spec+Quality review gates, push |
| **Implementer** | TDD when possible, minimal port, commit per task |
| **QA Spec** | Against spec §8/§9; read code, do not trust report alone |
| **QA Quality** | Thread safety, UI thread, no ParseTags on audit path, skills in publish output |

**Flow:** Implement → Spec QA → Quality QA → next task  
**Do not edit WinForms except as read-only reference.**  
**Always work from repo root above.**  
**Verify:** `dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo` after Core tasks; `./scripts/build-linux.sh` before final docs.

---

## File map

| Path | Action |
|------|--------|
| `src/Bdtm.Core/CharacterTagFileTransaction.cs` | Create (port, STJ) |
| `src/Bdtm.Core/CharacterTagAudit.cs` | Create (port large file, STJ) |
| `src/Bdtm.Core/CharacterTagReasonLocalizer.cs` | Create (port; UI may not call fully) |
| `src/Bdtm.Core/AppSettings.cs` | Modify — audit settings fields |
| `src/Bdtm.Core/OpenAiVisionClient.cs` | Modify — optional image, model override, optional usage |
| `tests/Bdtm.Core.Tests/CharacterTagAuditTests.cs` | Create — port unit tests |
| `tests/Bdtm.Core.Tests/CharacterTagFileTransactionTests.cs` | Create |
| `tests/Bdtm.Core.Tests/CharacterTagAuditSettingsAndSkillsTests.cs` | Create — skills exist + settings round-trip |
| `Agent/skills/**` | Already at repo root — ensure Avalonia copies them |
| `src/Bdtm.Avalonia/Bdtm.Avalonia.csproj` | Copy skills to output |
| `src/Bdtm.Avalonia/ViewModels/CharacterTagAudit*.cs` | Create wizard VMs |
| `src/Bdtm.Avalonia/Views/CharacterTagAuditWizardWindow.axaml(.cs)` | Create |
| `src/Bdtm.Avalonia/ViewModels/MainViewModel.cs` | Open wizard + refresh after apply |
| `src/Bdtm.Avalonia/Views/MainWindow.axaml` | Menu items |
| `src/Bdtm.Avalonia/ViewModels/SettingsViewModel.cs` + SettingsWindow | Audit model field |
| `docs/porting/STATUS_*_post_p53.md` | Status |

**Reference only:**
- `BooruDatasetTagManager/CharacterTagAudit.cs` (1179 lines)
- `BooruDatasetTagManager/CharacterTagFileTransaction.cs`
- `BooruDatasetTagManager/Form_CharacterTagAuditWizard.cs`
- `BooruDatasetTagManager.Tests/CharacterTagAuditTests.cs` (~40 facts)
- `BooruDatasetTagManager.Tests/CharacterTagAuditIntegrationTests.cs` (adapt: no WinForms csproj asserts)

---

### Task 1: AppSettings audit fields + skills copy wiring

**Owner:** Implementer  
**Files:**
- Modify: `src/Bdtm.Core/AppSettings.cs`
- Modify: `src/Bdtm.Avalonia/Bdtm.Avalonia.csproj`
- Test: `tests/Bdtm.Core.Tests/AppSettingsTests.cs` or new `CharacterTagAuditSettingsAndSkillsTests.cs`

- [ ] **Step 1: Add settings fields**

On `AppSettings` (top-level, matching WinForms, not nested under Llm unless cleaner — **use top-level like WinForms**):

```csharp
public string CharacterTagAuditModel { get; set; } = string.Empty;
// Style/Mode enums live in CharacterTagAudit.cs — add after types exist OR use string interim.
// For Task 1 only add model + minimum count as primitives if enums not yet ported:
public int CharacterTagAuditMinimumCount { get; set; } = 10;
```

**If enums not yet in Core**, Task 1 only adds:

```csharp
public string CharacterTagAuditModel { get; set; } = string.Empty;
public int CharacterTagAuditMinimumCount { get; set; } = 10;
// Style/Mode added in Task 3 when enums exist
```

Load normalization:

```csharp
loaded.CharacterTagAuditModel ??= string.Empty;
if (loaded.CharacterTagAuditMinimumCount <= 0)
    loaded.CharacterTagAuditMinimumCount = 10;
```

- [ ] **Step 2: Skills copy in Avalonia csproj**

```xml
  <ItemGroup>
    <Content Include="..\..\Agent\skills\**\*"
             Link="Agent\skills\%(RecursiveDir)%(Filename)%(Extension)"
             CopyToOutputDirectory="PreserveNewest"
             CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
```

(Adjust relative path from `src/Bdtm.Avalonia` to repo `Agent/skills` — verify with `ls ../../Agent/skills` from that folder = `src/Bdtm.Avalonia` → `../../Agent/skills`.)

- [ ] **Step 3: Tests**

```csharp
[Fact]
public void AgentSkillsExistInRepo()
{
    string root = /* walk up from AppContext.BaseDirectory or use known repo path via find */;
    // Prefer: locate by searching parents for Agent/skills/character-tag-auditor/SKILL.md
    Assert.True(File.Exists(Path.Combine(repoRoot, "Agent", "skills", "character-tag-auditor", "SKILL.md")));
    Assert.True(File.Exists(Path.Combine(repoRoot, "Agent", "skills", "prompt-pyramid", "SKILL.md")));
}

[Fact]
public void CharacterTagAuditModel_RoundTrip()
{
    var settings = AppSettings.Load(_tempRoot);
    settings.CharacterTagAuditModel = "gpt-4o";
    settings.CharacterTagAuditMinimumCount = 12;
    settings.Save();
    var reloaded = AppSettings.Load(_tempRoot);
    Assert.Equal("gpt-4o", reloaded.CharacterTagAuditModel);
    Assert.Equal(12, reloaded.CharacterTagAuditMinimumCount);
}
```

- [ ] **Step 4: Run tests + commit**

```bash
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo
git add src/Bdtm.Core/AppSettings.cs src/Bdtm.Avalonia/Bdtm.Avalonia.csproj tests/Bdtm.Core.Tests/*
git commit -m "feat(settings): character audit model fields and skill publish copy"
```

---

### Task 2: Port CharacterTagFileTransaction (STJ)

**Owner:** Implementer  
**Files:**
- Create: `src/Bdtm.Core/CharacterTagFileTransaction.cs`
- Create: `tests/Bdtm.Core.Tests/CharacterTagFileTransactionTests.cs`

- [ ] **Step 1: Write failing tests first**

```csharp
public class CharacterTagFileTransactionTests
{
    [Fact]
    public async Task CommitAsync_WritesNewContentAndCleansTxnDir()
    {
        using var temp = new TempDir();
        string root = temp.Path;
        string file = Path.Combine(root, "a.txt");
        File.WriteAllText(file, "old");
        await CharacterTagFileTransaction.CommitAsync(root, new[]
        {
            new CharacterTagFileChange(file, "new tags here")
        });
        Assert.Equal("new tags here", File.ReadAllText(file));
        Assert.Empty(Directory.GetDirectories(root, CharacterTagFileTransaction.DirectoryPrefix + "*"));
    }

    [Fact]
    public async Task CommitAsync_CreatesMissingFile()
    {
        using var temp = new TempDir();
        string root = temp.Path;
        string file = Path.Combine(root, "new.txt");
        await CharacterTagFileTransaction.CommitAsync(root, new[]
        {
            new CharacterTagFileChange(file, "created")
        });
        Assert.Equal("created", File.ReadAllText(file));
    }

    [Fact]
    public async Task CommitAsync_RejectsPathOutsideRoot()
    {
        using var temp = new TempDir();
        string outside = Path.Combine(Path.GetTempPath(), "bdtm-out-" + Guid.NewGuid().ToString("N") + ".txt");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterTagFileTransaction.CommitAsync(temp.Path, new[]
            {
                new CharacterTagFileChange(outside, "x")
            }));
    }
}
```

- [ ] **Step 2: Port implementation**

Copy from `BooruDatasetTagManager/CharacterTagFileTransaction.cs`:

- namespace `Bdtm.Core`
- Replace `JsonConvert.SerializeObject/DeserializeObject` with:

```csharp
private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

// serialize:
JsonSerializer.Serialize(manifest, JsonOpts)

// deserialize:
JsonSerializer.Deserialize<TransactionManifest>(json) ?? new TransactionManifest()
```

- Ensure `TransactionManifest` / `TransactionEntry` properties are public get/set for STJ  
- Keep `DirectoryPrefix`, staging, backup, recover logic identical  

- [ ] **Step 3: Tests pass + commit**

```bash
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --filter CharacterTagFileTransaction --nologo
git add src/Bdtm.Core/CharacterTagFileTransaction.cs tests/Bdtm.Core.Tests/CharacterTagFileTransactionTests.cs
git commit -m "feat(core): port CharacterTagFileTransaction with System.Text.Json"
```

---

### Task 3: Port CharacterTagAudit core + unit tests

**Owner:** Implementer (largest task — may take longer; do not skip tests)  
**Files:**
- Create: `src/Bdtm.Core/CharacterTagAudit.cs`
- Create: `tests/Bdtm.Core.Tests/CharacterTagAuditTests.cs`
- Optionally: `CharacterTagReasonLocalizer.cs`

- [ ] **Step 1: Port tests from WinForms**

Copy `BooruDatasetTagManager.Tests/CharacterTagAuditTests.cs` → `tests/Bdtm.Core.Tests/CharacterTagAuditTests.cs`:

- namespace `Bdtm.Core.Tests`
- `using Bdtm.Core`
- Remove `using Newtonsoft.Json` if tests only assert on domain types
- Keep all `[Fact]` methods that exercise Inventory, Parser, Policy, Transformation, Canonicalizer, PromptBuilder, Service with mock callbacks

- [ ] **Step 2: Port `CharacterTagAudit.cs`**

1. Copy file to Core, change namespace to `Bdtm.Core`
2. Replace Newtonsoft usages:

**Parser (`JObject.Parse`):** use `JsonDocument.Parse` / `JsonNode.Parse` and port field reads carefully. Keep validation rules identical — run tests often.

**Serialize inventory for prompts:**

```csharp
JsonSerializer.Serialize(auditedInventory.Tags)
// or anonymous objects as in original
```

3. Nullable enable: add `?` only as needed; do not change algorithms  
4. `CharacterTagSkillLoader.Load(applicationRoot)` remains path-based  

- [ ] **Step 3: Add Style/Mode to AppSettings** (now that enums exist)

```csharp
public CharacterTagAuditStyle CharacterTagAuditStyle { get; set; } = CharacterTagAuditStyle.Sparse;
public CharacterTagAuditExecutionMode CharacterTagAuditExecutionMode { get; set; } = CharacterTagAuditExecutionMode.Review;
```

Round-trip test for style/mode.

- [ ] **Step 4: Full Core tests green**

```bash
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo
```

Expected: previous ~62 + new audit/transaction tests all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Bdtm.Core/CharacterTagAudit.cs src/Bdtm.Core/AppSettings.cs tests/Bdtm.Core.Tests/CharacterTagAuditTests.cs
git commit -m "feat(core): port CharacterTagAudit service and unit tests"
```

- [ ] **Step 6: Spec + Quality QA (mandatory)**

Spec: policy categories, parser rules, ExecuteAsync stages with mock.  
Quality: no WinForms refs, STJ only, thread-safe enough for service.

---

### Task 4: Extend OpenAiVisionClient for audit

**Owner:** Implementer  
**Files:**
- Modify: `src/Bdtm.Core/OpenAiVisionClient.cs`
- Modify tests if needed

- [ ] **Step 1: Extend request**

```csharp
public sealed class OpenAiVisionCompletionRequest
{
    public string SystemPrompt { get; init; } = "";
    public string UserPrompt { get; init; } = "";
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "image/jpeg";
    /// <summary>When set, overrides Settings.VisionModel for this call.</summary>
    public string? Model { get; init; }
    /// <summary>When false, omit image_url part (text-only chat).</summary>
    public bool IncludeImage { get; init; } = true;
}

// Optional result extension:
public int? InputTokens { get; init; }
public int? OutputTokens { get; init; }
public int? TotalTokens { get; init; }
```

- [ ] **Step 2: BuildRequestJson**

- Use `request.Model` if non-empty else `Settings.VisionModel`  
- If `!IncludeImage` or `ImageData` empty **and** audit text stage: **do not** add image_url content part (user content = plain string or text-only array)  
- Preserve existing tagging path: `OpenAiVisionTagger` still passes image with IncludeImage true  

- [ ] **Step 3: Parse usage** (optional)

If `usage` object present in response JSON, fill token fields.

- [ ] **Step 4: Helper for audit adapter** (can live in Avalonia or Core)

```csharp
// Core helper recommended:
public static class CharacterTagOpenAiAdapter
{
    public static async Task<CharacterTagModelResponse> SendAsync(
        OpenAiVisionClient client,
        CharacterTagModelRequest request,
        CancellationToken ct)
    {
        byte[] image = Array.Empty<byte>();
        string mime = "image/jpeg";
        bool includeImage = false;
        if (request.ImagePaths.Count > 0 && File.Exists(request.ImagePaths[0]))
        {
            image = await File.ReadAllBytesAsync(request.ImagePaths[0], ct);
            includeImage = true;
            mime = GuessMime(request.ImagePaths[0]);
        }
        var result = await client.CompleteAsync(new OpenAiVisionCompletionRequest
        {
            SystemPrompt = request.SystemPrompt,
            UserPrompt = request.UserPrompt,
            ImageData = image,
            ContentType = mime,
            Model = request.Model,
            IncludeImage = includeImage,
        }, ct);
        if (!result.Success)
            return new CharacterTagModelResponse(string.Empty, result.ErrorMessage ?? "error", null);
        CharacterTagTokenUsage? usage = result.TotalTokens is int t
            ? new CharacterTagTokenUsage(result.InputTokens ?? 0, result.OutputTokens ?? 0, t)
            : null;
        return new CharacterTagModelResponse(result.Text, string.Empty, usage);
    }
}
```

- [ ] **Step 5: Ensure OpenAiVisionTagger still green + commit**

```bash
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo
git commit -m "feat(llm): extend OpenAiVisionClient for text-only audit calls"
```

---

### Task 5: Settings UI for audit model

**Owner:** Implementer  
**Files:** SettingsViewModel + SettingsWindow.axaml

- [ ] Add `CharacterTagAuditModel` bindable property; load/save  
- [ ] UI section under TAG2NL:

```xml
<Separator />
<TextBlock FontWeight="SemiBold" FontSize="15" Text="角色标签审计" />
<TextBlock Opacity="0.75" TextWrapping="Wrap"
           Text="连接与 Endpoint 同上。模型可留空则使用 Vision 模型。" />
<TextBlock Opacity="0.8" Text="审计模型" />
<TextBox Text="{Binding CharacterTagAuditModel}" />
```

- [ ] Build Avalonia + commit

```bash
dotnet build src/Bdtm.Avalonia/Bdtm.Avalonia.csproj -c Release --nologo
git commit -m "feat(ui): settings field for character tag audit model"
```

---

### Task 6: Wizard window — setup + progress + run

**Owner:** Implementer  
**Files:**
- Create: `CharacterTagAuditWizardViewModel.cs`, `CharacterTagAuditSetupViewModel.cs` (or single VM if clearer)
- Create: `CharacterTagAuditWizardWindow.axaml` + code-behind
- Modify: MainWindow menu + MainViewModel open command

**UI structure (Avalonia):**

- Window 1100x760  
- `Carousel` or three `IsVisible` panels controlled by `PageIndex` (0/1/2) — **do not** allow free Tab hopping  
- Page 0: trigger ComboBox, style ComboBox, mode ComboBox, min count NumericUpDown, model TextBox, image ListBox/ListBox of dataset images  
- Page 1: ProgressBar, StatusText, Cancel  
- Buttons: Cancel, Next (Next on page0 starts audit; page2 becomes Apply in Task 7)

**Run flow in VM:**

```csharp
async Task NextFromSetupAsync()
{
    // validate dataset, trigger, selected image, model (fallback VisionModel)
    PageIndex = 1;
    var skills = CharacterTagSkillLoader.Load(AppContext.BaseDirectory);
    var inventory = CharacterTagInventory.Create(dataset tag lists);
    var options = new CharacterTagAuditOptions { ... };
    using var client = new OpenAiVisionClient(settings.Llm);
    // Temporarily set VisionModel if audit model set — or pass Model on each request via adapter
    var service = new CharacterTagAuditService((req, ct) => CharacterTagOpenAiAdapter.SendAsync(client, req, ct));
    var progress = new Progress<CharacterTagAuditProgress>(p => Dispatcher.UIThread.Post(() => ApplyProgress(p)));
    Result = await service.ExecuteAsync(options, progress, cts.Token);
    BuildReviewRows();
    PageIndex = 2;
}
```

**MainViewModel:**

```csharp
[RelayCommand]
private async Task OpenCharacterTagAuditAsync()
{
    if (string.IsNullOrWhiteSpace(_dataset.DatasetRoot)) { StatusText = "请先打开数据集…"; return; }
    ApplyCurrentTagsToModel();
    if (DatasetHasUnsavedChanges()) _dataset.SaveAll();
    var wnd = new CharacterTagAuditWizardWindow
    {
        DataContext = new CharacterTagAuditWizardViewModel(_dataset, _settings, /* refresh callback */)
    };
    await wnd.ShowDialog(GetMainWindow());
    // refresh if DialogResult OK — Task 7
}
```

Menu:

```xml
<MenuItem Header="角色标签审计…" Command="{Binding OpenCharacterTagAuditCommand}" />
```

under 工具 and 打标.

- [ ] Build + commit

```bash
git commit -m "feat(ui): character tag audit wizard setup and progress"
```

---

### Task 7: Review grid + apply + refresh

**Owner:** Implementer  
**Files:** same wizard + MainViewModel refresh

- [ ] **Review grid** (`DataGrid` with Avalonia.Controls.DataGrid already referenced)

Columns: Tag, Count, Category, Initial, Final (ComboBox template), Replacement, IncludeInPrompt, Reason  

Binding to `ObservableCollection<CharacterTagAuditReviewRow>`.

Protection:

```csharp
public bool CanModify => CharacterTagAuditPolicy.CanDelete(Category);
// when !CanModify, Final forced Keep, Replacement empty
```

- [ ] **Redo visual** button → `ExecuteVisualReviewAsync` with optional new image path  
- [ ] **Final prompt** text + copy  
- [ ] **Excluded** list from result.ExcludedItems  
- [ ] **Apply:**

```csharp
// map rows → List<CharacterTagAuditItem>
// validate replacements
// for each DataItem in dataset: transform tags via CharacterTagTransformation / same as Form wizard TransformEditableTags
// build CharacterTagFileChange list
// confirm counts
// await CharacterTagFileTransaction.CommitAsync(root, changes, progress)
// update in-memory tags + AcceptCurrentTagsAsSaved(separator)
// callback main window RebuildGlobalTags/ReloadCurrentTags
// Close(true)
```

Port transformation logic from `Form_CharacterTagAuditWizard.TransformEditableTags` / Core `CharacterTagTransformation` — **prefer Core APIs** already ported; if WinForms used EditableTag, map to `Bdtm.Core` tag list (`TagList` / string lists in DataItem).

**Read** `DatasetManager.DataItem` and `TagList` APIs before applying — use existing `Tags` model:

```csharp
// Typical approach:
var original = item.Tags.ToTextTagsOrEquivalent();
var transformed = CharacterTagTransformation.Apply(original, decisions); // use actual Core API names from port
item.Tags.Clear();
foreach (var t in transformed) item.Tags.Add(t);
item.AcceptCurrentTagsAsSaved(separator);
```

If Core transformation works on `IEnumerable<string>`, format file with `SeparatorOnSave`.

- [ ] **Commit**

```bash
git commit -m "feat(ui): character tag audit review grid and transactional apply"
```

---

### Task 8: Full verify + docs + publish check

**Owner:** Implementer / Controller  

- [ ] `./scripts/build-linux.sh`  
- [ ] `./scripts/publish-linux.sh`  
- [ ] Verify:

```bash
test -f dist/linux-x64/Agent/skills/character-tag-auditor/SKILL.md
test -f dist/linux-x64/Agent/skills/prompt-pyramid/SKILL.md
```

- [ ] Status doc `docs/porting/STATUS_2026-07-11_post_p53.md`  
- [ ] Update `ROADMAP_REMAINING.md` / `MVP_STATUS.md`  
- [ ] Commit + push

```bash
git commit -m "docs(porting): P5.3 character tag audit status"
git push origin feature/avalonia-linux-mvp
```

---

## Spec coverage matrix

| Spec requirement | Task |
|------------------|------|
| Core audit engine port | 3 |
| File transaction | 2 |
| Skills load + publish | 1, 8 |
| STJ not Newtonsoft | 2, 3 |
| OpenAI text+image without ParseTags | 4 |
| Settings model | 1, 5 |
| Three-page wizard | 6, 7 |
| Redo visual | 7 |
| Transactional apply + UI refresh | 7 |
| Dual menu entry | 6 |
| Auto-save dirty before open | 6 |
| Tests + build-linux | 3, 8 |
| Reason machine translation optional | skip UI / optional Task 3 localizer only |

---

## Self-review (plan author)

1. Spec coverage mapped above.  
2. No intentional TBD steps; implementers must read WinForms for exact Transform API names when porting.  
3. Types: `CharacterTag*` names match WinForms/Core port.  
4. OpenAiVisionClient `IncludeImage` + `Model` required before Task 6.  
5. Risk: Task 3 is large — if blocked, split into 3a Inventory/Policy/Parser tests+impl, 3b Service+Prompt, 3c Canonicalizer/Transformation — Controller may re-dispatch.

---

## Execution

User preference: **Subagent-Driven** multi-agent.  
Plan path: `docs/porting/PLAN_2026-07-11-character-tag-audit.md` (tracked; `docs/superpowers/` gitignored).
