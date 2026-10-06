using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace EduAnalytics.UI.ViewModels;

public partial class MyCoursesViewModel : ObservableObject
{
    private readonly IMyCoursesService _service;

    [ObservableProperty]
    private ObservableCollection<MyCourseDetailDto> _courses = new();

    [ObservableProperty]
    private MyCourseDetailDto? _selectedCourse;

    public bool IsCourseSelected => SelectedCourse != null;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public MyCoursesViewModel(IMyCoursesService service)
    {
        _service = service;
    }

    partial void OnSelectedCourseChanged(MyCourseDetailDto? value)
    {
        OnPropertyChanged(nameof(IsCourseSelected));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetMyCoursesAsync();
            Courses = new ObservableCollection<MyCourseDetailDto>(list);
            if (Courses.Any())
            {
                SelectedCourse = Courses.First();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Dersler yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task InspectStudentAsync(StudentDto? student)
    {
        if (student == null || SelectedCourse == null) return;

        try
        {
            var profile = await _service.GetStudentProfileAsync(SelectedCourse.CourseId, student.Id);
            
            var dialog = new StudentProfileDialog(profile);
            
            // Set Owner to main window if possible
            if (Application.Current.MainWindow != null)
                dialog.Owner = Application.Current.MainWindow;

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            EduAnalytics.UI.Services.AppMessageBox.Show($"Öğrenci profili yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
