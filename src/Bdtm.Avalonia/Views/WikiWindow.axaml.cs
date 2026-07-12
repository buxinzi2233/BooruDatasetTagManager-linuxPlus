using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class WikiWindow : Window
{
    public WikiWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is WikiViewModel vm)
                await vm.LoadAsync();
        };
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
