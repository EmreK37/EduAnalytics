using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Sınav kitapçığının bir öğrenciye / cevap kâğıdına yansıyan görünümü.
/// </summary>
public class BookletDto
{
    public int BookletId { get; set; }
    public string BookletCode { get; set; } = null!;
    public int ExamId { get; set; }
    public List<BookletQuestionDto> Questions { get; set; } = new();
}

public class BookletQuestionDto
{
    public int QuestionId { get; set; }
    public int OrderInBooklet { get; set; }
    public string QuestionText { get; set; } = null!;
    public QuestionType Type { get; set; }

    /// <summary>Kitapçığa basılı şıklar (karıştırılmış sıra).</summary>
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string OptionE { get; set; } = string.Empty;

    /// <summary>"A:C,B:A,C:D,D:B,E:E" — orijinal şık → kitapçıkta görünen şık.</summary>
    public string? OptionShuffleMap { get; set; }
}
