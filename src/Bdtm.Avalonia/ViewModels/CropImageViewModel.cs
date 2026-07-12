using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class CropImageViewModel : ViewModelBase
{
    public const int MinimumCropSize = 8;

    public CropImageViewModel(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        ImagePath = Path.GetFullPath(imagePath);
        Regions = new ObservableCollection<CropRegion>();
        Regions.CollectionChanged += OnRegionsCollectionChanged;
        ExportedPaths = Array.Empty<string>();
        FileName = Path.GetFileName(ImagePath);

        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(ImagePath);
            if (info is not null)
            {
                ImageWidth = info.Width;
                ImageHeight = info.Height;
            }
        }
        catch
        {
            ImageWidth = 0;
            ImageHeight = 0;
        }
    }

    public string ImagePath { get; }
    public string FileName { get; }
    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }
    public ObservableCollection<CropRegion> Regions { get; }
    public IReadOnlyList<string> ExportedPaths { get; private set; }

    [ObservableProperty] private CropRegion? selectedRegion;
    [ObservableProperty] private string statusMessage = "在图像上拖拽添加裁剪区域。";

    /// <summary>List rows for binding (#n w×h). Synced with <see cref="Regions"/>.</summary>
    public ObservableCollection<CropRegionListItem> RegionItems { get; } = new();

    public void AddRegionFromImageRect(CropRect rect)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0)
        {
            StatusMessage = "无法读取图像尺寸。";
            return;
        }

        CropRect clamped = CropCanvasHelper.ClampToImage(rect, ImageWidth, ImageHeight);
        if (clamped.Width < MinimumCropSize || clamped.Height < MinimumCropSize)
        {
            StatusMessage = $"选区过小（最小 {MinimumCropSize}×{MinimumCropSize}）。";
            return;
        }

        int index = Regions.Count + 1;
        var region = new CropRegion
        {
            Index = index,
            Bounds = clamped,
            DisplayColorArgb = CropCanvasHelper.RegionColors[(index - 1) % CropCanvasHelper.RegionColors.Length],
        };
        Regions.Add(region);
        SelectedRegion = region;
        StatusMessage = $"已添加区域 #{region.Index}（{region.Bounds.Width}×{region.Bounds.Height}）。";
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        if (SelectedRegion is null)
        {
            StatusMessage = "请先选择一个区域。";
            return;
        }

        Regions.Remove(SelectedRegion);
        SelectedRegion = null;
        RenumberRegions();
        StatusMessage = Regions.Count == 0
            ? "已删除区域。在图像上拖拽添加裁剪区域。"
            : $"已删除区域。剩余 {Regions.Count} 个。";
    }

    public bool TryExport()
    {
        if (Regions.Count == 0)
        {
            StatusMessage = "请先添加至少一个裁剪区域。";
            ExportedPaths = Array.Empty<string>();
            return false;
        }

        if (!File.Exists(ImagePath))
        {
            StatusMessage = "源图像不存在。";
            ExportedPaths = Array.Empty<string>();
            return false;
        }

        try
        {
            ExportedPaths = ImageCropExporter.ExportRegions(ImagePath, Regions.ToList());
        }
        catch (Exception ex)
        {
            StatusMessage = "导出失败: " + ex.Message;
            ExportedPaths = Array.Empty<string>();
            return false;
        }

        if (ExportedPaths.Count == 0)
        {
            StatusMessage = "没有可导出的有效区域。";
            return false;
        }

        string dir = ImageCropExporter.GetOutputDirectory(ImagePath);
        StatusMessage = $"已导出 {ExportedPaths.Count} 张到 {dir}";
        return true;
    }

    public void SetImagePixelSize(int width, int height)
    {
        if (width > 0 && height > 0)
        {
            ImageWidth = width;
            ImageHeight = height;
        }
    }

    private void RenumberRegions()
    {
        for (int i = 0; i < Regions.Count; i++)
        {
            Regions[i].Index = i + 1;
            Regions[i].DisplayColorArgb =
                CropCanvasHelper.RegionColors[i % CropCanvasHelper.RegionColors.Length];
        }

        RebuildRegionItems();
    }

    private void OnRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildRegionItems();

    private void RebuildRegionItems()
    {
        RegionItems.Clear();
        foreach (CropRegion region in Regions)
            RegionItems.Add(new CropRegionListItem(region));

        // Keep list selection in sync when renumbering changes display text only.
        if (SelectedRegion is not null)
        {
            var match = RegionItems.FirstOrDefault(i => ReferenceEquals(i.Region, SelectedRegion));
            // no-op; SelectedRegion binding is on CropRegion via canvas/list
        }
    }

    partial void OnSelectedRegionChanged(CropRegion? value)
    {
        foreach (var item in RegionItems)
            item.IsSelected = ReferenceEquals(item.Region, value);
    }
}

public partial class CropRegionListItem : ObservableObject
{
    public CropRegionListItem(CropRegion region)
    {
        Region = region;
        RefreshText();
    }

    public CropRegion Region { get; }

    [ObservableProperty] private string displayText = string.Empty;
    [ObservableProperty] private bool isSelected;

    public void RefreshText()
    {
        DisplayText = $"#{Region.Index}  {Region.Bounds.Width}×{Region.Bounds.Height}";
    }

    public override string ToString() => DisplayText;
}
