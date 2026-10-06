using System.Windows.Controls;
using EduAnalytics.UI.ViewModels;

namespace EduAnalytics.UI.Views;

public partial class ProgramOutcomeMappingView : UserControl
{
    public ProgramOutcomeMappingView()
    {
        InitializeComponent();
    }

    private void MatrixContribution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            comboBox.DataContext is not MatrixCell cell ||
            DataContext is not ProgramOutcomeMappingViewModel viewModel)
        {
            return;
        }

        if (!comboBox.IsKeyboardFocusWithin && !comboBox.IsDropDownOpen)
            return;

        if (viewModel.SaveMatrixCellCommand.CanExecute(cell))
            viewModel.SaveMatrixCellCommand.Execute(cell);
    }
}
