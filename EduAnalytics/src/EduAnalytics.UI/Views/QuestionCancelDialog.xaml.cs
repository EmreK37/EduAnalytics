using System.Windows;

namespace EduAnalytics.UI.Views;

public partial class QuestionCancelDialog : Window
{
    public string Reason { get; private set; } = string.Empty;

    public string QuestionInfo
    {
        set => QuestionInfoText.Text = value;
    }

    public QuestionCancelDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        Reason = ReasonBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
