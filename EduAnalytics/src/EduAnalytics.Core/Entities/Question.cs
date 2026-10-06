using EduAnalytics.Core.Enums;

namespace EduAnalytics.Core.Entities;

/// <summary>
/// Soru bankasındaki bağımsız bir soru. Sınava bağlı değildir; ExamQuestion köprüsüyle
/// bir veya daha fazla sınavda kullanılabilir.
/// </summary>
public class Question
{
    public int Id { get; set; }

    /// <summary>Sorunun ait olduğu ders. Soru bankası ders bazlıdır.</summary>
    public int CourseId { get; set; }

    /// <summary>İlişkili soru grubu (common-stem). null = bağımsız soru.</summary>
    public int? QuestionGroupId { get; set; }

    public string QuestionText { get; set; } = null!;

    /// <summary>Sorunun tipi — test veya klasik.</summary>
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

    /// <summary>Bu sorunun varsayılan tam puanı. Sınav-bazlı override için ExamQuestion.OverrideMaxPoints.</summary>
    public decimal MaxPoints { get; set; } = 1.0m;

    // Test soruları için şıklar (klasik sorularda boş string tutulur)
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string OptionE { get; set; } = string.Empty;

    /// <summary>Test soruları için doğru şık. Klasikte Empty.</summary>
    public OptionLetter CorrectOption { get; set; }

    /// <summary>Klasik soru için cevap anahtarı / notlandırma rehberi. Test soruda null.</summary>
    public string? AnswerKey { get; set; }

    /// <summary>Bu soru sınavda kullanılabilir mi? Pasif sorular havuzda kalır ama seçilemez.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Hocanın favori soruları. Hızlı seçim için filtre.</summary>
    public bool IsFavorite { get; set; } = false;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public Course Course { get; set; } = null!;
    public QuestionGroup? QuestionGroup { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();
    public ICollection<QuestionLearningOutcome> QuestionLearningOutcomes { get; set; } = new List<QuestionLearningOutcome>();
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<ExamBookletQuestion> BookletQuestions { get; set; } = new List<ExamBookletQuestion>();
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
    public ICollection<QuestionRubricCriterion> RubricCriteria { get; set; } = new List<QuestionRubricCriterion>();
}
