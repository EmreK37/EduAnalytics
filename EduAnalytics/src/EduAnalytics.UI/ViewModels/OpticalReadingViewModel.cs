using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Enums;
using EduAnalytics.Ocr;
using EduAnalytics.Ocr.Models;
using EduAnalytics.Ocr.Reading;
using EduAnalytics.UI.Services;
using Microsoft.Win32;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Optik Okuma ekranı:
///   1) Sınav seç → yazdırılabilir optik form PDF'i üret,
///   2) Taranmış formları (PDF/JPEG/PNG) yükle → PaddleOCR ad-soyad/numara + OMR cevaplar,
///   3) İnceleme ızgarasında düşük güvenli okumaları düzelt,
///   4) Kaydet → mevcut cevap girişi servisi üzerinden (kitapçık şık çözme dahil) veritabanına yaz.
/// </summary>
public partial class OpticalReadingViewModel : ObservableObject
{
    private readonly IExamCrudService _examCrud;
    private readonly IAnswerEntryService _answerEntry;
    private readonly IExamBookletService _booklets;
    private readonly IOpticalFormReader _reader;
    private readonly IAppLogService _log;

    private AnswerEntryModel? _model;

    /// <summary>Formdaki satır sırasıyla çoktan seçmeli sorular (klasik sorular formda yer almaz).</summary>
    private List<AnswerEntryQuestion> _mcQuestions = new();

    /// <summary>
    /// Kitapçık Id → o kitapçıktaki çoktan seçmeli soruların OrderInBooklet sırasıyla QuestionId'leri.
    /// B/C/D kitapçıklarında soru sırası karıştırıldığı için form satırı i'nin hangi soruya
    /// karşılık geldiği öğrencinin kitapçığına bağlıdır; kayıt bu listeyle eşlenir.
    /// </summary>
    private Dictionary<int, List<int>> _bookletMcOrder = new();

    private Dictionary<string, int> _bookletCodeToId = new();

    [ObservableProperty] private ObservableCollection<ExamListItemDto> _exams = new();
    [ObservableProperty] private ExamListItemDto? _selectedExam;

    [ObservableProperty] private ObservableCollection<ScannedSheetRowViewModel> _sheets = new();
    [ObservableProperty] private ObservableCollection<StudentPickItem> _matchableStudents = new();
    [ObservableProperty] private ObservableCollection<string> _availableBookletCodes = new();

    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _processingStatus = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    [ObservableProperty] private string _examInfo = string.Empty;
    [ObservableProperty] private bool _hasExamLoaded;

    public OpticalReadingViewModel(
        IExamCrudService examCrud,
        IAnswerEntryService answerEntry,
        IExamBookletService booklets,
        IOpticalFormReader reader,
        IAppLogService log)
    {
        _examCrud = examCrud;
        _answerEntry = answerEntry;
        _booklets = booklets;
        _reader = reader;
        _log = log;
    }

    public async Task LoadAsync()
    {
        try
        {
            var exams = await _examCrud.GetAllExamsAsync();
            Exams = new ObservableCollection<ExamListItemDto>(exams);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sınavlar yüklenemedi: {ex.Message}";
            _log.Error("OptikOkuma", "Sınav listesi yüklenemedi", ex);
        }
    }

    partial void OnSelectedExamChanged(ExamListItemDto? value)
    {
        _ = LoadExamAsync(value);
    }

    private async Task LoadExamAsync(ExamListItemDto? exam)
    {
        Sheets.Clear();
        MatchableStudents.Clear();
        AvailableBookletCodes.Clear();
        ErrorMessage = null;
        SuccessMessage = null;
        HasExamLoaded = false;
        ExamInfo = string.Empty;
        _model = null;
        _mcQuestions = new List<AnswerEntryQuestion>();
        _bookletMcOrder = new Dictionary<int, List<int>>();
        _bookletCodeToId = new Dictionary<string, int>();

        if (exam == null)
            return;

        try
        {
            _model = await _answerEntry.LoadAsync(exam.Id);
            _mcQuestions = _model.Questions
                .Where(q => q.Type == QuestionType.MultipleChoice)
                .OrderBy(q => q.QuestionNumber)
                .ToList();

            foreach (var s in _model.Students)
            {
                MatchableStudents.Add(new StudentPickItem
                {
                    StudentId = s.StudentId,
                    StudentNumber = s.StudentNumber,
                    FullName = s.FullName
                });
            }

            foreach (var b in _model.AvailableBooklets)
            {
                _bookletCodeToId[b.BookletCode] = b.BookletId;
                AvailableBookletCodes.Add(b.BookletCode);
            }

            // Soru sırası kitapçıklarda karıştırılabildiğinden her kitapçığın kendi sırası yüklenir.
            if (_model.AvailableBooklets.Count > 1)
            {
                var mcIds = _mcQuestions.Select(q => q.QuestionId).ToHashSet();
                foreach (var booklet in await _booklets.GetBookletsForExamAsync(exam.Id))
                {
                    _bookletMcOrder[booklet.BookletId] = booklet.Questions
                        .Where(q => mcIds.Contains(q.QuestionId))
                        .OrderBy(q => q.OrderInBooklet)
                        .Select(q => q.QuestionId)
                        .ToList();
                }
            }

            var classicCount = _model.Questions.Count - _mcQuestions.Count;
            ExamInfo = $"{_mcQuestions.Count} çoktan seçmeli soru" +
                       (classicCount > 0 ? $" ({classicCount} klasik soru formda yer almaz)" : "") +
                       $" • {MatchableStudents.Count} kayıtlı öğrenci" +
                       (_model.AvailableBooklets.Count > 1 ? $" • {_model.AvailableBooklets.Count} kitapçık" : "");

            if (_mcQuestions.Count == 0)
                ErrorMessage = "Bu sınavda çoktan seçmeli soru yok; optik form üretilemez.";
            else if (_mcQuestions.Count > OpticalFormTemplate.MaxQuestions)
                ErrorMessage = $"Optik form en fazla {OpticalFormTemplate.MaxQuestions} soru destekler; " +
                               $"bu sınavda {_mcQuestions.Count} çoktan seçmeli soru var.";
            else if (_bookletMcOrder.Values.Any(order => order.Count != _mcQuestions.Count))
                ErrorMessage = "Kitapçıkların soru listesi sınavın çoktan seçmeli sorularıyla uyuşmuyor; " +
                               "kitapçıkları yeniden üretin.";
            else
                HasExamLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sınav yüklenemedi: {ex.Message}";
            _log.Error("OptikOkuma", $"Sınav {exam.Id} yüklenemedi", ex);
        }
    }

    private OpticalFormSpec BuildSpec()
    {
        if (_model == null)
            throw new InvalidOperationException("Önce sınav seçilmelidir.");

        // Çok kitapçıklı sınavda soru sırası kitapçığa göre değiştiğinden ana soru numarası
        // basılamaz; satırlar 1..N konum numarasıyla basılır ve kayıtta öğrencinin kitapçık
        // sırasına eşlenir. Tek kitapçıkta ana numaralar aynen kullanılır.
        var multiBooklet = _model.AvailableBooklets.Count > 1;

        return new OpticalFormSpec
        {
            ExamTitle = _model.ExamTitle,
            CourseName = _model.CourseName,
            ExamDate = SelectedExam?.ExamDate,
            QuestionNumbers = multiBooklet
                ? Enumerable.Range(1, _mcQuestions.Count).ToList()
                : _mcQuestions.Select(q => q.QuestionNumber).ToList(),
            BookletCodes = _model.AvailableBooklets.Select(b => b.BookletCode).ToList()
        };
    }

    // ─────────────────────────── Form üretimi ───────────────────────────

    [RelayCommand]
    private void GenerateForm()
    {
        if (!HasExamLoaded || _model == null)
        {
            ErrorMessage = "Önce bir sınav seçin.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF Dosyası|*.pdf",
            FileName = $"OptikForm_{Sanitize(_model.ExamTitle)}.pdf"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var pdf = OpticalFormPdfGenerator.Generate(BuildSpec());
            File.WriteAllBytes(dialog.FileName, pdf);
            SuccessMessage = $"✓ Optik form oluşturuldu: {Path.GetFileName(dialog.FileName)}. " +
                             "Bu formu çoğaltıp sınavda kullanın; taramada aynı şablon okunur.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Form üretilemedi: {ex.Message}";
            _log.Error("OptikOkuma", "Optik form PDF üretimi başarısız", ex);
        }
    }

    // ─────────────────────────── Tarama okuma ───────────────────────────

    [RelayCommand]
    private async Task ImportScansAsync()
    {
        if (!HasExamLoaded || _model == null)
        {
            ErrorMessage = "Önce bir sınav seçin.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Taranmış Formlar|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            return;

        ErrorMessage = null;
        SuccessMessage = null;
        IsProcessing = true;

        var spec = BuildSpec();
        var candidates = MatchableStudents
            .Select(s => new StudentCandidate(s.StudentId, s.StudentNumber, s.FullName))
            .ToList();

        var failures = new List<string>();
        try
        {
            var fileIndex = 0;
            foreach (var file in dialog.FileNames)
            {
                fileIndex++;
                ProcessingStatus = $"Okunuyor ({fileIndex}/{dialog.FileNames.Length}): {Path.GetFileName(file)}";

                try
                {
                    var pages = await Task.Run(() => _reader.ReadFile(file, spec));
                    foreach (var (label, result) in pages)
                        Sheets.Add(BuildRow(label, result, spec, candidates));
                }
                catch (Exception ex)
                {
                    // Bozuk tek dosya partiyi durdurmasın; kalan dosyalar okunmaya devam eder.
                    failures.Add($"{Path.GetFileName(file)} ({ex.Message})");
                    _log.Error("OptikOkuma", $"Tarama dosyası okunamadı: {file}", ex);
                }
            }

            var unreadable = Sheets.Count(s => !s.MarkersFound);
            var unmatched = Sheets.Count(s => s.MarkersFound && s.SelectedStudent == null);
            SuccessMessage = $"✓ {Sheets.Count} sayfa okundu." +
                             (unmatched > 0 ? $" {unmatched} sayfada öğrenci elle seçilmeli." : "") +
                             (unreadable > 0 ? $" {unreadable} sayfa form olarak tanınamadı." : "");
            if (failures.Count > 0)
                ErrorMessage = $"Okunamayan dosyalar: {string.Join(", ", failures)}";
        }
        finally
        {
            IsProcessing = false;
            ProcessingStatus = string.Empty;
        }
    }

    private ScannedSheetRowViewModel BuildRow(
        string label, OpticalSheetResult result, OpticalFormSpec spec, List<StudentCandidate> candidates)
    {
        if (!result.MarkersFound)
        {
            return new ScannedSheetRowViewModel
            {
                SourceLabel = label,
                MarkersFound = false,
                MatchInfo = "Okunamadı",
                WarningsText = string.Join(" ", result.Warnings)
            };
        }

        var (studentId, kind) = StudentMatcher.Match(result.RawStudentNumber, result.RawStudentName, candidates);
        var matchInfo = kind switch
        {
            MatchKind.ExactNumber => "Numara birebir eşleşti",
            MatchKind.FuzzyNumber => "Numara yaklaşık eşleşti — kontrol edin",
            MatchKind.NameOnly => "Yalnızca ad benzerliği — kontrol edin",
            _ => "Eşleşmedi — elle seçin"
        };

        var bookletCode = result.BookletIndex.HasValue
            ? spec.BookletCodes[result.BookletIndex.Value]
            : AvailableBookletCodes.Count == 1 ? AvailableBookletCodes[0] : null;

        var answersText = string.Concat(result.Answers.Select(a =>
            a.MultipleMarks ? '?'
            : a.Option == OptionLetter.Empty ? '-'
            : a.Option.ToString()[0]));

        // Boş bırakılan satırların "boşluk güveni" farklı bir ölçüdür; sayfanın genel güvenini
        // yalnızca işaretli (veya çift işaretli) satırlar belirler.
        var marked = result.Answers.Where(a => a.MultipleMarks || a.Option != OptionLetter.Empty).ToList();
        var answerConfidence = marked.Count > 0 ? marked.Min(a => a.Confidence) : 0;
        var overall = result.RawStudentNumber.Length > 0
            ? Math.Min(result.StudentNumberConfidence, answerConfidence)
            : answerConfidence;

        return new ScannedSheetRowViewModel
        {
            SourceLabel = label,
            MarkersFound = true,
            OcrNumber = result.RawStudentNumber,
            OcrName = result.RawStudentName,
            MatchInfo = matchInfo,
            OverallConfidence = overall,
            WarningsText = string.Join(" ", result.Warnings),
            SelectedStudent = studentId.HasValue
                ? MatchableStudents.FirstOrDefault(s => s.StudentId == studentId.Value)
                : null,
            BookletCode = bookletCode,
            AnswersText = answersText
        };
    }

    [RelayCommand]
    private void RemoveSheet(ScannedSheetRowViewModel? row)
    {
        if (row != null)
            Sheets.Remove(row);
    }

    // ─────────────────────────── Kaydetme ───────────────────────────

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        if (!HasExamLoaded || _model == null)
            return;

        ErrorMessage = null;
        SuccessMessage = null;

        var rows = Sheets.Where(s => s.MarkersFound && s.SelectedStudent != null).ToList();
        if (rows.Count == 0)
        {
            ErrorMessage = "Kaydedilecek eşleşmiş sayfa yok. Önce tarama yükleyin ve öğrencileri eşleştirin.";
            return;
        }

        // Aynı öğrenciye iki sayfa: hangisinin geçerli olduğuna öğretmen karar vermeli.
        var duplicates = rows.GroupBy(r => r.SelectedStudent!.StudentId)
            .Where(g => g.Count() > 1)
            .Select(g => g.First().SelectedStudent!.Display)
            .ToList();
        if (duplicates.Count > 0)
        {
            ErrorMessage = $"Aynı öğrenciye birden fazla sayfa eşleşti: {string.Join(", ", duplicates)}. " +
                           "Fazla sayfaları silin veya eşleştirmeyi düzeltin.";
            return;
        }

        // Çok kitapçıklı sınavda soru sırası kitapçığa göre değişir; kitapçık bilinmeden
        // form satırları sorulara eşlenemez.
        var multiBooklet = _model.AvailableBooklets.Count > 1;
        if (multiBooklet)
        {
            var missing = rows.Where(r => r.BookletCode == null).Select(r => r.SourceLabel).ToList();
            if (missing.Count > 0)
            {
                ErrorMessage = $"Kitapçık seçilmemiş sayfalar var: {string.Join(", ", missing)}. " +
                               "Bu sınavda soru sırası kitapçığa göre değiştiğinden kitapçık seçimi zorunludur.";
                return;
            }
        }

        var masterOrder = _mcQuestions.Select(q => q.QuestionId).ToList();
        var updates = new List<StudentAnswerUpdate>();
        foreach (var row in rows)
        {
            var answers = ParseAnswers(row.AnswersText);
            if (answers == null)
            {
                ErrorMessage = $"\"{row.SourceLabel}\": cevap dizisinde geçersiz karakter var. " +
                               "İzin verilenler: A–E, '-' (boş), '?' (belirsiz).";
                return;
            }
            if (answers.Count != _mcQuestions.Count)
            {
                ErrorMessage = $"\"{row.SourceLabel}\": cevap dizisi {_mcQuestions.Count} karakter olmalı, " +
                               $"{answers.Count} karakter girilmiş.";
                return;
            }

            int? bookletId = null;
            if (row.BookletCode != null && _bookletCodeToId.TryGetValue(row.BookletCode, out var id))
                bookletId = id;

            // Form satırı i → öğrencinin kitapçığındaki i. çoktan seçmeli soru.
            // Tek kitapçıkta (veya kitapçıksız sınavda) bu sıra ana sırayla aynıdır.
            var questionOrder = bookletId.HasValue && _bookletMcOrder.TryGetValue(bookletId.Value, out var order)
                ? order
                : masterOrder;

            for (var i = 0; i < _mcQuestions.Count; i++)
            {
                updates.Add(new StudentAnswerUpdate
                {
                    ExamId = _model.ExamId,
                    QuestionId = questionOrder[i],
                    StudentId = row.SelectedStudent!.StudentId,
                    BookletId = bookletId,
                    SelectedOption = answers[i],
                    Score = null
                });
            }
        }

        IsSaving = true;
        try
        {
            await _answerEntry.SaveAsync(_model.ExamId, updates);
            SuccessMessage = $"✓ {rows.Count} öğrencinin cevapları kaydedildi. " +
                             "Sınav analizi ekranından sonuçları inceleyebilirsiniz.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme başarısız: {ex.Message}";
            _log.Error("OptikOkuma", "Optik okuma sonuçları kaydedilemedi", ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>'ABD-?CA…' biçimindeki diziyi şık listesine çevirir; geçersiz karakterde null döner.</summary>
    private static List<OptionLetter>? ParseAnswers(string text)
    {
        var list = new List<OptionLetter>();
        foreach (var raw in text.Trim())
        {
            if (char.IsWhiteSpace(raw))
                continue;

            var ch = char.ToUpperInvariant(raw);
            if (ch is >= 'A' and <= 'E')
                list.Add((OptionLetter)(ch - 'A' + 1));
            else if (ch is '-' or '?' or '.')
                list.Add(OptionLetter.Empty);
            else
                return null;
        }
        return list;
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(s.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
    }
}
