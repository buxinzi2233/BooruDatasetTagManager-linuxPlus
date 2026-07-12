using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Bdtm.Avalonia.Views;

public partial class BgRemovalWindow : Window
{
    public BgRemovalWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
