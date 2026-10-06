namespace EduAnalytics.Core.Entities;

/// <summary>
/// Sınav (Exam) ↔ Soru Bankası (Question) çoka-çok ilişkisi.
/// Soru havuzdan seçilir, ExamQuestion ile sınava bağlanır.
/// Sınav-bazlı puan override ve iptal bilgisi burada tutulur.
/// </summary>
public class ExamQuestion
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int QuestionId { get; set; }

    /// <summary>Sınavda sorunun gösterileceği sıra (1, 2, 3...).</summary>
    public int OrderInExam { get; set; }

    /// <summary>Sınava özgü puan (null ise Question.MaxPoints kullanılır).</summary>
    public decimal? OverrideMaxPoints { get; set; }

    /// <summary>Sınav sırasında bu soru iptal edildi mi? Analizden hariç tutulur.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>İptal sebebi (opsiyonel).</summary>
    public string? CancellationReason { get; set; }

    // Navigation Properties
    public Exam Exam { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
