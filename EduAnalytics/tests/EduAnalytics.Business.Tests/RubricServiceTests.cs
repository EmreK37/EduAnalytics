using EduAnalytics.Business.Dtos;
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
    public class RubricServiceTests : IDisposable
    {
        private readonly EduAnalyticsDbContext _context;
        private readonly RubricService _service;

        public RubricServiceTests()
        {
            var options = new DbContextOptionsBuilder<EduAnalyticsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new EduAnalyticsDbContext(options);
            _service = new RubricService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task SetCriteriaAsync_ShouldSaveCriteriaWithCorrectOrder()
        {
            // Arrange
            int questionId = 1;
            var q = new Question { Id = questionId, QuestionText = "Test Q", Type = QuestionType.OpenEnded, MaxPoints = 20 };
            _context.Questions.Add(q);
            await _context.SaveChangesAsync();

            var newCriteria = new List<RubricCriterionCreateModel>
            {
                new RubricCriterionCreateModel { Title = "Syntax", MaxPoints = 10, Order = 0 },
                new RubricCriterionCreateModel { Title = "Logic", MaxPoints = 10, Order = 0 }
            };

            // Act
            await _service.SetCriteriaAsync(questionId, newCriteria);

            // Assert
            var saved = await _context.QuestionRubricCriteria.Where(c => c.QuestionId == questionId).ToListAsync();
            Assert.Equal(2, saved.Count);
            Assert.Contains(saved, c => c.Title == "Syntax" && c.Order == 1);
            Assert.Contains(saved, c => c.Title == "Logic" && c.Order == 2);
        }

        [Fact]
        public async Task SaveStudentGradeAsync_ShouldCreateStudentAnswerAndAssignScores()
        {
            // Arrange
            int examId = 1;
            int questionId = 1;
            int studentId = 1;
            
            var q = new Question { Id = questionId, QuestionText = "Test Q", Type = QuestionType.OpenEnded, MaxPoints = 20 };
            var criterion1 = new QuestionRubricCriterion { Id = 1, QuestionId = questionId, Title = "A", MaxPoints = 10 };
            var criterion2 = new QuestionRubricCriterion { Id = 2, QuestionId = questionId, Title = "B", MaxPoints = 10 };

            _context.Questions.Add(q);
            _context.QuestionRubricCriteria.AddRange(criterion1, criterion2);
            await _context.SaveChangesAsync();

            var updates = new List<CriterionScoreUpdate>
            {
                new CriterionScoreUpdate { CriterionId = 1, Score = 8m, Comment = "Good" },
                new CriterionScoreUpdate { CriterionId = 2, Score = 5m, Comment = "Needs Work" }
            };

            // Act
            await _service.SaveStudentGradeAsync(examId, questionId, studentId, updates);

            // Assert
            var answer = await _context.StudentAnswers
                .Include(sa => sa.CriterionScores)
                .FirstOrDefaultAsync(a => a.StudentId == studentId);

            Assert.NotNull(answer);
            Assert.Equal(13m, answer.Score); // 8 + 5
            Assert.True(answer.IsCorrect); // 13 >= 20/2
            
            Assert.Equal(2, answer.CriterionScores.Count);
            Assert.Contains(answer.CriterionScores, c => c.CriterionId == 1 && c.Score == 8m);
            Assert.Contains(answer.CriterionScores, c => c.CriterionId == 2 && c.Score == 5m);
        }
    }
}
