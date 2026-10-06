namespace EduAnalytics.Core.Entities;

/// <summary>
/// Bir öğrencinin bir klasik soruya verdiği cevabın, tek bir kriterden aldığı puan.
/// StudentAnswer.Score = bu kriter puanlarının toplamı (rubric varsa).
/// </summary>
public class StudentAnswerCriterion
{
    public int Id { get; set; }
    public int StudentAnswerId { get; set; }
    public int CriterionId { get; set; }

    /// <summary>0 ile criterion.MaxPoints arası.</summary>
    public decimal Score { get; set; }

    public string? Comment { get; set; }

    // Navigation Properties
    public StudentAnswer StudentAnswer { get; set; } = null!;
    public QuestionRubricCriterion Criterion { get; set; } = null!;
}
