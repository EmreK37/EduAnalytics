using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface IItemAnalysisService
{
    /// <summary>
    /// Bir sınavdaki tüm test sorularının madde analizi listesini döner.
    /// Her madde için zorluk indeksi (p), ayırt edicilik indeksi (D),
    /// madde güvenilirlik indeksi, çeldirici etkinlik indeksi ve
    /// üst/alt grup doğru sayıları üretilir.
    /// </summary>
    Task<List<ItemAnalysisDto>> AnalyzeAsync(int examId);
}
