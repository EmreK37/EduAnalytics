using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Soru bankası: dersin sorularını saklar, filtreler, aktif/favori toggler.
/// Common-stem (ilişkili soru) grupları da bu servis üzerinden yönetilir.
/// </summary>
public interface IQuestionBankService
{
    Task<List<QuestionBankItemDto>> SearchAsync(QuestionBankFilter filter);
    Task<QuestionBankItemDto?> GetByIdAsync(int questionId);
    Task<QuestionBankCreateModel?> GetEditModelAsync(int questionId);

    Task<int> CreateAsync(QuestionBankCreateModel model);
    Task UpdateAsync(int questionId, QuestionBankCreateModel model);
    Task DeleteAsync(int questionId);

    Task ToggleActiveAsync(int questionId);
    Task ToggleFavoriteAsync(int questionId);

    // Common-stem grup işlemleri
    Task<int> CreateGroupAsync(QuestionGroupCreateModel model);
    Task<List<QuestionGroupDto>> GetGroupsForCourseAsync(int courseId);
    Task<List<QuestionBankItemDto>> GetQuestionsForGroupAsync(int groupId);
}
