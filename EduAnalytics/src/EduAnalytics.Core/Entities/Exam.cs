using EduAnalytics.Core.Enums;

namespace EduAnalytics.Core.Entities;

public class Exam
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime ExamDate { get; set; }

    /// <summary>Sınav süresi (dakika). 0 = süresiz.</summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Sınav tipi (Quiz/Vize/Final/Bütünleme). Vize ise konu havuzu o tarihe kadar filtrelenir.</summary>
    public ExamType ExamType { get; set; } = ExamType.Midterm;

    /// <summary>Üretilecek kitapçık sayısı. 1 = tek kitapçık (karıştırma yok), 2-4 = A/B/C/D.</summary>
    public int BookletCount { get; set; } = 1;

    /// <summary>Kitapçıklar arasında şıklar karıştırılsın mı?</summary>
    public bool ShuffleOptions { get; set; } = false;

    public int CreatedByUserId { get; set; }

    // Navigation Properties
    public Course Course { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<ExamBooklet> Booklets { get; set; } = new List<ExamBooklet>();
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
