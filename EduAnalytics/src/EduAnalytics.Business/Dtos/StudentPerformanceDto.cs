namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Tek bir öğrencinin bir sınavdaki performansını temsil eder.
/// </summary>
public class StudentPerformanceDto
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string ClassName { get; set; } = null!;

    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public int EmptyAnswers { get; set; }
    public decimal TotalScore { get; set; }                    // Toplam kazanılan puan
    public decimal MaxPossibleScore { get; set; }              // Tam puan
    public double SuccessRate { get; set; }                    // 0-100 (TotalScore/MaxPossibleScore)
    public int ClassRank { get; set; }                         // Sınıf sıralaması (1 = en yüksek)

    /// <summary>
    /// Bu öğrencinin zayıf olduğu öğrenim çıktıları (başarı oranı %50'nin altında olanlar).
    /// </summary>
    public List<string> WeakLearningOutcomes { get; set; } = new();

    public List<string> WeakTopics
    {
        get => WeakLearningOutcomes;
        set => WeakLearningOutcomes = value;
    }
}
