using System.Windows;
using EduAnalytics.Business.Services.Interfaces;

namespace EduAnalytics.UI.Views;

public partial class StudentProfileDialog : Window
{
    public StudentProfileDialog(StudentCourseProfileDto profile)
    {
        InitializeComponent();
        
        StudentNameText.Text = profile.FullName;
        StudentNumberText.Text = $"{profile.StudentNumber} - {profile.ClassName}";
        
        if (!string.IsNullOrWhiteSpace(profile.FullName))
        {
            var parts = profile.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) InitialsText.Text = parts[0].Substring(0, 1).ToUpper();
            else if (parts.Length > 1) InitialsText.Text = (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpper();
        }

        if (profile.Exams != null && profile.Exams.Count > 0)
        {
            ExamsList.ItemsSource = profile.Exams;
            EmptyStateText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ExamsList.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
