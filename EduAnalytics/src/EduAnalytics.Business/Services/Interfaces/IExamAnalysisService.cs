using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Sınavın genel istatistiklerini hesaplar: ortalama, min, max, başarı oranı.
/// </summary>
public interface IExamAnalysisService
{
    Task<ExamSummaryDto?> GetSummaryAsync(int examId);
    Task<List<ExamSummaryDto>> GetAllExamSummariesAsync();
}
