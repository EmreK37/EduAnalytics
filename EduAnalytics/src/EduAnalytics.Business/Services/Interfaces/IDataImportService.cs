using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface IDataImportService
{
    Task<(int CoursesAdded, int StudentsAdded, int TopicsAdded, int QuestionsAdded)> ImportAdminPackageAsync(AdminExportPackageDto package);
}
