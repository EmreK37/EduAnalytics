using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Sınavda soru dağılımının dengeli olup olmadığını kontrol eder.
/// "Hocam 3. ve 5. haftaya yığılmışsın" tarzı uyarılar üretir.
/// </summary>
public interface IExamBalanceCheckService
{
    /// <summary>Mevcut bir sınav için denge raporunu üretir.</summary>
    Task<ExamBalanceReportDto> AnalyzeAsync(int examId);

    /// <summary>
    /// Daha sınav kaydedilmeden önce, seçilen soruların listesi üzerinden
    /// canlı uyarı üretir (UI'da "Kaydetmeden önce göster" için).
    /// </summary>
    Task<ExamBalanceReportDto> AnalyzeDraftAsync(int courseId, List<int> questionIds);
}
