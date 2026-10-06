using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Klasik soru rubric (kriter) yönetimi.
/// </summary>
public interface IRubricService
{
    Task<List<RubricCriterionDto>> GetCriteriaAsync(int questionId);

    Task SetCriteriaAsync(int questionId, List<RubricCriterionCreateModel> criteria);

    /// <summary>
    /// Bir öğrencinin belirli sınavda belirli klasik sorudan kriter-bazlı puan dökümünü getirir.
    /// Henüz puanlanmamışsa criteria boş Score'larla döner.
    /// </summary>
    Task<StudentRubricGradeDto> GetStudentGradeAsync(int examId, int questionId, int studentId);

    /// <summary>
    /// Öğrencinin kriter puanlarını kaydeder; toplamı StudentAnswer.Score'a yansıtır.
    /// </summary>
    Task SaveStudentGradeAsync(int examId, int questionId, int studentId, List<CriterionScoreUpdate> updates);
}
