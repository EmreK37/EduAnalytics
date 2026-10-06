using System.Windows;

namespace EduAnalytics.UI.Services;

public static class AppMessageBox
{
    public static MessageBoxResult Show(
        string message,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
        => MessageBox.Show(message, caption, button, icon);

    public static MessageBoxResult Show(
        Window? owner,
        string message,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
        => owner is null
            ? MessageBox.Show(message, caption, button, icon)
            : MessageBox.Show(owner, message, caption, button, icon);
}

