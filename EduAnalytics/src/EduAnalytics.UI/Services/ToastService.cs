using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EduAnalytics.UI.Services;

public enum ToastSeverity { Success, Error, Info, Warning }

public partial class ToastItem : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastSeverity Severity { get; set; }
}

/// <summary>
/// Ekranın sağ alt köşesinde 3.5 saniye görünen kısa bildirimler.
/// Singleton — DI ile inject edilir, her VM kullanabilir.
/// </summary>
public class ToastService
{
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public void Show(string title, string? message = null, ToastSeverity severity = ToastSeverity.Info)
    {
        var item = new ToastItem
        {
            Title = title,
            Message = message ?? string.Empty,
            Severity = severity
        };

        Application.Current?.Dispatcher.Invoke(() => Toasts.Add(item));

        Task.Delay(3500).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Toasts.Contains(item)) Toasts.Remove(item);
            });
        });
    }

    public void Success(string title, string? message = null) => Show(title, message, ToastSeverity.Success);
    public void Error(string title, string? message = null) => Show(title, message, ToastSeverity.Error);
    public void Info(string title, string? message = null) => Show(title, message, ToastSeverity.Info);
    public void Warning(string title, string? message = null) => Show(title, message, ToastSeverity.Warning);
}
