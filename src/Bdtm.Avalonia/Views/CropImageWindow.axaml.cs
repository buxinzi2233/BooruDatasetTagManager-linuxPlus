using System;
using System.ComponentModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Bdtm.Avalonia.ViewModels;
using Bdtm.Core;

namespace Bdtm.Avalonia.Views;

public partial class CropImageWindow : Window
{
    private Bitmap? _sourceBitmap;
    private bool _loaded;
    private CropImageViewModel? _vm;

    public CropImageWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as CropImageViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CropImageViewModel.SelectedRegion)
            or nameof(CropImageViewModel.RegionItems)
            or null)
            SyncListSelection();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_loaded) return;
        _loaded = true;

        Canvas.RegionDragCompleted += OnRegionDragCompleted;

        if (DataContext is not CropImageViewModel vm)
            return;

        try
        {
            if (File.Exists(vm.ImagePath))
            {
                using var stream = File.OpenRead(vm.ImagePath);
                _sourceBitmap = new Bitmap(stream);
                Canvas.Source = _sourceBitmap;
                vm.SetImagePixelSize(_sourceBitmap.PixelSize.Width, _sourceBitmap.PixelSize.Height);
                // Push sizes onto control (ImageWidth/Height are not notify props)
                Canvas.ImagePixelWidth = _sourceBitmap.PixelSize.Width;
                Canvas.ImagePixelHeight = _sourceBitmap.PixelSize.Height;
            }
            else
            {
                vm.StatusMessage = "图片文件不存在。";
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = "加载图片失败: " + ex.Message;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Canvas.RegionDragCompleted -= OnRegionDragCompleted;
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
        Canvas.Source = null;
        _sourceBitmap?.Dispose();
        _sourceBitmap = null;
    }

    private void OnRegionDragCompleted(CropRect imageRect)
    {
        if (DataContext is CropImageViewModel vm)
            vm.AddRegionFromImageRect(imageRect);
        SyncListSelection();
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CropImageViewModel vm)
        {
            Close(false);
            return;
        }

        if (vm.TryExport())
            Close(true);
        // else keep open; StatusMessage set by VM
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CropImageViewModel vm)
            vm.DeleteSelected();
        SyncListSelection();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (DataContext is CropImageViewModel vm)
                vm.DeleteSelected();
            SyncListSelection();
            e.Handled = true;
        }
    }

    private void RegionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CropImageViewModel vm)
            return;
        if (RegionList.SelectedItem is CropRegionListItem item)
            vm.SelectedRegion = item.Region;
        else if (RegionList.SelectedItem is null && e.AddedItems.Count == 0)
        {
            // keep canvas selection unless user cleared list
        }
    }

    private void SyncListSelection()
    {
        if (DataContext is not CropImageViewModel vm)
            return;

        CropRegionListItem? match = null;
        if (vm.SelectedRegion is not null)
        {
            foreach (var item in vm.RegionItems)
            {
                if (ReferenceEquals(item.Region, vm.SelectedRegion))
                {
                    match = item;
                    break;
                }
            }
        }

        RegionList.SelectedItem = match;
        Canvas.InvalidateVisual();
    }
}
