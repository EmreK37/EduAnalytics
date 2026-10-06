namespace EduAnalytics.Core.Entities;

/// <summary>
/// Kitapçık-bazlı soru sırası ve şık permütasyon haritası.
/// OptionShuffleMap formatı: "A:C,B:A,C:D,D:B,E:E"
///   → Bu kitapçıkta öğrencinin gördüğü A şıkkı, orijinal C şıkkıdır.
/// Cevap analizinde öğrencinin işaretlediği harf bu haritayla orijinal harfe çevrilir.
/// </summary>
public class ExamBookletQuestion
{
    public int Id { get; set; }
    public int BookletId { get; set; }
    public int QuestionId { get; set; }

    /// <summary>Bu kitapçıkta sorunun sırası.</summary>
    public int OrderInBooklet { get; set; }

    /// <summary>�?ık permütasyon haritası. null = şıklar karıştırılmamış.</summary>
    public string? OptionShuffleMap { get; set; }

    // Navigation Properties
    public ExamBooklet Booklet { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
