namespace EduAnalytics.Business.Dtos;

public class AdminExportPackageDto
{
    public string Version { get; set; } = "1.0";
    public string Institution { get; set; } = string.Empty;
    public List<AdminExportCourseDto> Courses { get; set; } = new();
    public List<AdminExportStudentDto> Students { get; set; } = new();
    public List<AdminExportTopicDto> Topics { get; set; } = new();
    public List<AdminExportQuestionDto> Questions { get; set; } = new();
}

public class AdminExportCourseDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AdminExportTopicDto
{
    public string CourseCode { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminExportQuestionDto
{
    public string Code { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string? TopicTitle { get; set; }
    public string QuestionType { get; set; } = string.Empty; // MultipleChoice, TrueFalse, FillInTheBlank, Classic
    public List<AdminExportQuestionChoiceDto> Choices { get; set; } = new();
    // For classic questions
    public string? ClassicAnswerKey { get; set; }
    public decimal? ClassicMaxScore { get; set; }
    public List<AdminExportRubricCriteriaDto> RubricCriteria { get; set; } = new();
}

public class AdminExportQuestionChoiceDto
{
    public string Key { get; set; } = string.Empty; // A, B, C...
    public string Content { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class AdminExportRubricCriteriaDto
{
    public string Name { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public int OrderIndex { get; set; }
}

public class AdminExportStudentDto
{
    public string Number { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
}
