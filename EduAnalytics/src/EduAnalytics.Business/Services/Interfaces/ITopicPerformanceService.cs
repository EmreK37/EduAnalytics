using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Konu (Bologna çıktısı) bazlı başarı analizi.
/// Bir soru birden fazla konuya bağlı olabilir; bu durumda aynı cevap birden çok konunun istatistiğine sayılır.
/// </summary>
public interface ITopicPerformanceService
{
    /// <summary>
    /// Bir sınavdaki tüm konuların sınıf genelindeki başarı oranlarını döndürür.
    /// </summary>
    Task<List<TopicPerformanceDto>> AnalyzeExamAsync(int examId);

    /// <summary>
    /// Sadece zayıf konuları döndürür (başarı oranı eşiğin altında).
    /// </summary>
    Task<List<TopicPerformanceDto>> GetWeakTopicsAsync(int examId, double threshold = 60.0);
}
