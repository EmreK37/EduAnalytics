using System.Windows;
using System.Windows.Controls;

namespace EduAnalytics.UI.Views;

public partial class LoadingOverlay : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata("Yükleniyor..."));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }
}
