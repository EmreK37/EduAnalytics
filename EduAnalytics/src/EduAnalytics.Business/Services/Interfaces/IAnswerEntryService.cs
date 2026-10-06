using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface IAnswerEntryService
{
    /// <summary>
    /// Sınavın bilgisini, sorularını, öğrencilerini ve mevcut cevaplarını yükler.
    /// </summary>
    Task<AnswerEntryModel> LoadAsync(int examId);

    /// <summary>
    /// Tüm cevapları kaydeder (add-or-update). Eski cevapların üzerine yazılır.
    /// </summary>
    Task SaveAsync(int examId, List<StudentAnswerUpdate> updates);
}
