using Avalonia.Controls;
using Avalonia.Interactivity;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class OnnxPreviewWindow : Window
{
    public OnnxPreviewWindow()
    {
        InitializeComponent();
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OnnxPreviewViewModel vm)
            vm.MarkConfirmed();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
