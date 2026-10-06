using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace EduAnalytics.UI.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => StartLoaderAnimation();
    }

    private void StartLoaderAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = -60,
            To = 210,
            Duration = TimeSpan.FromMilliseconds(2000),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        LoaderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    public Task FadeOutAndCloseAsync()
    {
        var tcs = new TaskCompletionSource();
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) => { Close(); tcs.SetResult(); };
        BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }
}
