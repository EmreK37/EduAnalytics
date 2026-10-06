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

public partial class ProgramOutcomeManagementViewModel : ObservableObject
{
    private readonly IProgramOutcomeService _poService;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    [ObservableProperty] private ObservableCollection<Program> _programs = new();
    [ObservableProperty] private Program? _selectedProgram;
    [ObservableProperty] private ObservableCollection<ProgramOutcomeDto> _programOutcomes = new();

    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formDescription = string.Empty;
    [ObservableProperty] private ProgramOutcomeDto? _editingOutcome;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public ProgramOutcomeManagementViewModel(IProgramOutcomeService poService)
    {
        _poService = poService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var programs = await _poService.GetProgramsAsync();
            Programs = new ObservableCollection<Program>(programs);

            if (SelectedProgram == null && programs.Count > 0)
                SelectedProgram = programs[0];
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

    partial void OnSelectedProgramChanged(Program? value)
    {
        _ = ReloadOutcomesAsync();
    }

    private async Task ReloadOutcomesAsync()
    {
        if (SelectedProgram == null) return;
        await _opLock.WaitAsync();
        try
        {
            var list = await _poService.GetByProgramAsync(SelectedProgram.Id);
            ProgramOutcomes = new ObservableCollection<ProgramOutcomeDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"PÇ listesi yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private void StartEdit(ProgramOutcomeDto? po)
    {
        if (po == null) return;
        EditingOutcome = po;
        FormCode = po.Code;
        FormDescription = po.Description;
    }

    [RelayCommand]
    private void StartNew()
    {
        EditingOutcome = null;
        FormCode = string.Empty;
        FormDescription = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedProgram == null)
        {
            ErrorMessage = "Önce program seçin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(FormCode) || string.IsNullOrWhiteSpace(FormDescription))
        {
            ErrorMessage = "Kod ve açıklama zorunludur.";
            return;
        }

        try
        {
            var code = NormalizeProgramOutcomeCode(FormCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorMessage = "Kod alanına 1, 2, 3 gibi bir sıra numarası yazın.";
                return;
            }

            var model = new ProgramOutcomeCreateModel
            {
                ProgramId = SelectedProgram.Id,
                Code = code,
                Description = FormDescription.Trim()
            };

            if (EditingOutcome == null)
            {
                await _poService.CreateAsync(model);
                SuccessMessage = "Yeni PÇ eklendi.";
            }
            else
            {
                await _poService.UpdateAsync(EditingOutcome.Id, model);
                SuccessMessage = "PÇ güncellendi.";
            }

            StartNew();
            await ReloadOutcomesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static int ExtractCodeNumber(string code)
    {
        var match = Regex.Match(code, @"\d+");
        return match.Success ? int.Parse(match.Value) : -1;
    }

    private static string NormalizeProgramOutcomeCode(string rawCode)
    {
        var value = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var match = Regex.Match(value, @"\d+");
        return match.Success ? $"PÇ-{int.Parse(match.Value)}" : string.Empty;
    }

    [RelayCommand]
    private void DownloadExcelTemplate()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası|*.xlsx",
            Title = "Program Çıktısı Şablonu İndir",
            FileName = "program_ciktilari_sablon.xlsx"
        };

        if (sfd.ShowDialog() != true) return;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Program Çıktıları");
        ws.Cell(1, 1).Value = "Kod";
        ws.Cell(1, 2).Value = "Açıklama";
        ws.Cell(2, 1).Value = "1";
        ws.Cell(2, 2).Value = "Örnek program çıktısı açıklaması";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        workbook.SaveAs(sfd.FileName);

        SuccessMessage = "Program çıktısı Excel şablonu indirildi.";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        if (SelectedProgram == null)
        {
            ErrorMessage = "Önce program seçin.";
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "Excel Dosyası|*.xlsx;*.xls",
            Title = "Program Çıktıları Excel Dosyası Seç"
        };

        if (ofd.ShowDialog() != true) return;

        await _opLock.WaitAsync();
        try
        {
            using var workbook = new XLWorkbook(ofd.FileName);
            var ws = workbook.Worksheets.First();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            var existingByCode = ProgramOutcomes.ToDictionary(po => po.Code.Trim(), StringComparer.OrdinalIgnoreCase);
            var usedNumbers = ProgramOutcomes
                .Select(po => ExtractCodeNumber(po.Code))
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

                var code = NormalizeProgramOutcomeCode(rawCode);
                if (string.IsNullOrWhiteSpace(code))
                {
                    int n = 1;
                    while (usedNumbers.Contains(n)) n++;
                    usedNumbers.Add(n);
                    code = $"PÇ-{n}";
                }
                else
                {
                    var number = ExtractCodeNumber(code);
                    if (number > 0) usedNumbers.Add(number);
                }

                var model = new ProgramOutcomeCreateModel
                {
                    ProgramId = SelectedProgram.Id,
                    Code = code,
                    Description = description
                };

                if (existingByCode.TryGetValue(code, out var existing))
                {
                    await _poService.UpdateAsync(existing.Id, model);
                    updated++;
                }
                else
                {
                    var id = await _poService.CreateAsync(model);
                    existingByCode[code] = new ProgramOutcomeDto
                    {
                        Id = id,
                        ProgramId = SelectedProgram.Id,
                        ProgramName = SelectedProgram.Name,
                        Code = code,
                        Description = description
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
    private async Task DeleteAsync(ProgramOutcomeDto? po)
    {
        if (po == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"{po.Code} silinsin mi? Bağlı ÖÇ eşleşmeleri de kaldırılacak.",
            "PÇ Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _poService.DeleteAsync(po.Id);
            ProgramOutcomes.Remove(po);
            SuccessMessage = "PÇ silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
