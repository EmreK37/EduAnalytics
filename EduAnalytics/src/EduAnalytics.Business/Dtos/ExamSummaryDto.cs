namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Bir sınavın genel istatistiklerini temsil eder.
/// </summary>
public class ExamSummaryDto
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = null!;
    public DateTime ExamDate { get; set; }
    public string CourseName { get; set; } = null!;
    public int TotalStudents { get; set; }
    public int TotalQuestions { get; set; }
    public int MultipleChoiceCount { get; set; }
    public int OpenEndedCount { get; set; }

    public decimal MaxPossibleScore { get; set; }      // Tüm soruların MaxPoints toplamı
    public decimal AverageScore { get; set; }          // Öğrencilerin ortalama puanı
    public double AverageSuccessRate { get; set; }     // Yüzdelik ortalama (0-100)
    public decimal HighestScore { get; set; }          // En yüksek puan
    public decimal LowestScore { get; set; }           // En düşük puan

    // Dashboard hızlı analiz kartları
    public double MedianScore { get; set; }
    public double CronbachAlpha { get; set; }
    public string CronbachAlphaInterpretation { get; set; } = string.Empty;
    public double PassingScore { get; set; }
    public double PassRate { get; set; }
    public double AverageDifficultyIndex { get; set; }
    public double AverageDiscriminationIndex { get; set; }
    public string LowestLearningOutcomeName { get; set; } = string.Empty;
    public double LowestLearningOutcomeSuccessRate { get; set; }
}
