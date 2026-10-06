using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;
using Microsoft.Win32;

namespace EduAnalytics.UI.ViewModels;

public partial class LearningOutcomeManagementViewModel : ObservableObject
{
    private readonly ILearningOutcomeService _loService;
    private readonly IProgramOutcomeService _poService;
    private readonly IExamCrudService _examService;

    /// <summary>DbContext'e paralel sorgu gitmesini engellemek için.</summary>
    private readonly SemaphoreSlim _opLock = new(1, 1);

    [ObservableProperty] private ObservableCollection<Course> _courses = new();
    [ObservableProperty] private Course? _selectedCourse;
    [ObservableProperty] private ObservableCollection<LearningOutcomeDto> _learningOutcomes = new();

    // Form alanları
    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formDescription = string.Empty;
    [ObservableProperty] private LearningOutcomeDto? _editingOutcome;

    /// <summary>Düzenlemede olan ÖÇ'nin kodu (sadece info amaçlı UI'da gösterilir).</summary>
    [ObservableProperty] private string _editingCodeInfo = string.Empty;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public LearningOutcomeManagementViewModel(
        ILearningOutcomeService loService,
        IProgramOutcomeService poService,
        IExamCrudService examService)
    {
        _loService = loService;
        _poService = poService;
        _examService = examService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var courses = await _examService.GetCoursesAsync();
            Courses = new ObservableCollection<Course>(courses);

            if (SelectedCourse == null && courses.Count > 0)
                SelectedCourse = courses[0];
            else
                await ReloadOutcomesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        _ = ReloadOutcomesAsync();
    }

    private async Task ReloadOutcomesAsync()
    {
        if (SelectedCourse == null) return;
        await _opLock.WaitAsync();
        try
        {
            var list = await _loService.GetByCourseAsync(SelectedCourse.Id);
            LearningOutcomes = new ObservableCollection<LearningOutcomeDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"ÖÇ listesi yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private void StartEdit(LearningOutcomeDto? lo)
    {
        if (lo == null) return;
        EditingOutcome = lo;
        EditingCodeInfo = lo.Code;
        FormCode = lo.Code;
        FormDescription = lo.Description ?? string.Empty;
    }

    [RelayCommand]
    private void StartNew()
    {
        EditingOutcome = null;
        EditingCodeInfo = string.Empty;
        FormCode = string.Empty;
        FormDescription = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedCourse == null)
        {
            ErrorMessage = "Önce ders seçin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(FormCode) || string.IsNullOrWhiteSpace(FormDescription))
        {
            ErrorMessage = "Kod ve açıklama zorunludur.";
            return;
        }

        await _opLock.WaitAsync();
        try
        {
            string code = NormalizeLearningOutcomeCode(FormCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorMessage = "Kod alanına 1, 2, 3 gibi bir sıra numarası yazın.";
                return;
            }

            var model = new LearningOutcomeCreateModel
            {
                CourseId = SelectedCourse.Id,
                Code = code,
                Name = FormDescription.Trim(),
                Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription.Trim()
            };

            if (EditingOutcome == null)
            {
                await _loService.CreateAsync(model);
                SuccessMessage = $"✓ Yeni ÖÇ eklendi ({code}).";
            }
            else
            {
                await _loService.UpdateAsync(EditingOutcome.Id, model);
                SuccessMessage = $"✓ {code} güncellendi.";
            }

            StartNew();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _opLock.Release();
        }
        await ReloadOutcomesAsync();
    }

    private static string NormalizeLearningOutcomeCode(string rawCode)
    {
        var value = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var match = Regex.Match(value, @"\d+");
        return match.Success ? $"ÖÇ-{int.Parse(match.Value)}" : string.Empty;
    }

    private static string GenerateNextCode(HashSet<int> usedNumbers)
    {
        int n = 1;
        while (usedNumbers.Contains(n)) n++;
        usedNumbers.Add(n);
        return $"ÖÇ-{n}";
    }

    private static int ExtractCodeNumber(string code)
    {
        var match = Regex.Match(code, @"\d+");
        return match.Success ? int.Parse(match.Value) : -1;
    }

    [RelayCommand]
    private void DownloadExcelTemplate()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası|*.xlsx",
            Title = "Öğrenim Çıktısı Şablonu İndir",
            FileName = "ogrenim_ciktilari_sablon.xlsx"
        };

        if (sfd.ShowDialog() != true) return;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Öğrenim Çıktıları");
        ws.Cell(1, 1).Value = "Kod";
        ws.Cell(1, 2).Value = "Açıklama";
        ws.Cell(2, 1).Value = "1";
        ws.Cell(2, 2).Value = "Öğrencinin bu ders sonunda kazanması beklenen bilgi, beceri veya yetkinliği açık ve ölçülebilir bir cümleyle yazın.";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        workbook.SaveAs(sfd.FileName);

        SuccessMessage = "Öğrenim çıktısı Excel şablonu indirildi.";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        if (SelectedCourse == null)
        {
            ErrorMessage = "Önce ders seçin.";
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "Excel Dosyası|*.xlsx;*.xls",
            Title = "Öğrenim Çıktıları Excel Dosyası Seç"
        };

        if (ofd.ShowDialog() != true) return;

        await _opLock.WaitAsync();
        try
        {
            using var workbook = new XLWorkbook(ofd.FileName);
            var ws = workbook.Worksheets.First();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            var existingByCode = LearningOutcomes.ToDictionary(lo => lo.Code.Trim(), StringComparer.OrdinalIgnoreCase);
            var usedNumbers = LearningOutcomes
                .Select(lo => ExtractCodeNumber(lo.Code))
                .Where(n => n > 0)
                .ToHashSet();

            int inserted = 0;
            int updated = 0;
            int skipped = 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var rawCode = ws.Cell(row, 1).GetString().Trim();
                var description = ws.Cell(row, 2).GetString().Trim();

                if (string.IsNullOrWhiteSpace(description))
                {
                    skipped++;
                    continue;
                }

                var code = NormalizeLearningOutcomeCode(rawCode);
                if (string.IsNullOrWhiteSpace(code))
                    code = GenerateNextCode(usedNumbers);
                else
                {
                    var number = ExtractCodeNumber(code);
                    if (number > 0) usedNumbers.Add(number);
                }

                var model = new LearningOutcomeCreateModel
                {
                    CourseId = SelectedCourse.Id,
                    Code = code,
                    Name = description,
                    Description = description
                };

                if (existingByCode.TryGetValue(code, out var existing))
                {
                    await _loService.UpdateAsync(existing.Id, model);
                    updated++;
                }
                else
                {
                    var id = await _loService.CreateAsync(model);
                    existingByCode[code] = new LearningOutcomeDto
                    {
                        Id = id,
                        CourseId = SelectedCourse.Id,
                        Code = code,
                        Name = description,
                        Description = model.Description
                    };
                    inserted++;
                }
            }

            SuccessMessage = $"Excel içe aktarıldı: {inserted} eklendi, {updated} güncellendi" +
                             (skipped > 0 ? $", {skipped} satır atlandı." : ".");
            ErrorMessage = null;
            StartNew();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Excel içe aktarma hatası: {ex.Message}";
            SuccessMessage = null;
        }
        finally
        {
            _opLock.Release();
        }

        await ReloadOutcomesAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(LearningOutcomeDto? lo)
    {
        if (lo == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"{lo.Code} — {lo.Name} silinsin mi?",
            "ÖÇ Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await _opLock.WaitAsync();
        try
        {
            await _loService.DeleteAsync(lo.Id);
            LearningOutcomes.Remove(lo);
            SuccessMessage = "ÖÇ silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _opLock.Release();
        }
    }
}
