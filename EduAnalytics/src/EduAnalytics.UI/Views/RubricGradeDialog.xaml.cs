using System.Windows;
using EduAnalytics.UI.ViewModels;

namespace EduAnalytics.UI.Views;

public partial class RubricGradeDialog : Window
{
    public RubricGradeDialog()
    {
        InitializeComponent();
    }

    public void Bind(RubricGradeDialogViewModel vm)
    {
        DataContext = vm;
        vm.GradeSaved += () =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
