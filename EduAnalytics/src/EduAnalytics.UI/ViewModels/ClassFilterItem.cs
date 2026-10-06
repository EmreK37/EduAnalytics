using CommunityToolkit.Mvvm.ComponentModel;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Sınav oluştururken birden fazla sınıf/bölüm seçebilmek için kullanılan model.
/// "Yazılım - 3.Sınıf", "Bilgisayar - 4.Sınıf" gibi etiketler ListBox'ta CheckBox ile listelenir.
/// </summary>
public partial class ClassFilterItem : ObservableObject
{
    public string ClassName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
