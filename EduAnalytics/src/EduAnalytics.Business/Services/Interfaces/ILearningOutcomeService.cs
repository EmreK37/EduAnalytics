using EduAnalytics.Business.Dtos;
using EduAnalytics.Core.Enums;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Öğrenim Çıktısı (ÖÇ) yönetimi. Vize sınavlarında tarihe kadar olan ÖÇ'leri filtreler.
/// </summary>
public interface ILearningOutcomeService
{
    Task<List<LearningOutcomeDto>> GetByCourseAsync(int courseId);

    /// <summary>
    /// Sınav tarih ve tipine göre seçilebilecek ÖÇ'leri döndürür.
    /// Vize: Topic.WeekNumber'ı sınav haftasına kadar olan ÖÇ'ler.
    /// Final: tüm ÖÇ'ler.
    /// Quiz: opsiyonel cutoff hafta verilebilir.
    /// </summary>
    Task<List<LearningOutcomeDto>> GetAvailableForExamAsync(int courseId, ExamType examType, int? cutoffWeek = null);

    Task<int> CreateAsync(LearningOutcomeCreateModel model);
    Task UpdateAsync(int learningOutcomeId, LearningOutcomeCreateModel model);
    Task DeleteAsync(int learningOutcomeId);

    Task LinkToTopicAsync(int learningOutcomeId, int topicId);
    Task UnlinkFromTopicAsync(int learningOutcomeId, int topicId);
}
