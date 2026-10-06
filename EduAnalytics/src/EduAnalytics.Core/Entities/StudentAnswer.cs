using EduAnalytics.Core.Enums;

namespace EduAnalytics.Core.Entities;

public class StudentAnswer
{
    public int Id { get; set; }

    /// <summary>Hangi sınavın cevabı. Aynı soru farklı sınavlarda kullanılabildiği için bu alan zorunludur.</summary>
    public int ExamId { get; set; }

    public int QuestionId { get; set; }
    public int StudentId { get; set; }

    /// <summary>Öğrenci hangi kitapçıkla cevapladı? null = tek kitapçık.</summary>
    public int? BookletId { get; set; }

    /// <summary>Test sorularında seçilen şık (orijinal şık harfine çevrilmiş hâli). Klasikte Empty.</summary>
    public OptionLetter SelectedOption { get; set; }

    /// <summary>Test: doğru/yanlış. Klasik: Score ≥ MaxPoints/2 ise true.</summary>
    public bool IsCorrect { get; set; }

    /// <summary>Klasik sorularda öğretmenin verdiği puan. Test için null.</summary>
    public decimal? Score { get; set; }

    // Navigation Properties
    public Exam Exam { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public ExamBooklet? Booklet { get; set; }
    public ICollection<StudentAnswerCriterion> CriterionScores { get; set; } = new List<StudentAnswerCriterion>();
}
