using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

/// <summary>
/// Akademik yapı (Program / Ders / Konu) yönetimi.
/// Bu üç entity arasında hiyerarşi vardır: Program → Course → Topic.
/// </summary>
public interface IAcademicStructureService
{
    // Programs
    Task<List<ProgramListDto>> GetProgramsAsync();
    Task<int> CreateProgramAsync(ProgramSaveModel model);
    Task UpdateProgramAsync(int id, ProgramSaveModel model);
    Task DeleteProgramAsync(int id);

    // Courses
    Task<List<CourseListDto>> GetCoursesByProgramAsync(int programId);
    Task<int> CreateCourseAsync(CourseSaveModel model, int createdByUserId);
    Task UpdateCourseAsync(int id, CourseSaveModel model);
    Task DeleteCourseAsync(int id);

    // Topics
    Task<List<TopicListDto>> GetTopicsByCourseAsync(int courseId);
    Task<int> CreateTopicAsync(TopicSaveModel model);
    Task UpdateTopicAsync(int id, TopicSaveModel model);
    Task DeleteTopicAsync(int id);
}
