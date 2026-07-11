using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Bdtm.Avalonia.Views;

public partial class Tag2NlConfirmWindow : Window
{
    public Tag2NlConfirmWindow()
    {
        InitializeComponent();
    }

    private void Start_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
