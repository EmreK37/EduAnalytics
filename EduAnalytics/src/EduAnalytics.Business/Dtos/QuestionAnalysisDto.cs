namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Tek bir sorunun analiz sonucunu temsil eder.
/// Cevap dağılımını ve çeldirici tespitini içerir.
/// </summary>
public class QuestionAnalysisDto
{
    public int QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = null!;
    public string QuestionType { get; set; } = null!;   // "MultipleChoice" | "OpenEnded"
    public decimal MaxPoints { get; set; }
    public string CorrectOption { get; set; } = null!;  // Test için "A" | "B" | "C" | "D", Klasik için "—"

    public int TotalAnswers { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public int EmptyCount { get; set; }
    public double SuccessRate { get; set; }             // 0-100
    public decimal AverageScore { get; set; }           // Bu sorunun ortalama puanı (klasikte anlamlı)

    // Şık bazlı dağılım (kaç kişi A seçti, kaç kişi B seçti vs.)
    public int OptionACount { get; set; }
    public int OptionBCount { get; set; }
    public int OptionCCount { get; set; }
    public int OptionDCount { get; set; }
    public int OptionECount { get; set; }

    // Çeldirici bilgisi (yanlış yapanların en çok seçtiği şık)
    public string? StrongestDistractorOption { get; set; }   // Örn: "C"
    public int StrongestDistractorCount { get; set; }        // Örn: 15
    public double StrongestDistractorRate { get; set; }      // Yanlışlar içinde yüzde (0-100)

    // Bu soru hangi konuları ölçüyor?
    public List<string> LinkedTopicTitles { get; set; } = new();
}
