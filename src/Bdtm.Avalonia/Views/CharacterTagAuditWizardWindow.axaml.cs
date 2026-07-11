using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Bdtm.Avalonia.ViewModels;

namespace Bdtm.Avalonia.Views;

public partial class CharacterTagAuditWizardWindow : Window
{
    private CharacterTagAuditWizardViewModel? _vm;

    public CharacterTagAuditWizardWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _vm = DataContext as CharacterTagAuditWizardViewModel;
        if (_vm is null) return;
        _vm.RequestClose += OnRequestClose;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose()
    {
        Dispatcher.UIThread.Post(() =>
        {
            bool? result = _vm?.Applied == true ? true : false;
            try { Close(result); } catch { /* ignore if already closing */ }
        });
    }

    private async void CopyPrompt_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CharacterTagAuditWizardViewModel vm)
            return;
        string text = vm.FinalPrompt ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            vm.StatusMessage = "最终提示词为空。";
            return;
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                vm.StatusMessage = "剪贴板不可用。";
                return;
            }
            await clipboard.SetTextAsync(text);
            vm.StatusMessage = "已复制最终提示词。";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = "复制失败: " + ex.Message;
        }
    }
}
