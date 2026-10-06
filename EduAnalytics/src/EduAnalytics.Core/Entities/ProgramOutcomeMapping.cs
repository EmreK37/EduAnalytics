namespace EduAnalytics.Core.Entities;

/// <summary>
/// Program Çıktısı (PÇ) ↔ Öğrenim Çıktısı (ÖÇ) çoka-çok ilişkisi.
/// Bir öğrenim çıktısı birden fazla program çıktısına katkı sağlayabilir.
/// ContributionLevel (1-5): bu ÖÇ'nün PÇ'ye katkı seviyesi.
/// </summary>
public class ProgramOutcomeMapping
{
    public int ProgramOutcomeId { get; set; }
    public int LearningOutcomeId { get; set; }

    /// <summary>1 (zayıf) — 5 (kuvvetli). Default: 3 (orta).</summary>
    public int ContributionLevel { get; set; } = 3;

    // Navigation Properties
    public ProgramOutcome ProgramOutcome { get; set; } = null!;
    public LearningOutcome LearningOutcome { get; set; } = null!;
}
