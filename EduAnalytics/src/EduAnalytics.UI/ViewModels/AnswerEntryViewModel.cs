using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using ClosedXML.Excel;

namespace EduAnalytics.UI.ViewModels;

public partial class AnswerEntryViewModel : ObservableObject
{
    private readonly IAnswerEntryService _service;
    private int _examId;
    public int ExamId => _examId;

    /// <summary>
    /// View, Questions koleksiyonu değiştiğinde DataGrid kolonlarını yeniden kurmak için dinler.
    /// </summary>
    public event Action? QuestionsLoaded;

    /// <summary>View klasik soru için kriter puanlama dialogunu açmak üzere bunu dinler.</summary>
    public event Action<int, int>? OpenRubricRequested;

    /// <summary>Geri butonu / kayıt sonrası analiz ekranına geri dönüş için.</summary>
    public event Action<int>? BackRequested;

    [ObservableProperty] private string _examTitle = string.Empty;
    [ObservableProperty] private string _courseName = string.Empty;
    [ObservableProperty] private int _bookletCount = 1;
    [ObservableProperty] private ObservableCollection<AnswerEntryQuestion> _questions = new();
    [ObservableProperty] private ObservableCollection<StudentRowViewModel> _studentRows = new();
    [ObservableProperty] private ObservableCollection<string> _availableBookletCodes = new();

    /// <summary>BookletCode → BookletId eşlemesi. Save sırasında ID lookup için.</summary>
    private Dictionary<string, int> _bookletCodeToId = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    [RelayCommand]
    private void CopyToClipboard()
    {
        try
        {
            var sb = new StringBuilder();
            
            // Başlık satırı
            sb.Append("OgrenciNo\tAdSoyad\tKitapcik");
            foreach (var q in Questions)
            {
                sb.Append($"\tSoru{q.QuestionNumber}");
            }
            sb.AppendLine();

            // Öğrenci satırları
            foreach (var student in StudentRows)
            {
                sb.Append($"{student.StudentNumber}\t{student.FullName}\t{student.BookletCode ?? string.Empty}");
                foreach (var cell in student.Cells)
                {
                    if (cell.Type == Core.Enums.QuestionType.MultipleChoice)
                    {
                        var val = cell.SelectedOption == Core.Enums.OptionLetter.Empty ? "" : cell.SelectedOption.ToString();
                        sb.Append($"\t{val}");
                    }
                    else
                    {
                        sb.Append($"\t{cell.Score}");
                    }
                }
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
            SuccessMessage = "✓ Tablo Excel formatında panoya kopyalandı.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kopyalama hatası: {ex.Message}";
            SuccessMessage = null;
        }
    }

    [RelayCommand]
    private void PasteFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                ErrorMessage = "Pano boş veya metin içermiyor.";
                return;
            }

            var text = Clipboard.GetText();
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            int updateCount = 0;
            
            // İlk satırı başlık olarak varsay, 2. satırdan başla
            // Ancak kullanıcı başlık olmadan kopyaladıysa öğrenci nosundan anlayalım.
            int startIdx = lines[0].Contains("OgrenciNo", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            for (int i = startIdx; i < lines.Length; i++)
            {
                var cols = lines[i].Split('\t');
                if (cols.Length < 2) continue; // En az Öğrenci No alanına izin ver

                var stdNo = cols[0].Trim();
                var studentRow = StudentRows.FirstOrDefault(r => r.StudentNumber == stdNo);

                if (studentRow == null) continue; // Öğrenci eşleşmedi

                // 3. sütun = Kitapçık kodu
                if (cols.Length > 2)
                {
                    var bookletCol = cols[2].Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(bookletCol) && AvailableBookletCodes.Contains(bookletCol))
                        studentRow.BookletCode = bookletCol;
                }

                for (int c = 3, qIdx = 0; c < cols.Length && qIdx < studentRow.Cells.Count; c++, qIdx++)
                {
                    var cellVal = cols[c].Trim();
                    var cell = studentRow.Cells[qIdx];

                    if (cell.Type == Core.Enums.QuestionType.MultipleChoice)
                    {
                        if (string.IsNullOrEmpty(cellVal))
                        {
                            cell.SelectedOption = Core.Enums.OptionLetter.Empty;
                        }
                        else if (Enum.TryParse<Core.Enums.OptionLetter>(cellVal.ToUpper(), out var opt))
                        {
                            cell.SelectedOption = opt;
                        }
                    }
                    else
                    {
                        if (decimal.TryParse(cellVal, out var sc))
                            cell.Score = sc;
                        else if (string.IsNullOrEmpty(cellVal))
                            cell.Score = null;
                    }
                    updateCount++;
                }
            }

            SuccessMessage = $"✓ Panodan {updateCount} hücre güncellendi. Kaydetmeyi unutmayın!";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yapıştırma hatası: {ex.Message}";
            SuccessMessage = null;
        }
    }

    [RelayCommand]
    private void ImportExcel()
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx;*.xls",
                Title = "Doldurulmuş Cevap Excel'ini Yükle"
            };

            if (ofd.ShowDialog() != true) return;

            using var wb = new XLWorkbook(ofd.FileName);
            var ws = wb.Worksheets.First();

            int updateCount = 0;
            int rowIdx = 2; // 1. satır başlık

            while (!ws.Cell(rowIdx, 1).IsEmpty())
            {
                var stdNo = ws.Cell(rowIdx, 1).GetString().Trim();
                var studentRow = StudentRows.FirstOrDefault(r => r.StudentNumber == stdNo);

                if (studentRow == null)
                {
                    rowIdx++;
                    continue;
                }

                // 3. sütun = Kitapçık
                var booklet = ws.Cell(rowIdx, 3).GetString().Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(booklet) && AvailableBookletCodes.Contains(booklet))
                    studentRow.BookletCode = booklet;

                // 4. sütundan sorular başlar
                for (int qIdx = 0; qIdx < studentRow.Cells.Count; qIdx++)
                {
                    var excelCell = ws.Cell(rowIdx, qIdx + 4);
                    var cellVal = excelCell.GetString().Trim();
                    var cell = studentRow.Cells[qIdx];

                    if (cell.Type == Core.Enums.QuestionType.MultipleChoice)
                    {
                        if (string.IsNullOrEmpty(cellVal))
                            cell.SelectedOption = Core.Enums.OptionLetter.Empty;
                        else if (Enum.TryParse<Core.Enums.OptionLetter>(cellVal.ToUpperInvariant(), out var opt))
                            cell.SelectedOption = opt;
                    }
                    else
                    {
                        if (decimal.TryParse(cellVal, System.Globalization.NumberStyles.Any,
                                             System.Globalization.CultureInfo.InvariantCulture, out var sc))
                            cell.Score = sc;
                        else if (decimal.TryParse(cellVal, out var sc2))
                            cell.Score = sc2;
                        else if (string.IsNullOrEmpty(cellVal))
                            cell.Score = null;
                    }
                    updateCount++;
                }

                rowIdx++;
            }

            SuccessMessage = $"✓ Excel'den {updateCount} hücre yüklendi. 'Kaydet' ile veritabanına yazın.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Excel yükleme hatası: {ex.Message}";
            SuccessMessage = null;
        }
    }

    [RelayCommand]
    private void DownloadExcel()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                Title = "Sınav Sonuç Tablosunu İndir",
                FileName = $"{ExamTitle}_Cevaplar.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Cevaplar");

                // Başlık Satırı (Satır 1)
                ws.Cell(1, 1).Value = "Öğrenci Numarası";
                ws.Cell(1, 2).Value = "Adı Soyadı";
                ws.Cell(1, 3).Value = "Kitapçık";

                for (int i = 0; i < Questions.Count; i++)
                {
                    ws.Cell(1, i + 4).Value = $"Soru {Questions[i].QuestionNumber}";
                }

                // Veriler (Satır 2'den itibaren)
                for (int rowIndex = 0; rowIndex < StudentRows.Count; rowIndex++)
                {
                    var sr = StudentRows[rowIndex];
                    int excelRow = rowIndex + 2;

                    ws.Cell(excelRow, 1).Value = sr.StudentNumber;
                    ws.Cell(excelRow, 2).Value = sr.FullName;
                    ws.Cell(excelRow, 3).Value = sr.BookletCode ?? "";

                    for (int colIndex = 0; colIndex < sr.Cells.Count; colIndex++)
                    {
                        var cell = sr.Cells[colIndex];
                        int excelCol = colIndex + 4;

                        if (cell.Type == Core.Enums.QuestionType.MultipleChoice)
                        {
                            var val = cell.SelectedOption == Core.Enums.OptionLetter.Empty ? "" : cell.SelectedOption.ToString();
                            ws.Cell(excelRow, excelCol).Value = val;
                        }
                        else
                        {
                            ws.Cell(excelRow, excelCol).Value = cell.Score?.ToString() ?? "";
                        }
                    }
                }

                // Sütunları sığdır
                ws.Columns().AdjustToContents();

                wb.SaveAs(sfd.FileName);
                SuccessMessage = $"✓ Tablo {sfd.FileName} konumuna başarıyla kaydedildi.";
                ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İndirme hatası: {ex.Message}";
            SuccessMessage = null;
        }
    }

    public AnswerEntryViewModel(IAnswerEntryService service)
    {
        _service = service;
    }

    public async Task LoadAsync(int examId)
    {
        _examId = examId;
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var model = await _service.LoadAsync(examId);
            ExamTitle = model.ExamTitle;
            CourseName = model.CourseName;
            BookletCount = model.BookletCount;

            // Kitapçık lookup'ları kur
            _bookletCodeToId = model.AvailableBooklets.ToDictionary(b => b.BookletCode, b => b.BookletId);
            AvailableBookletCodes = new ObservableCollection<string>(model.AvailableBooklets.Select(b => b.BookletCode));

            Questions = new ObservableCollection<AnswerEntryQuestion>(model.Questions);

            var rows = new List<StudentRowViewModel>();
            foreach (var s in model.Students)
            {
                var row = new StudentRowViewModel
                {
                    StudentId = s.StudentId,
                    StudentNumber = s.StudentNumber,
                    FullName = s.FullName,
                    BookletCode = s.BookletCode ?? (BookletCount == 1 ? "A" : null),
                    BookletId = s.BookletId
                };

                foreach (var q in model.Questions)
                {
                    var cell = new AnswerCellViewModel
                    {
                        QuestionId = q.QuestionId,
                        StudentId = s.StudentId,
                        Type = q.Type,
                        MaxPoints = q.MaxPoints
                    };

                    if (s.Answers.TryGetValue(q.QuestionId, out var existing))
                    {
                        cell.SelectedOption = existing.SelectedOption;
                        cell.Score = existing.Score;
                    }

                    row.Cells.Add(cell);
                }

                rows.Add(row);
            }
            StudentRows = new ObservableCollection<StudentRowViewModel>(rows);

            QuestionsLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Veri yükleme hatası: {ex.Message}" +
                           (ex.InnerException != null ? $" — {ex.InnerException.Message}" : "");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var updates = StudentRows
                .SelectMany(r =>
                {
                    int? bookletId = null;
                    if (!string.IsNullOrEmpty(r.BookletCode) && _bookletCodeToId.TryGetValue(r.BookletCode, out var bid))
                        bookletId = bid;
                    else
                        bookletId = r.BookletId;

                    return r.Cells.Select(c => new StudentAnswerUpdate
                    {
                        ExamId = _examId,
                        QuestionId = c.QuestionId,
                        StudentId = c.StudentId,
                        BookletId = bookletId,
                        SelectedOption = c.SelectedOption,
                        Score = c.Score
                    });
                })
                .ToList();

            await _service.SaveAsync(_examId, updates);
            SuccessMessage = $"✓ {updates.Count} cevap kaydedildi.";

            // Kayıt başarılı; analiz ekranına geri dön.
            BackRequested?.Invoke(_examId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme hatası: {ex.Message}" +
                           (ex.InnerException != null ? $" — {ex.InnerException.Message}" : "");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        BackRequested?.Invoke(_examId);
    }

    [RelayCommand]
    private void OpenRubricDialog(AnswerCellViewModel? cell)
    {
        if (cell == null || cell.Type != Core.Enums.QuestionType.OpenEnded) return;
        OpenRubricRequested?.Invoke(cell.QuestionId, cell.StudentId);
    }

    /// <summary>Cevap girişini yeniden yükle (rubric dialog kapandıktan sonra çağrılır).</summary>
    public Task ReloadAsync() => LoadAsync(_examId);

    /// <summary>Dropdown için OptionLetter değerleri.</summary>
    public Core.Enums.OptionLetter[] OptionLetters { get; } =
        new[]
        {
            Core.Enums.OptionLetter.Empty,
            Core.Enums.OptionLetter.A,
            Core.Enums.OptionLetter.B,
            Core.Enums.OptionLetter.C,
            Core.Enums.OptionLetter.D,
            Core.Enums.OptionLetter.E
        };
}
