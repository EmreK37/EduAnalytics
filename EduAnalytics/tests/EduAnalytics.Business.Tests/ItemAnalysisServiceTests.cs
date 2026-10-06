using EduAnalytics.Business.Services.Implementations;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EduAnalytics.Business.Tests
{
    public class ItemAnalysisServiceTests : IDisposable
    {
        private readonly EduAnalyticsDbContext _context;
        private readonly ItemAnalysisService _service;

        public ItemAnalysisServiceTests()
        {
            var options = new DbContextOptionsBuilder<EduAnalyticsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new EduAnalyticsDbContext(options);
            _service = new ItemAnalysisService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnEmptyList_WhenNoQuestionsInExam()
        {
            // Arrange
            int examId = 1;
            
            // Act
            var results = await _service.AnalyzeAsync(examId);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldCalculateDifficultyAndDistractorEffectiveness_WhenDataIsValid()
        {
            // Arrange
            int examId = 1;
            
            var question = new Question 
            { 
                Id = 1, 
                QuestionText = "Test Q1", 
                Type = QuestionType.MultipleChoice,
                CorrectOption = OptionLetter.A,
                OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D", OptionE = "E"
            };
            
            var examQuestion = new ExamQuestion
            {
                ExamId = examId,
                QuestionId = 1,
                Question = question,
                OrderInExam = 1,
                IsCancelled = false
            };

            _context.Questions.Add(question);
            _context.ExamQuestions.Add(examQuestion);

            // Add 10 students answers. 6 correct (A), 2 wrong (B), 1 wrong (C), 1 Empty
            var answers = new List<StudentAnswer>
            {
                new StudentAnswer { Id = 1, ExamId = examId, QuestionId = 1, StudentId = 1, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 2, ExamId = examId, QuestionId = 1, StudentId = 2, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 3, ExamId = examId, QuestionId = 1, StudentId = 3, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 4, ExamId = examId, QuestionId = 1, StudentId = 4, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 5, ExamId = examId, QuestionId = 1, StudentId = 5, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 6, ExamId = examId, QuestionId = 1, StudentId = 6, SelectedOption = OptionLetter.A, IsCorrect = true },
                new StudentAnswer { Id = 7, ExamId = examId, QuestionId = 1, StudentId = 7, SelectedOption = OptionLetter.B, IsCorrect = false },
                new StudentAnswer { Id = 8, ExamId = examId, QuestionId = 1, StudentId = 8, SelectedOption = OptionLetter.B, IsCorrect = false },
                new StudentAnswer { Id = 9, ExamId = examId, QuestionId = 1, StudentId = 9, SelectedOption = OptionLetter.C, IsCorrect = false },
                new StudentAnswer { Id = 10, ExamId = examId, QuestionId = 1, StudentId = 10, SelectedOption = OptionLetter.Empty, IsCorrect = false }
            };

            _context.StudentAnswers.AddRange(answers);
            await _context.SaveChangesAsync();

            // Act
            var results = await _service.AnalyzeAsync(examId);

            // Assert
            Assert.Single(results);
            var result = results.First();
            
            Assert.Equal(1, result.QuestionId);
            Assert.Equal(10, result.TotalStudents);
            Assert.Equal(6, result.CorrectCount);
            Assert.Equal(3, result.WrongCount); // B, B, C
            Assert.Equal(1, result.EmptyCount);
            
            // Difficulty = 6/10 = 0.6
            Assert.Equal(0.6, result.DifficultyIndex);
            
            // Distractor effectiveness calculation verification
            // B has 2/10 = 20% (effective), C has 1/10 = 10% (effective), D and E have 0%. 
            // Total non-correct options = 4 (B, C, D, E). 
            // Effective ones = 2 (B and C). Effectiveness = 2/4 = 0.5
            Assert.Equal(0.5, result.DistractorEffectivenessIndex);
        }
    }
}
