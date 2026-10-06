namespace EduAnalytics.Core.Entities;

/// <summary>
/// Sınav kitapçığı (A, B, C, D...). BookletCount &gt; 1 olan sınavlarda farklı kitapçıklarda
/// soru sırası ve şıklar farklı şekilde karışmış olur.
/// </summary>
public class ExamBooklet
{
    public int Id { get; set; }
    public int ExamId { get; set; }

    /// <summary>Kitapçık kodu: "A", "B", "C", "D".</summary>
    public string BookletCode { get; set; } = null!;

    // Navigation Properties
    public Exam Exam { get; set; } = null!;
    public ICollection<ExamBookletQuestion> BookletQuestions { get; set; } = new List<ExamBookletQuestion>();
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
