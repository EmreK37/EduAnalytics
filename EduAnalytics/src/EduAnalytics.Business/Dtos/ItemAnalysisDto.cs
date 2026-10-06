namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Klasik test geliştirme literatüründeki "madde analizi" çıktısı.
/// Tek bir soru için zorluk indeksi (p), ayırt edicilik indeksi (D),
/// madde güvenilirlik indeksi, çeldirici etkinlik indeksi ve
/// üst grup / alt grup doğru sayılarını içerir.
/// </summary>
public class ItemAnalysisDto
{
    public int QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = null!;
    public string CorrectOption { get; set; } = null!;

    // Temel sayılar
    public int TotalStudents { get; set; }      // Sınava giren öğrenci
    public int CorrectCount { get; set; }       // Doğru cevap sayısı
    public int WrongCount { get; set; }         // Yanlış cevap sayısı
    public int EmptyCount { get; set; }         // Boş bırakanlar

    // ─── Zorluk indeksi (p) ───
    /// <summary>p = doğru / toplam.  0..1 arası, 0=çok zor, 1=çok kolay.</summary>
    public double DifficultyIndex { get; set; }
    /// <summary>"Çok Zor / Zor / Orta / Kolay / Çok Kolay" sözel kategorisi.</summary>
    public string DifficultyCategory { get; set; } = string.Empty;

    // ─── Ayırt edicilik indeksi (D) ───
    /// <summary>D = (Üst%27 doğru − Alt%27 doğru) / N_grup.  −1..+1 arası.</summary>
    public double DiscriminationIndex { get; set; }
    public string DiscriminationCategory { get; set; } = string.Empty;
    public int UpperGroupCorrect { get; set; }      // Üst %27 grupta doğru sayan
    public int LowerGroupCorrect { get; set; }      // Alt %27 grupta doğru sayan
    public int GroupSize { get; set; }              // Her grupta kaç öğrenci var

    // ─── Madde güvenilirlik indeksi ───
    /// <summary>r_jx = D × √(p × (1 − p)).  Maddenin teste katkısının ölçüsü.</summary>
    public double ItemReliabilityIndex { get; set; }

    // ─── Çeldirici etkinlik indeksi ───
    /// <summary>Çeldiricilerin toplam etkinliği (0..1). Yüksek = iyi çeldiriciler.</summary>
    public double DistractorEffectivenessIndex { get; set; }
    public List<DistractorDetailDto> Distractors { get; set; } = new();
}

public class DistractorDetailDto
{
    public string Option { get; set; } = null!;       // "A", "B", "C", "D", "E"
    public int SelectedCount { get; set; }
    public double SelectionRate { get; set; }         // 0..100
    public bool IsCorrectOption { get; set; }
    /// <summary>İdeal çeldirici oranı = (1 − p) / (k − 1). Bunun altındaysa zayıf.</summary>
    public double IdealRate { get; set; }
    public bool IsEffective { get; set; }             // En az %5 öğrenci tarafından seçildi mi?
}
