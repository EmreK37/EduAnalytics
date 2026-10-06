using CommunityToolkit.Mvvm.ComponentModel;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Arayüzde (UI) öğrencileri Checkbox ile seçebilmek için kullanılan model.
/// </summary>
public partial class StudentSelectionViewModel : ObservableObject
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}