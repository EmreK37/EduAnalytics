namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Öğrenim Çıktısı (ÖÇ) görünümü.
/// </summary>
public class LearningOutcomeDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Bu ÖÇ'ye bağlı konu sayısı.</summary>
    public int TopicCount { get; set; }

    /// <summary>Bu ÖÇ'ye bağlı soru sayısı (havuz).</summary>
    public int QuestionCount { get; set; }

    /// <summary>Bu ÖÇ'nün eşleştirildiği program çıktısı kodları.</summary>
    public List<string> ProgramOutcomeCodes { get; set; } = new();
}

public class LearningOutcomeCreateModel
{
    public int CourseId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<int> TopicIds { get; set; } = new();
    public List<ProgramOutcomeMappingModel> ProgramOutcomeMappings { get; set; } = new();
}

public class ProgramOutcomeMappingModel
{
    public int ProgramOutcomeId { get; set; }
    public int ContributionLevel { get; set; } = 3;
}
