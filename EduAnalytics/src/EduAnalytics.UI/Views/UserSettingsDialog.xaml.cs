using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EduAnalytics.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace EduAnalytics.UI.Views;

public partial class UserSettingsDialog : Window
{
    private readonly IUserProfileService _profile;
    private string? _selectedPhotoPath;
    private bool _removePhoto;

    public UserSettingsDialog()
    {
        InitializeComponent();
        _profile = App.Services.GetRequiredService<IUserProfileService>();
        UsernameBox.Text = _profile.Current.Username;
        SelectGender(_profile.Current.Gender);
        RefreshAvatar();
    }

    private void SelectPhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Profil fotoğrafı seç",
            Filter = "Görsel dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Tüm dosyalar|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _selectedPhotoPath = dialog.FileName;
        _removePhoto = false;
        RefreshAvatar();
    }

    private void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        _selectedPhotoPath = null;
        _removePhoto = true;
        RefreshAvatar();
    }

    private void GenderOption_Checked(object sender, RoutedEventArgs e) => RefreshAvatar();

    private void UsernameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshAvatar();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        HideStatus();
        var username = UsernameBox.Text.Trim();
        var newPassword = NewPasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;
        var wantsPasswordChange = !string.IsNullOrWhiteSpace(newPassword) || !string.IsNullOrWhiteSpace(confirmPassword);

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowStatus("Kullanıcı adını girmediniz.");
            return;
        }

        if (wantsPasswordChange && newPassword != confirmPassword)
        {
            ShowStatus("Yeni şifre tekrarı aynı değil.");
            return;
        }

        try
        {
            _profile.UpdateProfile(
                username,
                GetSelectedGender(),
                CurrentPasswordBox.Password,
                wantsPasswordChange ? newPassword : null,
                _selectedPhotoPath,
                _removePhoto);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void RefreshAvatar()
    {
        var previewPhoto = _selectedPhotoPath;
        if (string.IsNullOrWhiteSpace(previewPhoto) && !_removePhoto)
            previewPhoto = _profile.GetPhotoPath();

        if (!string.IsNullOrWhiteSpace(previewPhoto) && File.Exists(previewPhoto))
        {
            AvatarImage.Source = LoadBitmap(previewPhoto);
            AvatarImage.Visibility = Visibility.Visible;
            AvatarText.Visibility = Visibility.Collapsed;
            return;
        }

        AvatarImage.Visibility = Visibility.Collapsed;
        AvatarText.Visibility = Visibility.Visible;
        AvatarText.Text = string.IsNullOrWhiteSpace(UsernameBox.Text)
            ? "EA"
            : UsernameBox.Text.Trim()[0].ToString().ToUpperInvariant();
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void SelectGender(UserGender gender)
    {
        GenderFemaleOption.IsChecked = gender == UserGender.Female;
        GenderMaleOption.IsChecked = gender == UserGender.Male;
        GenderUnspecifiedOption.IsChecked = gender == UserGender.Unspecified;
    }

    private UserGender GetSelectedGender()
    {
        if (GenderFemaleOption.IsChecked == true) return UserGender.Female;
        if (GenderMaleOption.IsChecked == true) return UserGender.Male;
        return UserGender.Unspecified;
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

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void AvatarImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AvatarImage.Clip = new EllipseGeometry(
            new Point(e.NewSize.Width / 2, e.NewSize.Height / 2),
            e.NewSize.Width / 2,
            e.NewSize.Height / 2);
    }
}
