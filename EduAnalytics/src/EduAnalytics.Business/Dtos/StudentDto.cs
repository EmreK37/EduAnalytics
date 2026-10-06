namespace EduAnalytics.Business.Dtos;

/// <summary>
/// Öğrenci yönetimi ekranı için liste/satır görünümü.
/// </summary>
public class StudentDto
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string ClassName { get; set; } = null!;

    /// <summary>Öğrencinin kaydolduğu ders sayısı.</summary>
    public int EnrolledCourseCount { get; set; }
}

/// <summary>Öğrenci ekleme/güncelleme modeli.</summary>
public class StudentSaveModel
{
    public int? Id { get; set; }
    public string StudentNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
}

/// <summary>Excel'den toplu yükleme sonucu.</summary>
public class StudentImportResult
{
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}
