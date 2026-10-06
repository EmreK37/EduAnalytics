using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EduAnalytics.UI.ViewModels;

public partial class StudentRowViewModel : ObservableObject
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;

    /// <summary>Öğrencinin kullandığı kitapçık kodu (A/B/C/D). Tek kitapçıklı sınavlarda otomatik "A".</summary>
    [ObservableProperty]
    private string? _bookletCode;

    /// <summary>BookletCode'a karşılık gelen veritabanı Id'si (lookup ile set edilir).</summary>
    public int? BookletId { get; set; }

    public ObservableCollection<AnswerCellViewModel> Cells { get; set; } = new();
}
