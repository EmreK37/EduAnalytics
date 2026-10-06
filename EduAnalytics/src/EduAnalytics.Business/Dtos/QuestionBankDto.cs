using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Soru bankasından getirilen tek bir sorunun özet/listeleme görünümü.
/// </summary>
public class QuestionBankItemDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string QuestionText { get; set; } = null!;
    public QuestionType Type { get; set; }
    public decimal MaxPoints { get; set; }
    public OptionLetter CorrectOption { get; set; }
    public bool IsActive { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>İlişkili soru grubu (common-stem) varsa Id'si.</summary>
    public int? QuestionGroupId { get; set; }

    /// <summary>Soruya bağlı öğrenim çıktıları (kod listesi: ÖÇ-1, ÖÇ-3 vb.).</summary>
    public List<string> LearningOutcomeCodes { get; set; } = new();

    /// <summary>Soruya bağlı konuların hafta numaraları (3, 5 vb.).</summary>
    public List<int> TopicWeeks { get; set; } = new();

    /// <summary>Bu soru kaç sınavda kullanıldı?</summary>
    public int UsedInExamCount { get; set; }

    /// <summary>Bu sorunun kullanıldığı sınavların başlıkları.</summary>
    public List<string> UsedInExamTitles { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Soru bankası filtresi. Tüm alanlar opsiyonel — null = filtreleme yok.
/// </summary>
public class QuestionBankFilter
{
    public int? CourseId { get; set; }
    public QuestionType? Type { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFavorite { get; set; }
    public List<int>? LearningOutcomeIds { get; set; }
    public List<int>? TopicIds { get; set; }
    public string? SearchText { get; set; }
}

/// <summary>
/// Yeni soru oluşturma modeli (sınava bağlamadan, doğrudan bankaya).
/// </summary>
public class QuestionBankCreateModel
{
    public int CourseId { get; set; }
    public int? QuestionGroupId { get; set; }
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
    public decimal MaxPoints { get; set; } = 1.0m;
    public string QuestionText { get; set; } = null!;

    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string OptionE { get; set; } = string.Empty;
    public OptionLetter CorrectOption { get; set; }
    public string? AnswerKey { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsFavorite { get; set; } = false;
    public int CreatedByUserId { get; set; }

    public List<int> TopicIds { get; set; } = new();
    public List<int> LearningOutcomeIds { get; set; } = new();
}

/// <summary>
/// Common-stem (ilişkili soru) grubunu oluşturma modeli.
/// </summary>
public class QuestionGroupCreateModel
{
    public int CourseId { get; set; }
    public string StemText { get; set; } = null!;
    public string? MediaPath { get; set; }
    public int CreatedByUserId { get; set; }
}

/// <summary>
/// Common-stem grup özet görünümü.
/// </summary>
public class QuestionGroupDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string StemText { get; set; } = null!;
    public string? MediaPath { get; set; }
    public int QuestionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
