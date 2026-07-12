using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class WikiViewModel : ViewModelBase
{
    private readonly string _tag;
    private readonly WikiCache _cache = new();
    private string _wikiUrl;
    private bool _fromCache;

    public WikiViewModel(string tag)
    {
        _tag = tag;
        Tag = DanbooruWikiClient.NormalizeTag(tag);
        _wikiUrl = DanbooruWikiClient.GetWikiUrl(Tag);
        Title = "Wiki · " + Tag;
        Body = "加载中…";
        Meta = string.Empty;
    }

    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string body = string.Empty;
    [ObservableProperty] private string meta = string.Empty;
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private bool notFound;
    [ObservableProperty] private string sourceLabel = string.Empty;

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        NotFound = false;
        try
        {
            if (!forceRefresh)
            {
                var cached = _cache.TryGet(_tag);
                if (cached is not null)
                {
                    ApplyPage(cached, fromCache: true);
                    return;
                }
            }
            else
            {
                _cache.Invalidate(_tag);
            }

            using var client = new DanbooruWikiClient();
            var page = await client.GetWikiPageAsync(_tag);
            if (page is null)
            {
                NotFound = true;
                Title = Tag;
                Body = "未找到该标签的 Wiki 页面。";
                Meta = _wikiUrl;
                SourceLabel = forceRefresh ? "联网刷新：无结果" : "联网：无结果";
                return;
            }

            _cache.Set(_tag, page);
            ApplyPage(page, fromCache: false);
        }
        catch (Exception ex)
        {
            // Fall back to stale cache if network fails
            var stale = _cache.TryGet(_tag);
            if (stale is not null)
            {
                ApplyPage(stale, fromCache: true);
                SourceLabel += " · 网络失败已用缓存: " + ex.Message;
                return;
            }

            Body = "加载失败: " + ex.Message;
            Meta = _wikiUrl;
            SourceLabel = "错误";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyPage(DanbooruWikiPage page, bool fromCache)
    {
        _fromCache = fromCache;
        Title = page.Title;
        Body = string.IsNullOrWhiteSpace(page.Body) ? "（无正文）" : page.Body;
        _wikiUrl = string.IsNullOrWhiteSpace(page.Url) ? DanbooruWikiClient.GetWikiUrl(Tag) : page.Url;
        string others = page.OtherNames.Count == 0 ? "—" : string.Join(", ", page.OtherNames);
        string updated = page.UpdatedAt?.ToString("u") ?? "—";
        Meta = $"别名: {others}\n更新: {updated}\n{_wikiUrl}";
        SourceLabel = fromCache
            ? "来源: 本地缓存 (" + _cache.Root + ")"
            : "来源: 联网 Danbooru API";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync(forceRefresh: true);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _wikiUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            // ignore
        }
    }
}
