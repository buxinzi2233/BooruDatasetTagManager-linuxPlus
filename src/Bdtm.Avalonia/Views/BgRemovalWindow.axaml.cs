using Avalonia.Controls;
using Avalonia.Interactivity;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class BgRemovalWindow : Window
{
    public BgRemovalWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BgRemovalViewModel vm) return;
        if (vm.SelectedModel is null) return;

        // Find a test image: use the first selected image if available, else ask VM to use any from dataset
        // MainViewModel passes a test image path via a property before ShowDialog
        if (!string.IsNullOrEmpty(TestImagePath) && System.IO.File.Exists(TestImagePath))
            await vm.TestAsync(TestImagePath);
    }

    /// <summary>Set by MainViewModel before ShowDialog to allow test button to work.</summary>
    public string? TestImagePath { get; set; }
}