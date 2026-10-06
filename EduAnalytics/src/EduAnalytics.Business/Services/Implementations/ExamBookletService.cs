using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace EduAnalytics.Business.Services.Implementations;

public class ExamBookletService : IExamBookletService
{
    private static readonly string[] BookletCodes = { "A", "B", "C", "D" };
    private readonly EduAnalyticsDbContext _context;

    public ExamBookletService(EduAnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<List<BookletDto>> GetBookletsForExamAsync(int examId)
    {
        var booklets = await _context.ExamBooklets
            .Where(b => b.ExamId == examId)
            .Include(b => b.BookletQuestions.OrderBy(bq => bq.OrderInBooklet))
                .ThenInclude(bq => bq.Question)
            .OrderBy(b => b.BookletCode)
            .ToListAsync();

        return booklets.Select(MapBooklet).ToList();
    }

    public async Task<BookletDto?> GetBookletAsync(int bookletId)
    {
        var b = await _context.ExamBooklets
            .Include(x => x.BookletQuestions.OrderBy(bq => bq.OrderInBooklet))
                .ThenInclude(bq => bq.Question)
            .FirstOrDefaultAsync(x => x.Id == bookletId);

        return b == null ? null : MapBooklet(b);
    }

    public async Task RegenerateBookletsAsync(int examId, int bookletCount, bool shuffleOptions)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions.OrderBy(eq => eq.OrderInExam))
            .Include(e => e.Booklets).ThenInclude(b => b.BookletQuestions)
            .FirstOrDefaultAsync(e => e.Id == examId)
            ?? throw new InvalidOperationException($"Sınav bulunamadı: {examId}");

        // Mevcut kitapçıklara bağlı StudentAnswer var mı? Varsa silmek tehlikeli
        var hasAnswers = await _context.StudentAnswers
            .AnyAsync(sa => sa.ExamId == examId && sa.BookletId != null);
        if (hasAnswers)
            throw new InvalidOperationException("Bu sınava cevap girilmiş. Kitapçıklar yeniden üretilemez.");

        // Eski kitapçıkları temizle
        foreach (var booklet in exam.Booklets)
            _context.ExamBookletQuestions.RemoveRange(booklet.BookletQuestions);
        _context.ExamBooklets.RemoveRange(exam.Booklets);
        await _context.SaveChangesAsync();

        bookletCount = Math.Clamp(bookletCount, 1, 4);
        exam.BookletCount = bookletCount;
        exam.ShuffleOptions = shuffleOptions;
        await _context.SaveChangesAsync();

        var rng = new Random();
        var newBooklets = new List<ExamBooklet>();
        for (int i = 0; i < bookletCount; i++)
        {
            var b = new ExamBooklet { ExamId = examId, BookletCode = BookletCodes[i] };
            _context.ExamBooklets.Add(b);
            newBooklets.Add(b);
        }
        await _context.SaveChangesAsync();

        var orderedQuestionIds = exam.ExamQuestions
            .OrderBy(eq => eq.OrderInExam)
            .Select(eq => eq.QuestionId)
            .ToList();

        foreach (var booklet in newBooklets)
        {
            var idsForBooklet = booklet.BookletCode == "A"
                ? orderedQuestionIds
                : orderedQuestionIds.OrderBy(_ => rng.Next()).ToList();

            for (int idx = 0; idx < idsForBooklet.Count; idx++)
            {
                _context.ExamBookletQuestions.Add(new ExamBookletQuestion
                {
                    BookletId = booklet.Id,
                    QuestionId = idsForBooklet[idx],
                    OrderInBooklet = idx + 1,
                    OptionShuffleMap = (shuffleOptions && booklet.BookletCode != "A")
                        ? GenerateShuffleMap(rng)
                        : null
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public OptionLetter DecodeStudentChoice(string? optionShuffleMap, OptionLetter displayedChoice)
    {
        // Kitapçıkta karıştırma yoksa, gösterilen şık doğrudan orijinaldir
        if (string.IsNullOrEmpty(optionShuffleMap) || displayedChoice == OptionLetter.Empty)
            return displayedChoice;

        // Map formatı: "A:C,B:A,C:D,D:B,E:E"
        // ↑ Orijinal A şıkkı kitapçıkta C harfine yerleştirilmiş.
        // Öğrenci kitapçıkta C'yi işaretledi → orijinaldeki A'yı seçmiş demektir.
        var mappings = optionShuffleMap.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var m in mappings)
        {
            var parts = m.Split(':');
            if (parts.Length != 2) continue;

            var displayed = parts[1].Trim().ToUpperInvariant();
            if (displayed == displayedChoice.ToString())
            {
                if (Enum.TryParse<OptionLetter>(parts[0].Trim().ToUpperInvariant(), out var original))
                    return original;
            }
        }

        return displayedChoice; // Eşleşme yoksa fallback
    }

    public string? GetReverseMap(string? optionShuffleMap)
    {
        if (string.IsNullOrEmpty(optionShuffleMap)) return null;

        var mappings = optionShuffleMap.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var reversed = new List<string>();
        foreach (var m in mappings)
        {
            var parts = m.Split(':');
            if (parts.Length != 2) continue;
            reversed.Add($"{parts[1].Trim()}:{parts[0].Trim()}");
        }
        return string.Join(",", reversed);
    }

    private static BookletDto MapBooklet(ExamBooklet b)
    {
        return new BookletDto
        {
            BookletId = b.Id,
            BookletCode = b.BookletCode,
            ExamId = b.ExamId,
            Questions = b.BookletQuestions
                .OrderBy(bq => bq.OrderInBooklet)
                .Select(bq => MapBookletQuestion(bq))
                .ToList()
        };
    }

    private static BookletQuestionDto MapBookletQuestion(ExamBookletQuestion bq)
    {
        // Şık karıştırması varsa, kitapçıkta basılan şıkları yeniden sırala
        var q = bq.Question;
        var dto = new BookletQuestionDto
        {
            QuestionId = q.Id,
            OrderInBooklet = bq.OrderInBooklet,
            QuestionText = q.QuestionText,
            Type = q.Type,
            OptionShuffleMap = bq.OptionShuffleMap,
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            OptionE = q.OptionE
        };

        if (!string.IsNullOrEmpty(bq.OptionShuffleMap))
        {
            // Map formatı: "A:C" — orijinal A şıkkı kitapçıkta C konumuna gelir
            var slots = new Dictionary<string, string>
            {
                ["A"] = q.OptionA,
                ["B"] = q.OptionB,
                ["C"] = q.OptionC,
                ["D"] = q.OptionD,
                ["E"] = q.OptionE
            };

            var booklet = new Dictionary<string, string>();
            foreach (var pair in bq.OptionShuffleMap.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(':');
                if (parts.Length != 2) continue;
                var orig = parts[0].Trim().ToUpperInvariant();
                var display = parts[1].Trim().ToUpperInvariant();
                if (slots.TryGetValue(orig, out var text))
                    booklet[display] = text;
            }

            dto.OptionA = booklet.GetValueOrDefault("A", q.OptionA);
            dto.OptionB = booklet.GetValueOrDefault("B", q.OptionB);
            dto.OptionC = booklet.GetValueOrDefault("C", q.OptionC);
            dto.OptionD = booklet.GetValueOrDefault("D", q.OptionD);
            dto.OptionE = booklet.GetValueOrDefault("E", q.OptionE);
        }

        return dto;
    }

    private static string GenerateShuffleMap(Random rng)
    {
        var letters = new[] { "A", "B", "C", "D", "E" };
        var shuffled = letters.OrderBy(_ => rng.Next()).ToArray();
        return string.Join(",", letters.Select((orig, i) => $"{orig}:{shuffled[i]}"));
    }
}
