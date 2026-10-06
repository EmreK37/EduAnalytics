using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface IExamStatisticsService
{
    /// <summary>
    /// Verilen sınav için merkezi eğilim, dağılım, şekil ve iç tutarlılık
    /// istatistiklerini hesaplar. acceptabilityIndex (0–100): geçme eşiği.
    /// </summary>
    Task<ExamStatisticsDto?> ComputeAsync(int examId, double acceptabilityIndex = 50.0);
}
