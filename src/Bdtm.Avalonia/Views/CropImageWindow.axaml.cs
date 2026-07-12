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
        Canvas.RegionClicked += OnRegionClicked;

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
        Canvas.RegionClicked -= OnRegionClicked;
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

    private void OnRegionClicked(CropRegion region, bool toggle)
    {
        if (DataContext is not CropImageViewModel vm)
            return;

        if (toggle)
            vm.ToggleSelection(region);
        else
            vm.SetSelection(region);

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

    private bool _syncingList;

    private void RegionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingList || DataContext is not CropImageViewModel vm)
            return;

        var selected = new List<CropRegionListItem>();
        if (RegionList.SelectedItems is { } items)
        {
            foreach (var obj in items)
            {
                if (obj is CropRegionListItem item)
                    selected.Add(item);
            }
        }

        vm.SetSelectionFromList(selected);
        Canvas.InvalidateVisual();
    }

    private void SyncListSelection()
    {
        if (DataContext is not CropImageViewModel vm)
            return;

        _syncingList = true;
        try
        {
            if (RegionList.SelectedItems is { } selectedItems)
            {
                selectedItems.Clear();
                foreach (var item in vm.RegionItems)
                {
                    if (vm.IsRegionSelected(item.Region))
                        selectedItems.Add(item);
                }
            }
        }
        finally
        {
            _syncingList = false;
        }

        Canvas.InvalidateVisual();
    }
}
