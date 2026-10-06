using EduAnalytics.Business.Services.Implementations;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EduAnalytics.Business.Tests
{
    public class ExamAnalysisServiceTests : IDisposable
    {
        private readonly EduAnalyticsDbContext _context;
        private readonly ExamAnalysisService _service;

        public ExamAnalysisServiceTests()
        {
            var options = new DbContextOptionsBuilder<EduAnalyticsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new EduAnalyticsDbContext(options);
            _service = new ExamAnalysisService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public void ComputeScore_ShouldReturnMaxPoints_WhenMultipleChoiceIsCorrect()
        {
            // Arrange
            var q = new Question { Type = QuestionType.MultipleChoice, MaxPoints = 10 };
            var a = new StudentAnswer { IsCorrect = true };

            // Act
            var score = ExamAnalysisService.ComputeScore(q, a);

            // Assert
            Assert.Equal(10m, score);
        }

        [Fact]
        public void ComputeScore_ShouldReturnZero_WhenMultipleChoiceIsWrong()
        {
            // Arrange
            var q = new Question { Type = QuestionType.MultipleChoice, MaxPoints = 10 };
            var a = new StudentAnswer { IsCorrect = false };

            // Act
            var score = ExamAnalysisService.ComputeScore(q, a);

            // Assert
            Assert.Equal(0m, score);
        }

        [Fact]
        public void ComputeScore_ShouldReturnStudentScore_WhenOpenEnded()
        {
            // Arrange
            var q = new Question { Type = QuestionType.OpenEnded, MaxPoints = 20 };
            var a = new StudentAnswer { Score = 15m };

            // Act
            var score = ExamAnalysisService.ComputeScore(q, a);

            // Assert
            Assert.Equal(15m, score);
        }

        [Fact]
        public void ComputeScore_ShouldUseOverrideMaxPoints_WhenProvided()
        {
            // Arrange
            var q = new Question { Type = QuestionType.MultipleChoice, MaxPoints = 10 };
            var a = new StudentAnswer { IsCorrect = true };

            // Act
            var score = ExamAnalysisService.ComputeScore(q, a, 25m);

            // Assert
            Assert.Equal(25m, score);
        }

        [Fact]
        public async Task GetSummaryAsync_ShouldCalculateAveragesCorrectly()
        {
            // Arrange
            var course = new Course { Id = 1, Name = "Math 101", Code = "MAT101" };
            var exam = new Exam { Id = 1, Title = "Midterm", CourseId = 1, Course = course, ExamDate = DateTime.Now };

            var q1 = new Question { Id = 1, QuestionText = "Q1", MaxPoints = 50, Type = QuestionType.MultipleChoice };
            var q2 = new Question { Id = 2, QuestionText = "Q2", MaxPoints = 50, Type = QuestionType.MultipleChoice };

            var eq1 = new ExamQuestion { ExamId = 1, QuestionId = 1, Question = q1, OrderInExam = 1 };
            var eq2 = new ExamQuestion { ExamId = 1, QuestionId = 2, Question = q2, OrderInExam = 2 };

            _context.Courses.Add(course);
            _context.Exams.Add(exam);
            _context.Questions.AddRange(q1, q2);
            _context.ExamQuestions.AddRange(eq1, eq2);

            // Student 1: 100
            // Student 2: 50
            // Student 3: 0
            var answers = new List<StudentAnswer>
            {
                new StudentAnswer { ExamId = 1, QuestionId = 1, StudentId = 1, IsCorrect = true },
                new StudentAnswer { ExamId = 1, QuestionId = 2, StudentId = 1, IsCorrect = true },
                new StudentAnswer { ExamId = 1, QuestionId = 1, StudentId = 2, IsCorrect = true },
                new StudentAnswer { ExamId = 1, QuestionId = 2, StudentId = 2, IsCorrect = false },
                new StudentAnswer { ExamId = 1, QuestionId = 1, StudentId = 3, IsCorrect = false },
                new StudentAnswer { ExamId = 1, QuestionId = 2, StudentId = 3, IsCorrect = false }
            };

            _context.StudentAnswers.AddRange(answers);
            await _context.SaveChangesAsync();

            // Act
            var summary = await _service.GetSummaryAsync(1);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(3, summary.TotalStudents);
            Assert.Equal(100m, summary.MaxPossibleScore);
            Assert.Equal(100m, summary.HighestScore);
            Assert.Equal(0m, summary.LowestScore);
            Assert.Equal(50m, summary.AverageScore);
            Assert.Equal(50.0, summary.AverageSuccessRate);
            Assert.Equal(2, summary.MultipleChoiceCount);
        }
    }
}
