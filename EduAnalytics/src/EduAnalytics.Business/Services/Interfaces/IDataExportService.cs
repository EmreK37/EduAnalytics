using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface IDataExportService
{
    Task<AdminExportPackageDto> ExportDatabaseAsync();
}
