using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class CharacterTagAuditWizardViewModel : ViewModelBase
{
    private readonly DatasetManager _dataset;
    private readonly AppSettings _settings;
    private readonly Action? _onApplied;
    private readonly Func<string, string, Task<bool>>? _confirmAsync;
    private readonly Action<string>? _alert;
    private CancellationTokenSource? _cts;
    private CharacterTagAuditResult? _auditResult;
    private List<CharacterTagAuditItem> _initialAuditItems = new();
    private List<CharacterTagAuditReviewRow> _allReviewRows = new();
    private string _selectedImagePath = string.Empty;
    private string _triggerWordUsed = string.Empty;
    private CharacterTagAuditStyle _styleUsed = CharacterTagAuditStyle.Sparse;
    private CharacterTagAuditExecutionMode _modeUsed = CharacterTagAuditExecutionMode.Review;

    public CharacterTagAuditWizardViewModel(
        DatasetManager dataset,
        AppSettings settings,
        Action? onApplied = null,
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action<string>? alert = null)
    {
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _onApplied = onApplied;
        _confirmAsync = confirmAsync;
        _alert = alert;

        Images = new ObservableCollection<CharacterTagAuditImageItem>(
            dataset.DataSet.Values
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(i => new CharacterTagAuditImageItem(i)));

        Inventory = CharacterTagInventory.Create(
            dataset.DataSet.Values.Select(i => i.Tags.Items.Select(t => t.Tag)));

        TriggerCandidates = new ObservableCollection<CharacterTagTriggerCandidate>(
            CharacterTagTriggerCandidates.Create(Inventory));

        StyleChoices = new ObservableCollection<NamedChoice<CharacterTagAuditStyle>>
        {
            new(CharacterTagAuditStyle.Sparse, "精简 (Sparse)"),
            new(CharacterTagAuditStyle.Full, "完整 (Full)"),
        };
        ModeChoices = new ObservableCollection<NamedChoice<CharacterTagAuditExecutionMode>>
        {
            new(CharacterTagAuditExecutionMode.Review, "审阅后应用"),
            new(CharacterTagAuditExecutionMode.SummaryApply, "摘要后应用"),
        };
        DecisionChoices = new ObservableCollection<NamedChoice<CharacterTagDecision>>
        {
            new(CharacterTagDecision.Keep, "保留"),
            new(CharacterTagDecision.Delete, "删除"),
            new(CharacterTagDecision.Replace, "替换"),
            new(CharacterTagDecision.Uncertain, "不确定"),
        };

        SelectedStyle = StyleChoices.FirstOrDefault(c => c.Value == settings.CharacterTagAuditStyle)
            ?? StyleChoices[0];
        SelectedMode = ModeChoices.FirstOrDefault(c => c.Value == settings.CharacterTagAuditExecutionMode)
            ?? ModeChoices[0];
        MinimumCount = settings.CharacterTagAuditMinimumCount <= 0 ? 10 : settings.CharacterTagAuditMinimumCount;
        AuditModel = settings.CharacterTagAuditModel ?? string.Empty;

        if (TriggerCandidates.Count > 0)
        {
            SelectedTriggerCandidate = TriggerCandidates[0];
            TriggerText = TriggerCandidates[0].Tag;
        }

        ReviewRows = new ObservableCollection<CharacterTagAuditReviewRow>();
        ExcludedItems = new ObservableCollection<CharacterTagInventoryItem>();
        UpdatePageFlags();
    }

    public CharacterTagInventory Inventory { get; }
    public ObservableCollection<CharacterTagAuditImageItem> Images { get; }
    public ObservableCollection<CharacterTagTriggerCandidate> TriggerCandidates { get; }
    public ObservableCollection<NamedChoice<CharacterTagAuditStyle>> StyleChoices { get; }
    public ObservableCollection<NamedChoice<CharacterTagAuditExecutionMode>> ModeChoices { get; }
    public ObservableCollection<NamedChoice<CharacterTagDecision>> DecisionChoices { get; }
    public ObservableCollection<CharacterTagAuditReviewRow> ReviewRows { get; }
    public ObservableCollection<CharacterTagInventoryItem> ExcludedItems { get; }

    [ObservableProperty] private int pageIndex;
    [ObservableProperty] private bool isSetupPage = true;
    [ObservableProperty] private bool isProgressPage;
    [ObservableProperty] private bool isReviewPage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canGoNext = true;
    [ObservableProperty] private string nextButtonText = "下一步";
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string progressText = string.Empty;
    [ObservableProperty] private double progressValue;
    [ObservableProperty] private double progressMaximum = 2;

    [ObservableProperty] private string triggerText = string.Empty;
    [ObservableProperty] private CharacterTagTriggerCandidate? selectedTriggerCandidate;
    [ObservableProperty] private NamedChoice<CharacterTagAuditStyle>? selectedStyle;
    [ObservableProperty] private NamedChoice<CharacterTagAuditExecutionMode>? selectedMode;
    [ObservableProperty] private int minimumCount = 10;
    [ObservableProperty] private string auditModel = string.Empty;
    [ObservableProperty] private CharacterTagAuditImageItem? selectedImage;

    [ObservableProperty] private string reviewSearch = string.Empty;
    [ObservableProperty] private bool showDeleteReplaceOnly;
    [ObservableProperty] private string summaryText = string.Empty;
    [ObservableProperty] private string finalPrompt = string.Empty;
    [ObservableProperty] private string metricsText = string.Empty;
    [ObservableProperty] private string excludedHeader = "排除项 (0)";
    [ObservableProperty] private bool showExcluded;
    [ObservableProperty] private bool canRedoVisual;

    public bool Applied { get; private set; }
    public event Action? RequestClose;

    partial void OnPageIndexChanged(int value) => UpdatePageFlags();

    partial void OnSelectedTriggerCandidateChanged(CharacterTagTriggerCandidate? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(TriggerText))
            TriggerText = value.Tag;
        else if (value is not null)
            TriggerText = value.Tag;
    }

    partial void OnReviewSearchChanged(string value) => RefreshReviewGrid();

    partial void OnShowDeleteReplaceOnlyChanged(bool value) => RefreshReviewGrid();

    private void UpdatePageFlags()
    {
        IsSetupPage = PageIndex == 0;
        IsProgressPage = PageIndex == 1;
        IsReviewPage = PageIndex == 2;
        CanGoNext = PageIndex != 1 && !IsBusy;
        NextButtonText = PageIndex == 2 ? "应用" : "下一步";
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (IsBusy) return;
        if (PageIndex == 0)
        {
            if (!ValidateSetup(out string error))
            {
                Notify(error);
                return;
            }
            await RunAuditAsync();
        }
        else if (PageIndex == 2)
        {
            await ApplyAsync();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch { /* ignore */ }
            return;
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void CopyPrompt()
    {
        // Clipboard is set by the view code-behind when available.
        StatusMessage = string.IsNullOrWhiteSpace(FinalPrompt) ? "最终提示词为空。" : "已请求复制最终提示词。";
    }

    [RelayCommand]
    private void ToggleExcluded()
    {
        ShowExcluded = !ShowExcluded;
    }

    [RelayCommand]
    private async Task RedoVisualAsync()
    {
        if (IsBusy || _initialAuditItems.Count == 0)
            return;
        if (SelectedImage is null)
        {
            Notify("请选择一张参考图后重做视觉审阅。");
            return;
        }

        _selectedImagePath = SelectedImage.Path;
        await RunVisualReviewOnlyAsync();
    }

    private bool ValidateSetup(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(_dataset.DatasetRoot))
        {
            error = "请先打开数据集文件夹。";
            return false;
        }

        string model = ResolveModel();
        if (string.IsNullOrWhiteSpace(model) || !HasValidLlmEndpoint(_settings.Llm))
        {
            error = "请先在设置中配置有效的 LLM Endpoint，并填写审计模型或 Vision 模型。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TriggerText))
        {
            error = "请填写或选择触发词。";
            return false;
        }

        if (SelectedImage is null || string.IsNullOrWhiteSpace(SelectedImage.Path) || !File.Exists(SelectedImage.Path))
        {
            error = "请选择一张参考图片。";
            return false;
        }

        if (MinimumCount < 1)
        {
            error = "最小出现次数至少为 1。";
            return false;
        }

        if (Inventory.Tags.Count == 0)
        {
            error = "当前数据集没有可审计的标签。";
            return false;
        }

        return true;
    }

    private static bool HasValidLlmEndpoint(LlmSettings llm)
    {
        if (!Uri.TryCreate((llm.Endpoint ?? string.Empty).Trim(), UriKind.Absolute, out var endpoint))
            return false;
        return endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps;
    }

    private string ResolveModel()
    {
        if (!string.IsNullOrWhiteSpace(AuditModel))
            return AuditModel.Trim();
        if (!string.IsNullOrWhiteSpace(_settings.CharacterTagAuditModel))
            return _settings.CharacterTagAuditModel.Trim();
        return (_settings.Llm.VisionModel ?? string.Empty).Trim();
    }

    private CharacterTagAuditOptions BuildOptions(string referenceImagePath)
    {
        CharacterTagSkillBundle skills = CharacterTagSkillLoader.Load(AppContext.BaseDirectory);
        return new CharacterTagAuditOptions
        {
            Inventory = Inventory,
            TriggerWord = TriggerText.Trim(),
            Style = SelectedStyle?.Value ?? CharacterTagAuditStyle.Sparse,
            MinimumCount = Math.Max(1, MinimumCount),
            Model = ResolveModel(),
            ReferenceImagePath = referenceImagePath,
            CharacterAuditorSkill = skills.CharacterAuditor,
            PromptPyramidSkill = skills.PromptPyramid,
        };
    }

    private async Task RunAuditAsync()
    {
        _selectedImagePath = SelectedImage!.Path;
        _triggerWordUsed = TriggerText.Trim();
        _styleUsed = SelectedStyle?.Value ?? CharacterTagAuditStyle.Sparse;
        _modeUsed = SelectedMode?.Value ?? CharacterTagAuditExecutionMode.Review;

        ResetAuditSession();
        PageIndex = 1;
        ProgressMaximum = 2;
        ProgressValue = 0;
        ProgressText = "准备审计…";
        IsBusy = true;
        UpdatePageFlags();

        _cts = new CancellationTokenSource();
        try
        {
            CharacterTagAuditOptions options = BuildOptions(_selectedImagePath);
            using var client = new OpenAiVisionClient(_settings.Llm);
            var service = new CharacterTagAuditService((req, ct) => CharacterTagOpenAiAdapter.SendAsync(client, req, ct));
            var progress = new Progress<CharacterTagAuditProgress>(p =>
            {
                Dispatcher.UIThread.Post(() => ApplyProgress(p));
            });

            _auditResult = await service.ExecuteAsync(options, progress, _cts.Token).ConfigureAwait(true);

            PersistWizardSettings();
            PrepareResults();
            PageIndex = 2;
        }
        catch (OperationCanceledException)
        {
            Notify("角色标签审计已取消。");
            ResetAuditSession();
            PageIndex = 0;
        }
        catch (FileNotFoundException ex)
        {
            Notify("缺少技能文件: " + ex.Message);
            PageIndex = 0;
        }
        catch (Exception ex)
        {
            Notify(FormatAuditError(ex));
            PageIndex = 0;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            UpdatePageFlags();
        }
    }

    private async Task RunVisualReviewOnlyAsync()
    {
        PageIndex = 1;
        ProgressMaximum = 1;
        ProgressValue = 0;
        ProgressText = "视觉复审中…";
        IsBusy = true;
        UpdatePageFlags();
        _cts = new CancellationTokenSource();
        try
        {
            CharacterTagAuditOptions options = BuildOptions(_selectedImagePath);
            options.TriggerWord = _triggerWordUsed;
            options.Style = _styleUsed;
            using var client = new OpenAiVisionClient(_settings.Llm);
            var service = new CharacterTagAuditService((req, ct) => CharacterTagOpenAiAdapter.SendAsync(client, req, ct));
            var progress = new Progress<CharacterTagAuditProgress>(p =>
            {
                Dispatcher.UIThread.Post(() => ApplyProgress(p));
            });
            _auditResult = await service.ExecuteVisualReviewAsync(
                options, _initialAuditItems, progress, _cts.Token).ConfigureAwait(true);
            PrepareResults();
        }
        catch (OperationCanceledException)
        {
            Notify("视觉复审已取消。");
        }
        catch (Exception ex)
        {
            Notify(FormatAuditError(ex));
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            PageIndex = 2;
            UpdatePageFlags();
        }
    }

    private void PersistWizardSettings()
    {
        try
        {
            _settings.CharacterTagAuditStyle = _styleUsed;
            _settings.CharacterTagAuditExecutionMode = _modeUsed;
            _settings.CharacterTagAuditMinimumCount = Math.Max(1, MinimumCount);
            if (!string.IsNullOrWhiteSpace(AuditModel))
                _settings.CharacterTagAuditModel = AuditModel.Trim();
            _settings.Save();
        }
        catch
        {
            // Non-fatal: audit still usable without persisting prefs.
        }
    }

    private void ApplyProgress(CharacterTagAuditProgress update)
    {
        int total = Math.Max(1, update.TotalSteps);
        int completed = Math.Clamp(update.CompletedSteps, 0, total);
        ProgressMaximum = total;
        ProgressValue = completed;

        string stageText = update.Stage switch
        {
            CharacterTagAuditStage.TextScreening => "文本筛选",
            CharacterTagAuditStage.TextScreeningCompleted => "文本筛选",
            CharacterTagAuditStage.VisualReview => "视觉审阅",
            CharacterTagAuditStage.Repair => "修复响应",
            _ => string.Empty,
        };

        int displayStep = update.Stage switch
        {
            CharacterTagAuditStage.TextScreening => 1,
            CharacterTagAuditStage.TextScreeningCompleted => Math.Min(total, 2),
            CharacterTagAuditStage.VisualReview when completed >= total => total,
            CharacterTagAuditStage.VisualReview => Math.Min(total, completed + 1),
            _ => Math.Min(total, Math.Max(1, completed)),
        };

        ProgressText = string.IsNullOrEmpty(stageText)
            ? string.Empty
            : $"步骤 {displayStep}/{total}：{stageText}";

        if (update.Stage == CharacterTagAuditStage.TextScreeningCompleted && update.Items is not null)
            _initialAuditItems = update.Items.ToList();
    }

    private void ResetAuditSession()
    {
        _auditResult = null;
        _initialAuditItems = new List<CharacterTagAuditItem>();
        _allReviewRows = new List<CharacterTagAuditReviewRow>();
        ReviewRows.Clear();
        ExcludedItems.Clear();
        SummaryText = string.Empty;
        FinalPrompt = string.Empty;
        MetricsText = string.Empty;
        ExcludedHeader = "排除项 (0)";
        CanRedoVisual = false;
    }

    private void PrepareResults()
    {
        if (_auditResult is null)
            return;

        if (_initialAuditItems.Count == 0)
            _initialAuditItems = _auditResult.Items.Select(CloneItem).ToList();

        _allReviewRows = _auditResult.Items
            .Select(item => CharacterTagAuditReviewRow.FromItem(item, DecisionChoices))
            .ToList();

        foreach (var row in _allReviewRows)
            row.Changed += OnReviewRowChanged;

        ExcludedItems.Clear();
        foreach (var ex in _auditResult.ExcludedItems)
            ExcludedItems.Add(ex);
        ExcludedHeader = $"排除项 ({ExcludedItems.Count})";

        ShowDeleteReplaceOnly = _modeUsed == CharacterTagAuditExecutionMode.SummaryApply;
        CanRedoVisual = _initialAuditItems.Count > 0;
        UpdateMetrics();
        RefreshReviewGrid();
    }

    private void OnReviewRowChanged()
    {
        RebuildSummaryAndPrompt();
    }

    private void RefreshReviewGrid()
    {
        IEnumerable<CharacterTagAuditReviewRow> rows = _allReviewRows;
        string search = (ReviewSearch ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(search))
            rows = rows.Where(r => r.Tag.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (ShowDeleteReplaceOnly)
            rows = rows.Where(r => r.Decision == CharacterTagDecision.Delete || r.Decision == CharacterTagDecision.Replace);

        ReviewRows.Clear();
        foreach (var row in rows)
            ReviewRows.Add(row);
        RebuildSummaryAndPrompt();
    }

    private void RebuildSummaryAndPrompt()
    {
        if (_auditResult is null)
            return;

        int deleteCount = _allReviewRows.Count(r => r.CanModify && r.Decision == CharacterTagDecision.Delete);
        int replaceCount = _allReviewRows.Count(r => r.CanModify && r.Decision == CharacterTagDecision.Replace);
        int keepCount = _allReviewRows.Count - deleteCount - replaceCount;
        int affected = CountAffectedFiles(
            _allReviewRows
                .Where(r => r.CanModify && (r.Decision == CharacterTagDecision.Delete || r.Decision == CharacterTagDecision.Replace))
                .Select(r => r.Tag));

        string styleLabel = _styleUsed == CharacterTagAuditStyle.Sparse ? "精简" : "完整";
        SummaryText =
            $"风格 {styleLabel} · 审计 {_allReviewRows.Count} · 保留 {keepCount} · 删除 {deleteCount} · 替换 {replaceCount} · 排除 {_auditResult.ExcludedItems.Count} · 影响文件约 {affected}";

        FinalPrompt = CharacterTagPromptBuilder.Build(BuildCanonicalizedAuditItems(), _triggerWordUsed);
    }

    private List<CharacterTagAuditItem> BuildCanonicalizedAuditItems()
    {
        List<CharacterTagAuditItem> items = _allReviewRows.Select(r => r.ToAuditItem()).ToList();
        CharacterTagResultCanonicalizer.Apply(items, _styleUsed);
        return items;
    }

    private void UpdateMetrics()
    {
        CharacterTagAuditMetrics? metrics = _auditResult?.Metrics;
        if (metrics is null)
        {
            MetricsText = string.Empty;
            return;
        }

        string usage = metrics.HasTokenUsage
            ? $"tokens in/out/total = {metrics.InputTokens}/{metrics.OutputTokens}/{metrics.TotalTokens}"
            : "token 用量不可用";
        MetricsText = $"耗时 {metrics.TotalDuration.TotalSeconds:0.0}s · {usage}";
    }

    private int CountAffectedFiles(IEnumerable<string> tags)
    {
        var set = new HashSet<string>(tags, StringComparer.Ordinal);
        return _dataset.DataSet.Values.Count(item => item.Tags.Items.Any(t => set.Contains(t.Tag)));
    }

    private async Task ApplyAsync()
    {
        List<CharacterTagAuditItem> decisions = _allReviewRows.Select(r => r.ToAuditItem()).ToList();
        string validationError = ValidateEditedDecisions(decisions);
        if (!string.IsNullOrEmpty(validationError))
        {
            Notify(validationError);
            return;
        }

        IsBusy = true;
        CanGoNext = false;
        StatusMessage = "正在准备变更…";
        try
        {
            List<(DatasetManager.DataItem Item, IReadOnlyList<TagItem> Tags)> affected = await Task.Run(() =>
                _dataset.DataSet.Values
                    .Select(item => (Item: item, Tags: TransformTags(item.Tags, decisions)))
                    .Where(change => !change.Item.Tags.Items.Select(t => t.Tag)
                        .SequenceEqual(change.Tags.Select(t => t.Tag), StringComparer.Ordinal))
                    .ToList()).ConfigureAwait(true);

            int changeCount = decisions.Count(item => item.ShouldDelete || item.ShouldReplace);
            if (affected.Count == 0)
            {
                Notify("没有需要应用的删除/替换变更。");
                return;
            }

            string confirm = $"将应用 {changeCount} 条决策，影响 {affected.Count} 个标签文件。是否继续？";
            if (_confirmAsync is not null)
            {
                bool ok = await _confirmAsync("确认应用", confirm).ConfigureAwait(true);
                if (!ok) return;
            }

            string separator = PromptParser.UnescapeSeparator(_settings.SeparatorOnSave);
            List<CharacterTagFileChange> changes = new();
            foreach (var change in affected)
            {
                string textPath = change.Item.TextFilePath;
                if (string.IsNullOrEmpty(textPath))
                {
                    textPath = Path.Combine(
                        Path.GetDirectoryName(change.Item.ImageFilePath) ?? _dataset.DatasetRoot,
                        change.Item.Name + "." + _dataset.Options.DefaultTagsFileExtension);
                    change.Item.TextFilePath = textPath;
                }

                string content = string.Join(separator, change.Tags.Select(t => t.ToString()));
                changes.Add(new CharacterTagFileChange(textPath, content));
            }

            int total = changes.Count;
            StatusMessage = $"保存中 0/{total}…";
            var progress = new Progress<int>(completed =>
            {
                Dispatcher.UIThread.Post(() => StatusMessage = $"保存中 {completed}/{total}…");
            });

            await CharacterTagFileTransaction.CommitAsync(
                _dataset.DatasetRoot,
                changes,
                progress: progress).ConfigureAwait(true);

            StatusMessage = "正在更新内存中的标签…";
            string sep = _settings.SeparatorOnSave;
            foreach (var (item, tags) in affected)
            {
                item.Tags.SetTags(tags.Select(t => (t.Tag, t.Weight)));
                item.AcceptCurrentTagsAsSaved(sep);
            }
            _dataset.UpdateDatasetHash();
            _onApplied?.Invoke();

            Applied = true;
            StatusMessage = "角色标签审计已应用。";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Notify("保存失败: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
            UpdatePageFlags();
        }
    }

    private static IReadOnlyList<TagItem> TransformTags(TagList originalTags, IEnumerable<CharacterTagAuditItem> decisions)
    {
        var byTag = decisions.ToDictionary(item => item.Tag, StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TagItem>();
        foreach (TagItem original in originalTags.Items)
        {
            if (byTag.TryGetValue(original.Tag, out CharacterTagAuditItem? decision) && decision.ShouldDelete)
                continue;
            string effective = byTag.TryGetValue(original.Tag, out decision) && decision.ShouldReplace
                ? decision.ReplacementTag
                : original.Tag;
            if (string.IsNullOrWhiteSpace(effective) || !emitted.Add(effective))
                continue;
            result.Add(new TagItem(effective, original.Weight));
        }
        return result;
    }

    private static string ValidateEditedDecisions(IReadOnlyList<CharacterTagAuditItem> decisions)
    {
        foreach (CharacterTagAuditItem item in decisions)
        {
            if ((item.FinalDecision == CharacterTagDecision.Delete || item.FinalDecision == CharacterTagDecision.Replace)
                && !item.CanDelete)
                return $"受保护类别不可删除/替换: {item.Tag}";
            if (item.FinalDecision == CharacterTagDecision.Replace
                && !CharacterTagAuditPolicy.IsValidReplacement(item.Tag, item.ReplacementTag))
                return $"无效替换: {item.Tag}";
        }

        var replacementSources = new HashSet<string>(
            decisions.Where(item => item.FinalDecision == CharacterTagDecision.Replace).Select(item => item.Tag),
            StringComparer.Ordinal);
        if (decisions.Any(item => item.FinalDecision == CharacterTagDecision.Replace
            && replacementSources.Contains(item.ReplacementTag)))
            return "检测到替换链/环，请修正替换目标。";
        return string.Empty;
    }

    private static CharacterTagAuditItem CloneItem(CharacterTagAuditItem item) => new()
    {
        Tag = item.Tag,
        Count = item.Count,
        InitialDecision = item.InitialDecision,
        FinalDecision = item.FinalDecision,
        Category = item.Category,
        Reason = item.Reason,
        ReplacementTag = item.ReplacementTag,
        IncludeInPrompt = item.IncludeInPrompt,
        PromptOrder = item.PromptOrder,
    };

    private static string FormatAuditError(Exception ex) =>
        CharacterTagAuditErrorFormatter.Format(ex, key => key switch
        {
            "CharacterTagAuditModelInvalidReplacement" => "模型返回了无效替换: {0} -> {1}",
            _ => ex.Message,
        });

    private void Notify(string message)
    {
        StatusMessage = message;
        _alert?.Invoke(message);
    }
}

public sealed class CharacterTagAuditImageItem
{
    public CharacterTagAuditImageItem(DatasetManager.DataItem data)
    {
        Data = data;
        Name = data.Name;
        Path = data.ImageFilePath;
    }

    public DatasetManager.DataItem Data { get; }
    public string Name { get; }
    public string Path { get; }
    public string Display => Name;

    public override string ToString() => Name;
}

public sealed class NamedChoice<T>
{
    public NamedChoice(T value, string text)
    {
        Value = value;
        Text = text;
    }

    public T Value { get; }
    public string Text { get; }
    public override string ToString() => Text;
}

public partial class CharacterTagAuditReviewRow : ObservableObject
{
    public event Action? Changed;

    public string Tag { get; init; } = string.Empty;
    public int Count { get; init; }
    public CharacterTagCategory CategoryValue { get; init; }
    public string CategoryDisplay { get; init; } = string.Empty;
    public CharacterTagDecision InitialValue { get; init; }
    public string InitialDisplay { get; init; } = string.Empty;
    public bool CanModify { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int PromptOrder { get; init; }
    public IReadOnlyList<NamedChoice<CharacterTagDecision>> DecisionChoices { get; init; } =
        Array.Empty<NamedChoice<CharacterTagDecision>>();

    [ObservableProperty] private NamedChoice<CharacterTagDecision>? selectedDecision;
    [ObservableProperty] private string replacementTag = string.Empty;
    [ObservableProperty] private bool includeInPrompt;

    public CharacterTagDecision Decision => SelectedDecision?.Value ?? CharacterTagDecision.Keep;

    public static CharacterTagAuditReviewRow FromItem(
        CharacterTagAuditItem item,
        IReadOnlyList<NamedChoice<CharacterTagDecision>> choices)
    {
        bool canModify = CharacterTagAuditPolicy.CanDelete(item.Category);
        CharacterTagDecision decision = canModify ? item.FinalDecision : CharacterTagDecision.Keep;
        var row = new CharacterTagAuditReviewRow
        {
            Tag = item.Tag,
            Count = item.Count,
            CategoryValue = item.Category,
            CategoryDisplay = LocalizeCategory(item.Category),
            InitialValue = item.InitialDecision,
            InitialDisplay = LocalizeDecision(item.InitialDecision),
            CanModify = canModify,
            Reason = item.Reason ?? string.Empty,
            PromptOrder = item.PromptOrder,
            DecisionChoices = choices,
            ReplacementTag = item.ReplacementTag ?? string.Empty,
            IncludeInPrompt = item.IncludeInPrompt && decision != CharacterTagDecision.Delete,
            SelectedDecision = choices.FirstOrDefault(c => c.Value == decision) ?? choices.FirstOrDefault(),
        };
        return row;
    }

    public CharacterTagAuditItem ToAuditItem()
    {
        CharacterTagDecision decision = CanModify ? Decision : CharacterTagDecision.Keep;
        return new CharacterTagAuditItem
        {
            Tag = Tag,
            Count = Count,
            InitialDecision = InitialValue,
            FinalDecision = decision,
            Category = CategoryValue,
            Reason = Reason,
            ReplacementTag = ReplacementTag?.Trim() ?? string.Empty,
            IncludeInPrompt = decision != CharacterTagDecision.Delete && IncludeInPrompt,
            PromptOrder = PromptOrder,
        };
    }

    partial void OnSelectedDecisionChanged(NamedChoice<CharacterTagDecision>? value)
    {
        if (value?.Value == CharacterTagDecision.Delete)
            IncludeInPrompt = false;
        Changed?.Invoke();
    }

    partial void OnReplacementTagChanged(string value) => Changed?.Invoke();

    partial void OnIncludeInPromptChanged(bool value) => Changed?.Invoke();

    private static string LocalizeDecision(CharacterTagDecision decision) => decision switch
    {
        CharacterTagDecision.Keep => "保留",
        CharacterTagDecision.Delete => "删除",
        CharacterTagDecision.Replace => "替换",
        CharacterTagDecision.Uncertain => "不确定",
        _ => decision.ToString(),
    };

    private static string LocalizeCategory(CharacterTagCategory category) => category switch
    {
        CharacterTagCategory.Identity => "身份",
        CharacterTagCategory.Hair => "头发",
        CharacterTagCategory.Eyes => "眼睛",
        CharacterTagCategory.Face => "面部",
        CharacterTagCategory.Body => "身体",
        CharacterTagCategory.Clothing => "服装",
        CharacterTagCategory.Footwear => "鞋履",
        CharacterTagCategory.Legwear => "袜装",
        CharacterTagCategory.WearableAccessory => "配饰",
        CharacterTagCategory.Action => "动作",
        CharacterTagCategory.Pose => "姿势",
        CharacterTagCategory.Expression => "表情",
        CharacterTagCategory.Scene => "场景",
        CharacterTagCategory.Composition => "构图",
        CharacterTagCategory.Quality => "质量",
        CharacterTagCategory.Object => "物体",
        _ => "其他",
    };
}
