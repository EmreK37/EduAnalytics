namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Sınav sırasında veya sonrasında bir sorunun iptal edilmesi.
/// İptal edilen soru analizden hariç tutulur ama veri tabanından silinmez.
/// </summary>
public interface IExamCancellationService
{
    Task CancelQuestionAsync(int examId, int questionId, string? reason = null);
    Task RestoreQuestionAsync(int examId, int questionId);
    Task<bool> IsCancelledAsync(int examId, int questionId);
}
