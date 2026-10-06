using System.Windows.Controls;
using EduAnalytics.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EduAnalytics.UI.Views;

public partial class MyCoursesView : UserControl
{
    public MyCoursesView()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MyCoursesViewModel>();
        DataContext = vm;
        Loaded += async (s, e) => await vm.LoadAsync();
    }
}
