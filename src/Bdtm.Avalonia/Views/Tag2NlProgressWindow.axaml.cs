using Avalonia.Controls;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class Tag2NlProgressWindow : Window
{
    public Tag2NlProgressWindow()
    {
        InitializeComponent();
        Closing += (_, e) =>
        {
            if (DataContext is Tag2NlProgressViewModel vm && !vm.AllowClose)
            {
                vm.RequestCancel();
                e.Cancel = true;
            }
        };
    }
}
