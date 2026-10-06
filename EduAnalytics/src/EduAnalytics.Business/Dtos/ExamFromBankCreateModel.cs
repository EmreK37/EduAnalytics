using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Sınavı SORU BANKASINDAN seçilmiş sorularla oluşturma modeli.
/// Klasik CreateExamModel'e alternatif: yeni soru üretmez, sadece havuzdan seçer.
/// </summary>
public class ExamFromBankCreateModel
{
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime ExamDate { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public ExamType ExamType { get; set; } = ExamType.Midterm;
    public int BookletCount { get; set; } = 1;
    public bool ShuffleOptions { get; set; } = false;
    public int CreatedByUserId { get; set; }

    /// <summary>Soru bankasından seçilen sorular (sırasıyla).</summary>
    public List<ExamBankQuestionRef> SelectedQuestions { get; set; } = new();

    public List<StudentCreateModel> Students { get; set; } = new();
}

public class ExamBankQuestionRef
{
    public int QuestionId { get; set; }
    public int OrderInExam { get; set; }
    /// <summary>Sınav-bazlı puan override. null = sorunun varsayılan MaxPoints'i.</summary>
    public decimal? OverrideMaxPoints { get; set; }
}
