using EduAnalytics.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.DataAccess.Context;

public class EduAnalyticsDbContext : DbContext
{
    public EduAnalyticsDbContext(DbContextOptions<EduAnalyticsDbContext> options)
        : base(options)
    {
    }

    // Kullanıcı / Akademik yapı
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Program> Programs { get; set; } = null!;
    public DbSet<ProgramOutcome> ProgramOutcomes { get; set; } = null!;
    public DbSet<ProgramOutcomeMapping> ProgramOutcomeMappings { get; set; } = null!;
    public DbSet<Course> Courses { get; set; } = null!;
    public DbSet<Topic> Topics { get; set; } = null!;
    public DbSet<LearningOutcome> LearningOutcomes { get; set; } = null!;
    public DbSet<TopicLearningOutcome> TopicLearningOutcomes { get; set; } = null!;

    // Soru bankası
    public DbSet<QuestionGroup> QuestionGroups { get; set; } = null!;
    public DbSet<Question> Questions { get; set; } = null!;
    public DbSet<QuestionTopic> QuestionTopics { get; set; } = null!;
    public DbSet<QuestionLearningOutcome> QuestionLearningOutcomes { get; set; } = null!;

    // Sınav
    public DbSet<Exam> Exams { get; set; } = null!;
    public DbSet<ExamQuestion> ExamQuestions { get; set; } = null!;
    public DbSet<ExamBooklet> ExamBooklets { get; set; } = null!;
    public DbSet<ExamBookletQuestion> ExamBookletQuestions { get; set; } = null!;

    // Öğrenci
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<StudentCourse> StudentCourses { get; set; } = null!;
    public DbSet<StudentAnswer> StudentAnswers { get; set; } = null!;

    // FAZ 5: Klasik soru rubric'i
    public DbSet<QuestionRubricCriterion> QuestionRubricCriteria { get; set; } = null!;
    public DbSet<StudentAnswerCriterion> StudentAnswerCriteria { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ───────────────────────────────────────
        // USER
        // ───────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ───────────────────────────────────────
        // PROGRAM
        // ───────────────────────────────────────
        modelBuilder.Entity<Program>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.HasIndex(p => p.Code).IsUnique();
        });

        // ───────────────────────────────────────
        // PROGRAM OUTCOME (PÇ)
        // ───────────────────────────────────────
        modelBuilder.Entity<ProgramOutcome>(entity =>
        {
            entity.HasKey(po => po.Id);
            entity.Property(po => po.Code).IsRequired().HasMaxLength(20);
            entity.Property(po => po.Description).IsRequired().HasMaxLength(1000);

            entity.HasOne(po => po.Program)
                  .WithMany(p => p.ProgramOutcomes)
                  .HasForeignKey(po => po.ProgramId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(po => new { po.ProgramId, po.Code }).IsUnique();
        });

        // ───────────────────────────────────────
        // PROGRAM OUTCOME ↔ LEARNING OUTCOME (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<ProgramOutcomeMapping>(entity =>
        {
            entity.HasKey(m => new { m.ProgramOutcomeId, m.LearningOutcomeId });

            entity.HasOne(m => m.ProgramOutcome)
                  .WithMany(po => po.Mappings)
                  .HasForeignKey(m => m.ProgramOutcomeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.LearningOutcome)
                  .WithMany(lo => lo.ProgramOutcomeMappings)
                  .HasForeignKey(m => m.LearningOutcomeId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ───────────────────────────────────────
        // COURSE
        // ───────────────────────────────────────
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Code).IsRequired().HasMaxLength(20);
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.Description).HasMaxLength(1000);

            entity.HasOne(c => c.Program)
                  .WithMany(p => p.Courses)
                  .HasForeignKey(c => c.ProgramId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.CreatedBy)
                  .WithMany(u => u.Courses)
                  .HasForeignKey(c => c.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ───────────────────────────────────────
        // TOPIC
        // ───────────────────────────────────────
        modelBuilder.Entity<Topic>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired().HasMaxLength(300);
            entity.Property(t => t.Description).HasMaxLength(1000);

            entity.HasOne(t => t.Course)
                  .WithMany(c => c.Topics)
                  .HasForeignKey(t => t.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ───────────────────────────────────────
        // LEARNING OUTCOME (Öğrenim Çıktısı)
        // ───────────────────────────────────────
        modelBuilder.Entity<LearningOutcome>(entity =>
        {
            entity.HasKey(lo => lo.Id);
            entity.Property(lo => lo.Code).IsRequired().HasMaxLength(20);
            entity.Property(lo => lo.Name).IsRequired().HasMaxLength(300);
            entity.Property(lo => lo.Description).HasMaxLength(1000);

            entity.HasOne(lo => lo.Course)
                  .WithMany(c => c.LearningOutcomes)
                  .HasForeignKey(lo => lo.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(lo => new { lo.CourseId, lo.Code }).IsUnique();
        });

        // ───────────────────────────────────────
        // TOPIC ↔ LEARNING OUTCOME (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<TopicLearningOutcome>(entity =>
        {
            entity.HasKey(tl => new { tl.TopicId, tl.LearningOutcomeId });

            entity.HasOne(tl => tl.Topic)
                  .WithMany(t => t.TopicLearningOutcomes)
                  .HasForeignKey(tl => tl.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tl => tl.LearningOutcome)
                  .WithMany(lo => lo.TopicLearningOutcomes)
                  .HasForeignKey(tl => tl.LearningOutcomeId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ───────────────────────────────────────
        // QUESTION GROUP (Common-Stem)
        // ───────────────────────────────────────
        modelBuilder.Entity<QuestionGroup>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.StemText).IsRequired();
            entity.Property(g => g.MediaPath).HasMaxLength(500);
            entity.Property(g => g.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(g => g.Course)
                  .WithMany(c => c.QuestionGroups)
                  .HasForeignKey(g => g.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.CreatedBy)
                  .WithMany(u => u.QuestionGroups)
                  .HasForeignKey(g => g.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ───────────────────────────────────────
        // QUESTION (Soru Bankası)
        // ───────────────────────────────────────
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.QuestionText).IsRequired().HasMaxLength(2000);

            entity.Property(q => q.Type)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(q => q.MaxPoints)
                  .IsRequired()
                  .HasColumnType("decimal(6,2)");

            entity.Property(q => q.OptionA).HasMaxLength(500);
            entity.Property(q => q.OptionB).HasMaxLength(500);
            entity.Property(q => q.OptionC).HasMaxLength(500);
            entity.Property(q => q.OptionD).HasMaxLength(500);
            entity.Property(q => q.OptionE).HasMaxLength(500);
            entity.Property(q => q.AnswerKey).HasMaxLength(2000);

            entity.Property(q => q.CorrectOption)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(10);

            entity.Property(q => q.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(q => q.Course)
                  .WithMany(c => c.Questions)
                  .HasForeignKey(q => q.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(q => q.QuestionGroup)
                  .WithMany(g => g.Questions)
                  .HasForeignKey(q => q.QuestionGroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(q => q.CreatedBy)
                  .WithMany(u => u.Questions)
                  .HasForeignKey(q => q.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ───────────────────────────────────────
        // QUESTION ↔ TOPIC (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<QuestionTopic>(entity =>
        {
            entity.HasKey(qt => new { qt.QuestionId, qt.TopicId });

            entity.HasOne(qt => qt.Question)
                  .WithMany(q => q.QuestionTopics)
                  .HasForeignKey(qt => qt.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qt => qt.Topic)
                  .WithMany(t => t.QuestionTopics)
                  .HasForeignKey(qt => qt.TopicId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ───────────────────────────────────────
        // QUESTION ↔ LEARNING OUTCOME (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<QuestionLearningOutcome>(entity =>
        {
            entity.HasKey(ql => new { ql.QuestionId, ql.LearningOutcomeId });

            entity.HasOne(ql => ql.Question)
                  .WithMany(q => q.QuestionLearningOutcomes)
                  .HasForeignKey(ql => ql.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ql => ql.LearningOutcome)
                  .WithMany(l => l.QuestionLearningOutcomes)
                  .HasForeignKey(ql => ql.LearningOutcomeId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ───────────────────────────────────────
        // EXAM
        // ───────────────────────────────────────
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ExamType)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasOne(e => e.Course)
                  .WithMany(c => c.Exams)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                  .WithMany(u => u.Exams)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ───────────────────────────────────────
        // EXAM ↔ QUESTION (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(eq => eq.Id);
            entity.Property(eq => eq.OverrideMaxPoints).HasColumnType("decimal(6,2)");
            entity.Property(eq => eq.CancellationReason).HasMaxLength(500);

            entity.HasOne(eq => eq.Exam)
                  .WithMany(e => e.ExamQuestions)
                  .HasForeignKey(eq => eq.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(eq => eq.Question)
                  .WithMany(q => q.ExamQuestions)
                  .HasForeignKey(eq => eq.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(eq => new { eq.ExamId, eq.QuestionId }).IsUnique();
        });

        // ───────────────────────────────────────
        // EXAM BOOKLET (Kitapçık)
        // ───────────────────────────────────────
        modelBuilder.Entity<ExamBooklet>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.BookletCode).IsRequired().HasMaxLength(5);

            entity.HasOne(b => b.Exam)
                  .WithMany(e => e.Booklets)
                  .HasForeignKey(b => b.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.ExamId, b.BookletCode }).IsUnique();
        });

        // ───────────────────────────────────────
        // EXAM BOOKLET QUESTION
        // ───────────────────────────────────────
        modelBuilder.Entity<ExamBookletQuestion>(entity =>
        {
            entity.HasKey(bq => bq.Id);
            entity.Property(bq => bq.OptionShuffleMap).HasMaxLength(50);

            entity.HasOne(bq => bq.Booklet)
                  .WithMany(b => b.BookletQuestions)
                  .HasForeignKey(bq => bq.BookletId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bq => bq.Question)
                  .WithMany(q => q.BookletQuestions)
                  .HasForeignKey(bq => bq.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(bq => new { bq.BookletId, bq.QuestionId }).IsUnique();
        });

        // ───────────────────────────────────────
        // STUDENT
        // ───────────────────────────────────────
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.StudentNumber).IsRequired().HasMaxLength(20);
            entity.HasIndex(s => s.StudentNumber).IsUnique();
            entity.Property(s => s.FullName).IsRequired().HasMaxLength(150);
            entity.Property(s => s.ClassName).IsRequired().HasMaxLength(50);
        });

        // ───────────────────────────────────────
        // STUDENT ↔ COURSE (M2M)
        // ───────────────────────────────────────
        modelBuilder.Entity<StudentCourse>(entity =>
        {
            entity.HasKey(sc => new { sc.StudentId, sc.CourseId });

            entity.HasOne(sc => sc.Student)
                  .WithMany(s => s.StudentCourses)
                  .HasForeignKey(sc => sc.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sc => sc.Course)
                  .WithMany(c => c.StudentCourses)
                  .HasForeignKey(sc => sc.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ───────────────────────────────────────
        // STUDENT ANSWER
        // ───────────────────────────────────────
        modelBuilder.Entity<StudentAnswer>(entity =>
        {
            entity.HasKey(sa => sa.Id);
            entity.Property(sa => sa.SelectedOption)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(10);

            entity.Property(sa => sa.Score).HasColumnType("decimal(6,2)");

            // Aynı öğrenci aynı sınavda aynı soruya 2 kez cevap veremez
            entity.HasIndex(sa => new { sa.ExamId, sa.QuestionId, sa.StudentId }).IsUnique();

            entity.HasOne(sa => sa.Exam)
                  .WithMany(e => e.StudentAnswers)
                  .HasForeignKey(sa => sa.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sa => sa.Question)
                  .WithMany(q => q.StudentAnswers)
                  .HasForeignKey(sa => sa.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sa => sa.Student)
                  .WithMany(s => s.StudentAnswers)
                  .HasForeignKey(sa => sa.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // BookletId ilişkisini NoAction tutuyoruz: Exam → StudentAnswer cascade'i ile
            // Exam → Booklet → StudentAnswer cascade'inin çakışmasını engeller
            // (SQL Server "multiple cascade paths" kısıtlaması).
            entity.HasOne(sa => sa.Booklet)
                  .WithMany(b => b.StudentAnswers)
                  .HasForeignKey(sa => sa.BookletId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ───────────────────────────────────────
        // QUESTION RUBRIC CRITERION (FAZ 5)
        // ───────────────────────────────────────
        modelBuilder.Entity<QuestionRubricCriterion>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Description).HasMaxLength(1000);
            entity.Property(c => c.MaxPoints).IsRequired().HasColumnType("decimal(6,2)");

            entity.HasOne(c => c.Question)
                  .WithMany(q => q.RubricCriteria)
                  .HasForeignKey(c => c.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.QuestionId, c.Order });
        });

        // ───────────────────────────────────────
        // STUDENT ANSWER CRITERION (FAZ 5)
        // ───────────────────────────────────────
        modelBuilder.Entity<StudentAnswerCriterion>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Score).IsRequired().HasColumnType("decimal(6,2)");
            entity.Property(s => s.Comment).HasMaxLength(500);

            entity.HasOne(s => s.StudentAnswer)
                  .WithMany(sa => sa.CriterionScores)
                  .HasForeignKey(s => s.StudentAnswerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Criterion)
                  .WithMany(c => c.StudentScores)
                  .HasForeignKey(s => s.CriterionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(s => new { s.StudentAnswerId, s.CriterionId }).IsUnique();
        });
    }
}
