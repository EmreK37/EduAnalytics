using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.Views;

public partial class AIAssistantPanel : UserControl
{
    private AIAssistantViewModel? _subscribedVm;

    public AIAssistantPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.Messages.CollectionChanged -= OnMessagesChanged;
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is AIAssistantViewModel vm)
        {
            _subscribedVm = vm;
            vm.Messages.CollectionChanged += OnMessagesChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVm == null) return;
        _subscribedVm.Messages.CollectionChanged -= OnMessagesChanged;
        _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedVm = null;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ChatScroll.ScrollToEnd());
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AIAssistantViewModel.IsPanelOpen)) return;
        if (sender is not AIAssistantViewModel { IsPanelOpen: true }) return;

        Dispatcher.BeginInvoke(() =>
        {
            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text.Length;
        });
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AIAssistantViewModel vm && vm.IsPanelOpen)
            vm.TogglePanelCommand.Execute(null);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AIAssistantViewModel vm) return;

        try
        {
            var dialog = new AIAssistantSettingsDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
                vm.RefreshConfigStatus();
        }
        catch (Exception ex)
        {
            App.Services.GetRequiredService<IAppLogService>()
                .Error("AI.Settings", "AI settings dialog failed to open from panel.", ex);
            App.Services.GetRequiredService<ToastService>()
                .Error("AI ayarları açılamadı", ex.Message);
        }
    }

    private void SuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AIAssistantViewModel vm) return;
        if (sender is FrameworkElement { Tag: string prompt })
        {
            vm.InputText = prompt;
            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text.Length;
        }
    }

    private void SuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AIAssistantViewModel vm) return;
        if (sender is FrameworkElement { Tag: string prompt })
        {
            vm.InputText = prompt;
            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text.Length;
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            if (DataContext is AIAssistantViewModel vm && vm.SendCommand.CanExecute(null))
                vm.SendCommand.Execute(null);

            e.Handled = true;
        }
    }
}
