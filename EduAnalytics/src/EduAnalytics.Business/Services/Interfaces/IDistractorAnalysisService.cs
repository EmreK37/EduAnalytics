using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Çeldirici (distractor) analizi: Yanlış yapan öğrencilerin en çok hangi şıkkı seçtiğini tespit eder.
/// Öğretmen, "Soru 4'te C şıkkını yanlış yapanların %83'ü seçti → C çeldirici güçlü" bilgisine ulaşır.
/// </summary>
public interface IDistractorAnalysisService
{
    /// <summary>
    /// Bir sınavdaki tüm soruların çeldirici dahil analizini döndürür.
    /// </summary>
    Task<List<QuestionAnalysisDto>> AnalyzeExamAsync(int examId);

    /// <summary>
    /// Güçlü çeldiricisi olan soruları listeler (çeldirici oranı ≥ %50).
    /// Öğretmen "Dikkat edilmesi gereken sorular" listesi görür.
    /// </summary>
    Task<List<QuestionAnalysisDto>> GetStrongDistractorsAsync(int examId, double minDistractorRate = 50.0);
}
