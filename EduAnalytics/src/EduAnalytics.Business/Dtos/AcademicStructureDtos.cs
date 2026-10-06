namespace EduAnalytics.Business.Dtos;

// ─── PROGRAM ─────────────────────────────────────────

public class ProgramListDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int CourseCount { get; set; }
    public int OutcomeCount { get; set; }
}

public class ProgramSaveModel
{
    public int? Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

// ─── COURSE ──────────────────────────────────────────

public class CourseListDto
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int TopicCount { get; set; }
    public int LearningOutcomeCount { get; set; }
    public int QuestionCount { get; set; }
    public int ExamCount { get; set; }
}

public class CourseSaveModel
{
    public int? Id { get; set; }
    public int ProgramId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

// ─── TOPIC ───────────────────────────────────────────

public class TopicListDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int WeekNumber { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int LearningOutcomeCount { get; set; }
    public int QuestionCount { get; set; }
}

public class TopicSaveModel
{
    public int? Id { get; set; }
    public int CourseId { get; set; }
    public int WeekNumber { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
}
