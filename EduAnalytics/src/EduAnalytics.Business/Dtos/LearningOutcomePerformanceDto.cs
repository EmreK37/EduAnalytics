namespace EduAnalytics.Business.Dtos;

public class LearningOutcomePerformanceDto
{
    public int LearningOutcomeId { get; set; }
    public string OutcomeName { get; set; } = null!;
    public string? Description { get; set; }

    public int RelatedQuestionCount { get; set; }
    public int TotalAnswers { get; set; }
    public int CorrectAnswers { get; set; }
    public double SuccessRate { get; set; }

    public string PerformanceLevel => SuccessRate switch
    {
        < 40 => "KRİTİK",
        < 60 => "ZAYIF",
        < 80 => "ORTA",
        _ => "İYİ"
    };
}
