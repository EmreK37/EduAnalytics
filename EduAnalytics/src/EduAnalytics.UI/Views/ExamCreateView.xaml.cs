using System.Windows.Controls;

namespace EduAnalytics.UI.Views;

public partial class ExamCreateView : UserControl
{
    public ExamCreateView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.ExamCreateViewModel vm)
                await vm.LoadAsync();
        };
    }
}
