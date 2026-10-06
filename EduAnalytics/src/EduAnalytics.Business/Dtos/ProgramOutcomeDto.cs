namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Program Çıktısı (PÇ) görünümü.
/// </summary>
public class ProgramOutcomeDto
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>Bu PÇ'ye katkı sağlayan ÖÇ sayısı.</summary>
    public int LearningOutcomeCount { get; set; }
}

/// <summary>
/// Program → Ders → ÖÇ → Soru hiyerarşik raporu.
/// "Bu programın PÇ-3'üne hangi ÖÇ'ler ne kadar katkı sağlıyor, hangi sorular bu ÖÇ'leri ölçüyor?"
/// </summary>
public class ProgramOutcomeReportDto
{
    public int ProgramOutcomeId { get; set; }
    public string ProgramOutcomeCode { get; set; } = null!;
    public string ProgramOutcomeDescription { get; set; } = null!;

    public List<LinkedLearningOutcomeDto> LinkedLearningOutcomes { get; set; } = new();

    /// <summary>Bu PÇ'ye bağlı tüm ÖÇ'lerin sınavlardaki ortalama başarısı.</summary>
    public double OverallSuccessRate { get; set; }

    public int TotalQuestionCount { get; set; }
    public int TotalAnswerCount { get; set; }
}

public class LinkedLearningOutcomeDto
{
    public int LearningOutcomeId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int ContributionLevel { get; set; }
    public int QuestionCount { get; set; }
    public double SuccessRate { get; set; }
}

public class ProgramOutcomeCreateModel
{
    public int ProgramId { get; set; }
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>
/// PÇ-ÖÇ eşleştirme ekranı için, bir PÇ'ye bağlı ÖÇ'nün özet bilgileri + katkı seviyesi.
/// </summary>
public class MappedLearningOutcomeDto
{
    public int LearningOutcomeId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int ContributionLevel { get; set; }
}
