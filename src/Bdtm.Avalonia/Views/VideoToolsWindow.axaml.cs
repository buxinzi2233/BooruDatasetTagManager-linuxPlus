using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Bdtm.Avalonia.Views;

public partial class VideoToolsWindow : Window
{
    public VideoToolsWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
