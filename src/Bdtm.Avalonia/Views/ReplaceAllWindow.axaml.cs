using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Bdtm.Avalonia.Views;

public partial class ReplaceAllWindow : Window
{
    public string? SourceTag => SourceTagCombo.SelectedItem as string;
    public string? NewTag => NewTagTextBox.Text?.Trim();

    public ReplaceAllWindow()
    {
        InitializeComponent();
    }

    public void SetAllTags(ObservableCollection<string> tags)
    {
        SourceTagCombo.ItemsSource = tags;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
