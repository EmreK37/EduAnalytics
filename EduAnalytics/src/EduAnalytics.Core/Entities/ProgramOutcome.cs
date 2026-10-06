namespace EduAnalytics.Core.Entities;

/// <summary>
/// Program Çıktısı (PÇ). Bir programın mezunundan beklenen yetkinlik.
/// Hiyerarşi: ProgramOutcome → LearningOutcome → Question.
/// </summary>
public class ProgramOutcome
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;

    // Navigation Properties
    public Program Program { get; set; } = null!;
    public ICollection<ProgramOutcomeMapping> Mappings { get; set; } = new List<ProgramOutcomeMapping>();
}
