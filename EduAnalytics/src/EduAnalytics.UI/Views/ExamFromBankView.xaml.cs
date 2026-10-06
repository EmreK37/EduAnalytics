using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.UI.ViewModels;

namespace EduAnalytics.UI.Views;

public partial class ExamFromBankView : UserControl
{
    public ExamFromBankView()
    {
        InitializeComponent();
    }

    private void SelectedQuestionPoints_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        CommitSelectedQuestionPoints(sender as TextBox);
        e.Handled = true;
        Keyboard.ClearFocus();
    }

    private void SelectedQuestionPoints_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitSelectedQuestionPoints(sender as TextBox);
    }

    private void CommitSelectedQuestionPoints(TextBox? textBox)
    {
        if (textBox?.DataContext is not QuestionBankItemDto question ||
            DataContext is not ExamFromBankViewModel viewModel)
        {
            return;
        }

        if (!TryParsePoint(textBox.Text, out var points) ||
            !viewModel.TryUpdateSelectedQuestionPoints(question, points))
        {
            textBox.Text = FormatPoint(question.MaxPoints);
            return;
        }

        textBox.Text = FormatPoint(question.MaxPoints);
    }

    private static bool TryParsePoint(string text, out decimal points)
    {
        var normalized = (text ?? string.Empty)
            .Trim()
            .Replace(" ", string.Empty)
            .Replace(",", ".");

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out points);
    }

    private static string FormatPoint(decimal points) =>
        points.ToString("0.##", CultureInfo.CurrentCulture);
}
