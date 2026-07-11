using System.Linq;
using Avalonia.Controls;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ImageList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (sender is not ListBox list)
            return;

        var selected = list.SelectedItems?
            .OfType<ImageListItem>()
            .ToList() ?? new();
        vm.SetSelectedImages(selected);
    }
}
