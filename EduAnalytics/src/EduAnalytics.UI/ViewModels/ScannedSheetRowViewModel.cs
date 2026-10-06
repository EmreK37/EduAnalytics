using CommunityToolkit.Mvvm.ComponentModel;

namespace EduAnalytics.UI.ViewModels;

/// <summary>Optik okuma inceleme ızgarasında öğrenci seçimi için combo öğesi.</summary>
public class StudentPickItem
{
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Display => $"{StudentNumber} — {FullName}";
}

/// <summary>
/// Taranmış tek bir optik formun inceleme satırı. OCR/OMR sonuçları düzenlenebilir:
/// öğretmen eşleşen öğrenciyi, kitapçığı ve cevap dizisini düzeltebilir.
/// </summary>
public partial class ScannedSheetRowViewModel : ObservableObject
{
    /// <summary>Dosya adı (+ sayfa numarası).</summary>
    public string SourceLabel { get; init; } = string.Empty;

    /// <summary>Hizalama işaretleri bulunamayan sayfalar kaydedilemez.</summary>
    public bool MarkersFound { get; init; }

    /// <summary>PaddleOCR'ın okuduğu ham numara ve ad (referans için gösterilir).</summary>
    public string OcrNumber { get; init; } = string.Empty;
    public string OcrName { get; init; } = string.Empty;

    /// <summary>Eşleştirmenin nasıl yapıldığının açıklaması (birebir / yaklaşık / elle).</summary>
    public string MatchInfo { get; init; } = string.Empty;

    /// <summary>0–1: numara güveni ile cevap güvenlerinin en düşüğü.</summary>
    public double OverallConfidence { get; init; }
    public string ConfidenceDisplay => MarkersFound ? $"%{OverallConfidence * 100:0}" : "—";

    public string WarningsText { get; init; } = string.Empty;

    [ObservableProperty] private StudentPickItem? _selectedStudent;
    [ObservableProperty] private string? _bookletCode;

    /// <summary>Soru başına tek karakter: A–E, '-' boş, '?' çift işaret. Elle düzeltilebilir.</summary>
    [ObservableProperty] private string _answersText = string.Empty;
}
