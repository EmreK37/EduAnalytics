namespace EduAnalytics.Core.Entities;

/// <summary>
/// Akademik program (örn. "Tıp Fakültesi", "Bilgisayar Mühendisliği").
/// Çoklu program desteği: her programın kendi ders ve program çıktıları (PÇ) olur.
/// </summary>
public class Program
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    // Navigation Properties
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<ProgramOutcome> ProgramOutcomes { get; set; } = new List<ProgramOutcome>();
}
