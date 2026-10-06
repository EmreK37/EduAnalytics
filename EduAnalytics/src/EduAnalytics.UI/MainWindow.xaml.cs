using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using EduAnalytics.UI.Services;
using EduAnalytics.UI.ViewModels;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace EduAnalytics.UI;

public partial class MainWindow : FluentWindow
{
    public static readonly DependencyProperty IsSidebarCollapsedProperty =
        DependencyProperty.Register(nameof(IsSidebarCollapsed), typeof(bool), typeof(MainWindow),
            new PropertyMetadata(false));

    public bool IsSidebarCollapsed
    {
        get => (bool)GetValue(IsSidebarCollapsedProperty);
        set => SetValue(IsSidebarCollapsedProperty, value);
    }

    public static readonly DependencyProperty IsSettingsSectionExpandedProperty =
        DependencyProperty.Register(nameof(IsSettingsSectionExpanded), typeof(bool), typeof(MainWindow),
            new PropertyMetadata(false));

    public bool IsSettingsSectionExpanded
    {
        get => (bool)GetValue(IsSettingsSectionExpandedProperty);
        set => SetValue(IsSettingsSectionExpandedProperty, value);
    }

    public static readonly DependencyProperty CurrentUserNameProperty =
        DependencyProperty.Register(nameof(CurrentUserName), typeof(string), typeof(MainWindow),
            new PropertyMetadata("EduAnalytics"));

    public string CurrentUserName
    {
        get => (string)GetValue(CurrentUserNameProperty);
        set => SetValue(CurrentUserNameProperty, value);
    }

    public static readonly DependencyProperty CurrentUserAvatarTextProperty =
        DependencyProperty.Register(nameof(CurrentUserAvatarText), typeof(string), typeof(MainWindow),
            new PropertyMetadata("EA"));

    public string CurrentUserAvatarText
    {
        get => (string)GetValue(CurrentUserAvatarTextProperty);
        set => SetValue(CurrentUserAvatarTextProperty, value);
    }

    public static readonly DependencyProperty CurrentUserPhotoProperty =
        DependencyProperty.Register(nameof(CurrentUserPhoto), typeof(ImageSource), typeof(MainWindow),
            new PropertyMetadata(null));

    public ImageSource? CurrentUserPhoto
    {
        get => (ImageSource?)GetValue(CurrentUserPhotoProperty);
        set => SetValue(CurrentUserPhotoProperty, value);
    }

    public static readonly DependencyProperty HasCurrentUserPhotoProperty =
        DependencyProperty.Register(nameof(HasCurrentUserPhoto), typeof(bool), typeof(MainWindow),
            new PropertyMetadata(false));

    public bool HasCurrentUserPhoto
    {
        get => (bool)GetValue(HasCurrentUserPhotoProperty);
        set => SetValue(HasCurrentUserPhotoProperty, value);
    }

    private const double SidebarExpandedWidth = 260;
    private const double SidebarCollapsedWidth = 64;
    private const double AIPanelExpandedWidth = 380;
    private const double AIFloatingButtonInset = 24;
    private const double AIFloatingButtonDragThreshold = 10;

    private readonly MainViewModel _viewModel;
    private readonly IAppLogService _log;
    private readonly IUserProfileService _userProfile;
    private bool _isAiButtonDragging;
    private bool _suppressAiButtonClick;
    private Point _aiButtonDragOffset;
    private Point _aiButtonDragStart;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _log = App.Services.GetRequiredService<IAppLogService>();
        _userProfile = App.Services.GetRequiredService<IUserProfileService>();
        DataContext = viewModel;
        RefreshUserIdentity();
        UpdateThemeIcon(App.Services.GetRequiredService<IThemeService>().CurrentTheme);
        PreviewKeyDown += OnPreviewKeyDown;
        _viewModel.AIAssistant.PropertyChanged += OnAIAssistantPropertyChanged;
        Loaded += (_, _) => ApplyAIPanelState(_viewModel.AIAssistant.IsPanelOpen, "window-loaded");
        Closed += (_, _) => _viewModel.AIAssistant.PropertyChanged -= OnAIAssistantPropertyChanged;
    }

    private void OnAIAssistantPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AIAssistantViewModel.IsPanelOpen))
            ApplyAIPanelState(_viewModel.AIAssistant.IsPanelOpen, "property-changed");
    }

    private void ApplyAIPanelState(bool open, string source)
    {
        var target = open ? AIPanelExpandedWidth : 0;

        AIPanelColumn.BeginAnimation(System.Windows.Controls.ColumnDefinition.WidthProperty, null);
        AIPanelColumn.Width = new GridLength(target);
        AIPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;

        if (!open)
            Dispatcher.BeginInvoke(PositionAiFloatingButtonAtDefault);

        _log.Info("AI.Panel",
            $"Panel state applied. Open={open}, Width={target}, Source={source}");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemQuestion || e.Key == Key.Oem2))
        {
            ShowShortcuts_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        var svc = App.Services.GetRequiredService<IThemeService>();
        svc.CycleTheme();
        UpdateThemeIcon(svc.CurrentTheme);
    }

    private void UpdateThemeIcon(AppThemeMode mode)
    {
        ThemeIcon.Kind = mode switch
        {
            AppThemeMode.Dark   => MaterialIconKind.WeatherNight,
            AppThemeMode.System => MaterialIconKind.ThemeLightDark,
            _                => MaterialIconKind.WeatherSunny
        };
        ThemeToggleBtn.ToolTip = mode switch
        {
            AppThemeMode.Dark   => "Tema: Koyu",
            AppThemeMode.System => "Tema: Sistem (otomatik)",
            _                => "Tema: Açık"
        };
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        var target = IsSidebarCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;
        var current = SidebarColumn.ActualWidth > 0 ? SidebarColumn.ActualWidth : SidebarColumn.Width.Value;

        SidebarColumn.BeginAnimation(System.Windows.Controls.ColumnDefinition.WidthProperty, null);
        SidebarColumn.Width = new GridLength(current);

        var anim = new GridLengthAnimation
        {
            From = new GridLength(current),
            To = new GridLength(target),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        anim.Completed += (_, _) =>
        {
            SidebarColumn.BeginAnimation(System.Windows.Controls.ColumnDefinition.WidthProperty, null);
            SidebarColumn.Width = new GridLength(target);
        };

        SidebarColumn.BeginAnimation(System.Windows.Controls.ColumnDefinition.WidthProperty, anim);
    }

    private void ToggleSettingsSection_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsSectionExpanded = !IsSettingsSectionExpanded;
    }

    private void AiFloatingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressAiButtonClick)
        {
            _suppressAiButtonClick = false;
            return;
        }

        OpenAiFloatingPanel();
    }

    private void OpenAiFloatingPanel()
    {
        if (_viewModel.AIAssistant.TogglePanelCommand.CanExecute(null))
        {
            _viewModel.AIAssistant.TogglePanelCommand.Execute(null);
            Dispatcher.BeginInvoke(() =>
                ApplyAIPanelState(_viewModel.AIAssistant.IsPanelOpen, "floating-button"));
            _viewModel.Toasts.Info("AI Asistan açıldı", "Uygulama kullanımı, sınav analizi ve raporlar hakkında soru sorabilirsiniz.");
        }
    }

    private void AiFloatingButton_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(PositionAiFloatingButtonAtDefault);
    }

    private void AiFloatingLayer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionAiFloatingButtonAtDefault();
    }

    private void AiFloatingButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isAiButtonDragging = true;
        _suppressAiButtonClick = false;
        _aiButtonDragStart = e.GetPosition(AiFloatingLayer);

        var left = GetAiFloatingButtonLeft();
        var top = GetAiFloatingButtonTop();
        _aiButtonDragOffset = new Point(_aiButtonDragStart.X - left, _aiButtonDragStart.Y - top);

        AiFloatingButton.CaptureMouse();
    }

    private void AiFloatingButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isAiButtonDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(AiFloatingLayer);
        if (Math.Abs(current.X - _aiButtonDragStart.X) > AIFloatingButtonDragThreshold ||
            Math.Abs(current.Y - _aiButtonDragStart.Y) > AIFloatingButtonDragThreshold)
        {
            _suppressAiButtonClick = true;
        }

        SetAiFloatingButtonPosition(
            current.X - _aiButtonDragOffset.X,
            current.Y - _aiButtonDragOffset.Y);

        e.Handled = true;
    }

    private void AiFloatingButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isAiButtonDragging)
            return;

        _isAiButtonDragging = false;
        AiFloatingButton.ReleaseMouseCapture();

        if (_suppressAiButtonClick)
        {
            _suppressAiButtonClick = false;
            e.Handled = true;
            return;
        }

        OpenAiFloatingPanel();
        e.Handled = true;
    }

    private void AiFloatingButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isAiButtonDragging = false;
    }

    private bool HasAiFloatingButtonPosition()
        => !double.IsNaN(System.Windows.Controls.Canvas.GetLeft(AiFloatingButton)) &&
           !double.IsNaN(System.Windows.Controls.Canvas.GetTop(AiFloatingButton));

    private double GetAiFloatingButtonLeft()
    {
        var left = System.Windows.Controls.Canvas.GetLeft(AiFloatingButton);
        return double.IsNaN(left) ? 0 : left;
    }

    private double GetAiFloatingButtonTop()
    {
        var top = System.Windows.Controls.Canvas.GetTop(AiFloatingButton);
        return double.IsNaN(top) ? 0 : top;
    }

    private void PositionAiFloatingButtonAtDefault()
    {
        if (AiFloatingLayer.ActualWidth <= 0 || AiFloatingLayer.ActualHeight <= 0)
            return;

        SetAiFloatingButtonPosition(
            AiFloatingLayer.ActualWidth - GetAiFloatingButtonWidth() - AIFloatingButtonInset,
            AiFloatingLayer.ActualHeight - GetAiFloatingButtonHeight() - AIFloatingButtonInset);
    }

    private void ClampAiFloatingButtonPosition()
    {
        SetAiFloatingButtonPosition(GetAiFloatingButtonLeft(), GetAiFloatingButtonTop());
    }

    private void SetAiFloatingButtonPosition(double left, double top)
    {
        var maxLeft = Math.Max(0, AiFloatingLayer.ActualWidth - GetAiFloatingButtonWidth());
        var maxTop = Math.Max(0, AiFloatingLayer.ActualHeight - GetAiFloatingButtonHeight());

        System.Windows.Controls.Canvas.SetLeft(AiFloatingButton, Math.Clamp(left, 0, maxLeft));
        System.Windows.Controls.Canvas.SetTop(AiFloatingButton, Math.Clamp(top, 0, maxTop));
    }

    private double GetAiFloatingButtonWidth()
        => AiFloatingButton.ActualWidth > 0 ? AiFloatingButton.ActualWidth : AiFloatingButton.Width;

    private double GetAiFloatingButtonHeight()
        => AiFloatingButton.ActualHeight > 0 ? AiFloatingButton.ActualHeight : AiFloatingButton.Height;

    private void RefreshUserIdentity()
    {
        CurrentUserName = string.IsNullOrWhiteSpace(_userProfile.Current.Username)
            ? "EduAnalytics"
            : _userProfile.Current.Username;
        CurrentUserAvatarText = _userProfile.GetAvatarText();
        UserMenuNameText.Text = CurrentUserName;

        var photoPath = _userProfile.GetPhotoPath();
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            CurrentUserPhoto = null;
            HasCurrentUserPhoto = false;
            return;
        }

        try
        {
            CurrentUserPhoto = LoadBitmap(photoPath);
            HasCurrentUserPhoto = true;
        }
        catch (Exception ex)
        {
            CurrentUserPhoto = null;
            HasCurrentUserPhoto = false;
            _log.Warning("User.Profile", $"Profil fotoğrafı yüklenemedi. {ex.Message}");
        }
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

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.KeyboardShortcutsDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenUserSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UserMenuToggle.IsChecked = false;
            var dlg = App.Services.GetRequiredService<Views.UserSettingsDialog>();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                RefreshUserIdentity();
                _viewModel.Toasts.Success("Kullanıcı ayarları kaydedildi", "Giriş ekranı avatarı ve hesap bilgileri güncellendi.");
            }
        }
        catch (Exception ex)
        {
            _log.Error("User.Settings", "User settings dialog failed to open.", ex);
            _viewModel.Toasts.Error("Kullanıcı ayarları açılamadı", ex.Message);
            AppMessageBox.Show(
                this,
                $"Kullanıcı ayarları açılırken hata oluştu:\n\n{ex.GetType().Name}: {ex.Message}",
                "Kullanıcı Ayarları",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenAISettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Views.AIAssistantSettingsDialog { Owner = this };
            if (dlg.ShowDialog() == true)
                _viewModel.AIAssistant.RefreshConfigStatus();
        }
        catch (Exception ex)
        {
            _log.Error("AI.Settings", "AI settings dialog failed to open.", ex);
            _viewModel.Toasts.Error("AI ayarları açılamadı", ex.Message);
            AppMessageBox.Show(
                this,
                $"AI ayarları açılırken hata oluştu:\n\n{ex.GetType().Name}: {ex.Message}",
                "AI Ayarları",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        UserMenuToggle.IsChecked = false;

        var result = AppMessageBox.Show(
            this,
            "Hesaptan çıkış yapmak istiyor musunuz?",
            "Hesaptan Çıkış",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        _log.Info("User.Session", "User logged out from account menu.");
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Hide();

        var loginWindow = App.Services.GetRequiredService<Views.LoginWindow>();
        var loginResult = loginWindow.ShowDialog();

        if (loginResult == true)
        {
            RefreshUserIdentity();
            Application.Current.MainWindow = this;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Show();
            Activate();
            _log.Info("User.Session", "User logged in again after logout.");
            return;
        }

        Application.Current.Shutdown();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow(object sender, RoutedEventArgs e)
        => Close();
}

/// <summary>WPF GridLength animator (built-in DoubleAnimation works only on Double DP).</summary>
internal class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    public GridLength From { get; set; }
    public GridLength To { get; set; }
    public IEasingFunction? EasingFunction { get; set; }

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        var progress = clock.CurrentProgress ?? 0;
        if (EasingFunction != null) progress = EasingFunction.Ease(progress);
        var fromVal = From.Value;
        var toVal = To.Value;
        var current = fromVal + (toVal - fromVal) * progress;
        return new GridLength(current, GridUnitType.Pixel);
    }
}
