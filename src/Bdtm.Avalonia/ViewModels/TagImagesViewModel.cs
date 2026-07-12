using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bdtm.Avalonia.Services;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class TagImagesViewModel : ViewModelBase
{
    private readonly string _tag;
    private readonly System.Collections.Generic.List<DatasetManager.DataItem> _allItems;

    public ObservableCollection<TagImageItem> Items { get; } = new();

    public string Title => $"图片网格: {_tag}";

    [ObservableProperty]
    private int _thumbnailSize = 120;

    public string Summary
    {
        get
        {
            int total = Items.Count;
            int hasTag = Items.Count(i => i.HasTag);
            return $"标签「{_tag}」· 含标签 {hasTag}/{total} 张";
        }
    }

    public TagImagesViewModel(string tag, System.Collections.Generic.IReadOnlyList<DatasetManager.DataItem> allItems)
    {
        _tag = tag;
        _allItems = allItems.ToList();

        foreach (var item in _allItems)
        {
            bool hasTag = item.Tags.Contains(tag);
            Items.Add(new TagImageItem(this, item, hasTag));
        }

        _ = LoadThumbnailsAsync();
    }

    partial void OnThumbnailSizeChanged(int value)
    {
        foreach (var item in Items)
            item.OnParentThumbSizeChanged();
    }

    public void Apply()
    {
        foreach (var item in Items)
        {
            if (item.OriginalHasTag != item.HasTag)
            {
                if (item.HasTag)
                    item.Data.Tags.Add(_tag, skipIfExists: true);
                else
                    item.Data.Tags.Remove(_tag);
            }
        }
    }

    private async Task LoadThumbnailsAsync()
    {
        int edge = Math.Clamp(ThumbnailSize, 50, 200);
        foreach (var item in Items)
        {
            if (!ImageThumbnailLoader.IsRasterImage(item.Path))
                continue;

            try
            {
                var bmp = await ImageThumbnailLoader.LoadAsync(item.Path, edge);
                if (bmp is null) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var old = item.Thumbnail;
                    item.Thumbnail = bmp;
                    old?.Dispose();
                });
                await Task.Yield();
            }
            catch
            {
                // skip bad images
            }
        }
    }
}

public partial class TagImageItem : ObservableObject
{
    private readonly TagImagesViewModel _parent;

    public DatasetManager.DataItem Data { get; }
    public string Name => Data.Name;
    public string Path => Data.ImageFilePath;
    public bool OriginalHasTag { get; }

    public int ThumbSize => _parent.ThumbnailSize;
    public int CardWidth => _parent.ThumbnailSize + 12;

    [ObservableProperty]
    private bool _hasTag;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    public TagImageItem(TagImagesViewModel parent, DatasetManager.DataItem data, bool hasTag)
    {
        _parent = parent;
        Data = data;
        OriginalHasTag = hasTag;
        _hasTag = hasTag;
    }

    public void OnParentThumbSizeChanged()
    {
        OnPropertyChanged(nameof(ThumbSize));
        OnPropertyChanged(nameof(CardWidth));
    }
}
