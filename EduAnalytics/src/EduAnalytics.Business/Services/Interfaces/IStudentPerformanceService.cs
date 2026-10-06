using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Öğrenci bazlı performans raporu.
/// </summary>
public interface IStudentPerformanceService
{
    /// <summary>
    /// Bir sınavdaki tüm öğrencilerin performansını (sıralama dahil) döndürür.
    /// </summary>
    Task<List<StudentPerformanceDto>> GetExamRankingAsync(int examId);

    /// <summary>
    /// Tek bir öğrencinin detaylı performans raporunu döndürür.
    /// </summary>
    Task<StudentPerformanceDto?> GetStudentReportAsync(int examId, int studentId);
}
