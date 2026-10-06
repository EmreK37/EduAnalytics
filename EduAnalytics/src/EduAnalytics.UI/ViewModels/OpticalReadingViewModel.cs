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
/// Optik Okuma ekranÄ±:
///   1) SÄ±nav seÃ§ â†’ yazdÄ±rÄ±labilir optik form PDF'i Ã¼ret,
///   2) TaranmÄ±ÅŸ formlarÄ± (PDF/JPEG/PNG) yÃ¼kle â†’ PaddleOCR ad-soyad/numara + OMR cevaplar,
///   3) Ä°nceleme Ä±zgarasÄ±nda dÃ¼ÅŸÃ¼k gÃ¼venli okumalarÄ± dÃ¼zelt,
///   4) Kaydet â†’ mevcut cevap giriÅŸi servisi Ã¼zerinden (kitapÃ§Ä±k ÅŸÄ±k Ã§Ã¶zme dahil) veritabanÄ±na yaz.
/// </summary>
public partial class OpticalReadingViewModel : ObservableObject
{
    private readonly IExamCrudService _examCrud;
    private readonly IAnswerEntryService _answerEntry;
    private readonly IExamBookletService _booklets;
    private readonly IOpticalFormReader _reader;
    private readonly IAppLogService _log;

    private AnswerEntryModel? _model;

    /// <summary>Formdaki satÄ±r sÄ±rasÄ±yla Ã§oktan seÃ§meli sorular (klasik sorular formda yer almaz).</summary>
    private List<AnswerEntryQuestion> _mcQuestions = new();

    /// <summary>
    /// KitapÃ§Ä±k Id â†’ o kitapÃ§Ä±ktaki Ã§oktan seÃ§meli sorularÄ±n OrderInBooklet sÄ±rasÄ±yla QuestionId'leri.
    /// B/C/D kitapÃ§Ä±klarÄ±nda soru sÄ±rasÄ± karÄ±ÅŸtÄ±rÄ±ldÄ±ÄŸÄ± iÃ§in form satÄ±rÄ± i'nin hangi soruya
    /// karÅŸÄ±lÄ±k geldiÄŸi Ã¶ÄŸrencinin kitapÃ§Ä±ÄŸÄ±na baÄŸlÄ±dÄ±r; kayÄ±t bu listeyle eÅŸlenir.
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
            ErrorMessage = $"SÄ±navlar yÃ¼klenemedi: {ex.Message}";
            _log.Error("OptikOkuma", "SÄ±nav listesi yÃ¼klenemedi", ex);
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

            // Soru sÄ±rasÄ± kitapÃ§Ä±klarda karÄ±ÅŸtÄ±rÄ±labildiÄŸinden her kitapÃ§Ä±ÄŸÄ±n kendi sÄ±rasÄ± yÃ¼klenir.
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
            ExamInfo = $"{_mcQuestions.Count} Ã§oktan seÃ§meli soru" +
                       (classicCount > 0 ? $" ({classicCount} klasik soru formda yer almaz)" : "") +
                       $" â€¢ {MatchableStudents.Count} kayÄ±tlÄ± Ã¶ÄŸrenci" +
                       (_model.AvailableBooklets.Count > 1 ? $" â€¢ {_model.AvailableBooklets.Count} kitapÃ§Ä±k" : "");

            if (_mcQuestions.Count == 0)
                ErrorMessage = "Bu sÄ±navda Ã§oktan seÃ§meli soru yok; optik form Ã¼retilemez.";
            else if (_mcQuestions.Count > OpticalFormTemplate.MaxQuestions)
                ErrorMessage = $"Optik form en fazla {OpticalFormTemplate.MaxQuestions} soru destekler; " +
                               $"bu sÄ±navda {_mcQuestions.Count} Ã§oktan seÃ§meli soru var.";
            else if (_bookletMcOrder.Values.Any(order => order.Count != _mcQuestions.Count))
                ErrorMessage = "KitapÃ§Ä±klarÄ±n soru listesi sÄ±navÄ±n Ã§oktan seÃ§meli sorularÄ±yla uyuÅŸmuyor; " +
                               "kitapÃ§Ä±klarÄ± yeniden Ã¼retin.";
            else
                HasExamLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"SÄ±nav yÃ¼klenemedi: {ex.Message}";
            _log.Error("OptikOkuma", $"SÄ±nav {exam.Id} yÃ¼klenemedi", ex);
        }
    }

    private OpticalFormSpec BuildSpec()
    {
        if (_model == null)
            throw new InvalidOperationException("Ã–nce sÄ±nav seÃ§ilmelidir.");

        // Ã‡ok kitapÃ§Ä±klÄ± sÄ±navda soru sÄ±rasÄ± kitapÃ§Ä±ÄŸa gÃ¶re deÄŸiÅŸtiÄŸinden ana soru numarasÄ±
        // basÄ±lamaz; satÄ±rlar 1..N konum numarasÄ±yla basÄ±lÄ±r ve kayÄ±tta Ã¶ÄŸrencinin kitapÃ§Ä±k
        // sÄ±rasÄ±na eÅŸlenir. Tek kitapÃ§Ä±kta ana numaralar aynen kullanÄ±lÄ±r.
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

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Form Ã¼retimi â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private void GenerateForm()
    {
        if (!HasExamLoaded || _model == null)
        {
            ErrorMessage = "Ã–nce bir sÄ±nav seÃ§in.";
            return;
        }

        var dialog = new EduAnalytics.UI.Views.OmrGeneratorDialog(BuildSpec());
        
        if (System.Windows.Application.Current.MainWindow != null)
            dialog.Owner = System.Windows.Application.Current.MainWindow;

        dialog.ShowDialog();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Tarama okuma â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task ImportScansAsync()
    {
        if (!HasExamLoaded || _model == null)
        {
            ErrorMessage = "Ã–nce bir sÄ±nav seÃ§in.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "TaranmÄ±ÅŸ Formlar|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff",
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
                    // Bozuk tek dosya partiyi durdurmasÄ±n; kalan dosyalar okunmaya devam eder.
                    failures.Add($"{Path.GetFileName(file)} ({ex.Message})");
                    _log.Error("OptikOkuma", $"Tarama dosyasÄ± okunamadÄ±: {file}", ex);
                }
            }

            var unreadable = Sheets.Count(s => !s.MarkersFound);
            var unmatched = Sheets.Count(s => s.MarkersFound && s.SelectedStudent == null);
            SuccessMessage = $"âœ“ {Sheets.Count} sayfa okundu." +
                             (unmatched > 0 ? $" {unmatched} sayfada Ã¶ÄŸrenci elle seÃ§ilmeli." : "") +
                             (unreadable > 0 ? $" {unreadable} sayfa form olarak tanÄ±namadÄ±." : "");
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
                MatchInfo = "OkunamadÄ±",
                WarningsText = string.Join(" ", result.Warnings)
            };
        }

        var (studentId, kind) = StudentMatcher.Match(result.RawStudentNumber, result.RawStudentName, candidates);
        var matchInfo = kind switch
        {
            MatchKind.ExactNumber => "Numara birebir eÅŸleÅŸti",
            MatchKind.FuzzyNumber => "Numara yaklaÅŸÄ±k eÅŸleÅŸti â€” kontrol edin",
            MatchKind.NameOnly => "YalnÄ±zca ad benzerliÄŸi â€” kontrol edin",
            _ => "EÅŸleÅŸmedi â€” elle seÃ§in"
        };

        var bookletCode = result.BookletIndex.HasValue
            ? spec.BookletCodes[result.BookletIndex.Value]
            : AvailableBookletCodes.Count == 1 ? AvailableBookletCodes[0] : null;

        var answersText = string.Concat(result.Answers.Select(a =>
            a.MultipleMarks ? '?'
            : a.Option == OptionLetter.Empty ? '-'
            : a.Option.ToString()[0]));

        // BoÅŸ bÄ±rakÄ±lan satÄ±rlarÄ±n "boÅŸluk gÃ¼veni" farklÄ± bir Ã¶lÃ§Ã¼dÃ¼r; sayfanÄ±n genel gÃ¼venini
        // yalnÄ±zca iÅŸaretli (veya Ã§ift iÅŸaretli) satÄ±rlar belirler.
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

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Kaydetme â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            ErrorMessage = "Kaydedilecek eÅŸleÅŸmiÅŸ sayfa yok. Ã–nce tarama yÃ¼kleyin ve Ã¶ÄŸrencileri eÅŸleÅŸtirin.";
            return;
        }

        // AynÄ± Ã¶ÄŸrenciye iki sayfa: hangisinin geÃ§erli olduÄŸuna Ã¶ÄŸretmen karar vermeli.
        var duplicates = rows.GroupBy(r => r.SelectedStudent!.StudentId)
            .Where(g => g.Count() > 1)
            .Select(g => g.First().SelectedStudent!.Display)
            .ToList();
        if (duplicates.Count > 0)
        {
            ErrorMessage = $"AynÄ± Ã¶ÄŸrenciye birden fazla sayfa eÅŸleÅŸti: {string.Join(", ", duplicates)}. " +
                           "Fazla sayfalarÄ± silin veya eÅŸleÅŸtirmeyi dÃ¼zeltin.";
            return;
        }

        // Ã‡ok kitapÃ§Ä±klÄ± sÄ±navda soru sÄ±rasÄ± kitapÃ§Ä±ÄŸa gÃ¶re deÄŸiÅŸir; kitapÃ§Ä±k bilinmeden
        // form satÄ±rlarÄ± sorulara eÅŸlenemez.
        var multiBooklet = _model.AvailableBooklets.Count > 1;
        if (multiBooklet)
        {
            var missing = rows.Where(r => r.BookletCode == null).Select(r => r.SourceLabel).ToList();
            if (missing.Count > 0)
            {
                ErrorMessage = $"KitapÃ§Ä±k seÃ§ilmemiÅŸ sayfalar var: {string.Join(", ", missing)}. " +
                               "Bu sÄ±navda soru sÄ±rasÄ± kitapÃ§Ä±ÄŸa gÃ¶re deÄŸiÅŸtiÄŸinden kitapÃ§Ä±k seÃ§imi zorunludur.";
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
                ErrorMessage = $"\"{row.SourceLabel}\": cevap dizisinde geÃ§ersiz karakter var. " +
                               "Ä°zin verilenler: Aâ€“E, '-' (boÅŸ), '?' (belirsiz).";
                return;
            }
            if (answers.Count != _mcQuestions.Count)
            {
                ErrorMessage = $"\"{row.SourceLabel}\": cevap dizisi {_mcQuestions.Count} karakter olmalÄ±, " +
                               $"{answers.Count} karakter girilmiÅŸ.";
                return;
            }

            int? bookletId = null;
            if (row.BookletCode != null && _bookletCodeToId.TryGetValue(row.BookletCode, out var id))
                bookletId = id;

            // Form satÄ±rÄ± i â†’ Ã¶ÄŸrencinin kitapÃ§Ä±ÄŸÄ±ndaki i. Ã§oktan seÃ§meli soru.
            // Tek kitapÃ§Ä±kta (veya kitapÃ§Ä±ksÄ±z sÄ±navda) bu sÄ±ra ana sÄ±rayla aynÄ±dÄ±r.
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
            SuccessMessage = $"âœ“ {rows.Count} Ã¶ÄŸrencinin cevaplarÄ± kaydedildi. " +
                             "SÄ±nav analizi ekranÄ±ndan sonuÃ§larÄ± inceleyebilirsiniz.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme baÅŸarÄ±sÄ±z: {ex.Message}";
            _log.Error("OptikOkuma", "Optik okuma sonuÃ§larÄ± kaydedilemedi", ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>'ABD-?CAâ€¦' biÃ§imindeki diziyi ÅŸÄ±k listesine Ã§evirir; geÃ§ersiz karakterde null dÃ¶ner.</summary>
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
