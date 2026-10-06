using System.Collections.Generic;
using System.Threading.Tasks;
using EduAnalytics.Business.Dtos;

namespace EduAnalytics.Business.Services.Interfaces;

public interface ILearningOutcomePerformanceService
{
    Task<List<LearningOutcomePerformanceDto>> AnalyzeExamAsync(int examId);
}
