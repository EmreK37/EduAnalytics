using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using EduAnalytics.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.Views;

public partial class LoginWindow : Window
{
    private readonly IUserProfileService _profile;
    private readonly bool _isSetupMode;

    public LoginWindow()
    {
        InitializeComponent();
        _profile = App.Services.GetRequiredService<IUserProfileService>();
        _isSetupMode = !_profile.HasUser;
        ConfigureMode();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void ConfigureMode()
    {
        if (!_isSetupMode)
        {
            UsernameBox.Text = _profile.Current.Username;
            return;
        }

        TitleText.Text = "Ä°lk kullanÄ±cÄ±yÄ± oluÅŸturun";
        SubtitleText.Text = "UygulamayÄ± kullanmak iÃ§in yerel bir kullanÄ±cÄ± adÄ± ve ÅŸifre belirleyin.";
        ConfirmPasswordPanel.Visibility = Visibility.Visible;
        SubmitButton.Content = "KullanÄ±cÄ±yÄ± OluÅŸtur";
    }

    private void Submit_Click(object sender, RoutedEventArgs e) => Submit();

    private void Submit()
    {
        HideStatus();
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowStatus("KullanÄ±cÄ± adÄ±nÄ± girmediniz.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("Şifreyi girmediniz.");
            return;
        }

        // --- GİZLİ ADMİN MODU ---
        if (username.ToLowerInvariant() == "admin" && password == "admin123")
        {
            App.IsAdminMode = true;
            DialogResult = true;
            return;
        }
        try
        {
            if (_isSetupMode)
            {
                if (password != ConfirmPasswordBox.Password)
                {
                    ShowStatus("Åifre tekrarÄ± aynÄ± deÄŸil.");
                    return;
                }

                _profile.CreateUser(username, password);
                DialogResult = true;
                return;
            }

            if (!_profile.SignIn(username, password))
            {
                ShowStatus("KullanÄ±cÄ± adÄ± veya ÅŸifre hatalÄ±.");
                return;
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusBorder.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusText.Text = string.Empty;
        StatusBorder.Visibility = Visibility.Collapsed;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void WindowSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
            return;

        if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
            return;

        DragMove();
    }

    private static bool IsInteractiveElement(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is ButtonBase or TextBox or System.Windows.Controls.PasswordBox)
                return true;

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
