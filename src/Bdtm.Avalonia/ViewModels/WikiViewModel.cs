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
    private string _wikiUrl;

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

    public async Task LoadAsync()
    {
        IsLoading = true;
        NotFound = false;
        try
        {
            using var client = new DanbooruWikiClient();
            var page = await client.GetWikiPageAsync(_tag);
            if (page is null)
            {
                NotFound = true;
                Title = Tag;
                Body = "未找到该标签的 Wiki 页面。";
                Meta = _wikiUrl;
                return;
            }

            Title = page.Title;
            Body = string.IsNullOrWhiteSpace(page.Body) ? "（无正文）" : page.Body;
            _wikiUrl = page.Url;
            string others = page.OtherNames.Count == 0
                ? "—"
                : string.Join(", ", page.OtherNames);
            string updated = page.UpdatedAt?.ToString("u") ?? "—";
            Meta = $"别名: {others}\n更新: {updated}\n{page.Url}";
        }
        catch (Exception ex)
        {
            Body = "加载失败: " + ex.Message;
            Meta = _wikiUrl;
        }
        finally
        {
            IsLoading = false;
        }
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
