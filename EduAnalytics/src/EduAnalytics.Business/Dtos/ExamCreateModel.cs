using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Yeni bir sınav oluşturmak için UI'dan gelen form verisi.
/// Yeni mantık: Sorular soru bankasına yazılır + ExamQuestion ile sınava bağlanır.
/// </summary>
public class ExamCreateModel
{
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime ExamDate { get; set; }

    /// <summary>Sınav süresi (dakika). 0 = süresiz.</summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Sınav tipi (Quiz / Vize / Final / Bütünleme).</summary>
    public ExamType ExamType { get; set; } = ExamType.Midterm;

    /// <summary>Üretilecek kitapçık sayısı (1-4 arası).</summary>
    public int BookletCount { get; set; } = 1;

    /// <summary>Kitapçıklar arasında şıklar karıştırılsın mı?</summary>
    public bool ShuffleOptions { get; set; } = false;

    public int CreatedByUserId { get; set; }
    public List<QuestionCreateModel> Questions { get; set; } = new();
    public List<StudentCreateModel> Students { get; set; } = new();
}

public class StudentCreateModel
{
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
}

public class QuestionCreateModel
{
    /// <summary>Sınavdaki sıra (eski adı QuestionNumber). UI ile uyumluluk için korunmuştur.</summary>
    public int QuestionNumber { get; set; }
    public QuestionType Type { get; set; }
    public decimal MaxPoints { get; set; } = 1.0m;
    public string QuestionText { get; set; } = null!;

    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string OptionE { get; set; } = string.Empty;
    public OptionLetter CorrectOption { get; set; }

    public string? AnswerKey { get; set; }

    /// <summary>Soru bankasında aktif mi (sınavda kullanılabilir mi)?</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Hocanın favorisi mi?</summary>
    public bool IsFavorite { get; set; } = false;

    public List<int> TopicIds { get; set; } = new();

    /// <summary>Sorunun bağlandığı öğrenim çıktıları (ÖÇ'ler).</summary>
    public List<int> LearningOutcomeIds { get; set; } = new();

    /// <summary>İlişkili soru grubu (common-stem). null = bağımsız soru.</summary>
    public int? QuestionGroupId { get; set; }
}
