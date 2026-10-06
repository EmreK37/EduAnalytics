using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Sınav yönetimi listesinde gösterilecek özet sınav kartı.
/// </summary>
public class ExamListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int CourseId { get; set; }
    public DateTime ExamDate { get; set; }
    public int DurationMinutes { get; set; }
    public ExamType ExamType { get; set; }
    public int BookletCount { get; set; }
    public bool ShuffleOptions { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalAnswers { get; set; }
    public List<ExamListQuestionDto> Questions { get; set; } = new();

    /// <summary>True ise bu sınava zaten cevap girilmiş.</summary>
    public bool HasAnswers => TotalAnswers > 0;
}

public class ExamListQuestionDto
{
    public int QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = null!;
    public QuestionType Type { get; set; }
    public decimal MaxPoints { get; set; }
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Mevcut bir sınavın temel metadatasını güncellemek için kullanılır.
/// Soru ekleme/çıkarma bu DTO ile yapılmaz.
/// </summary>
public class ExamUpdateModel
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime ExamDate { get; set; }
    public int DurationMinutes { get; set; }
    public ExamType ExamType { get; set; }
}
