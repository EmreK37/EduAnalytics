using System.Windows;
using EduAnalytics.UI.ViewModels;

namespace EduAnalytics.UI.Views;

public partial class QuestionEditDialog : Window
{
    public QuestionEditDialog(QuestionEditDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Closed += saved =>
        {
            DialogResult = saved;
            Close();
        };
    }
}
