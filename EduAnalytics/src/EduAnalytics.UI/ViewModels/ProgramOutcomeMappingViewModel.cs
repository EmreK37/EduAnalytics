using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using EduAnalytics.Core.Entities;

namespace EduAnalytics.UI.ViewModels;

public partial class MatrixCell : ObservableObject
{
    public int ProgramOutcomeId { get; init; }
    public int LearningOutcomeId { get; init; }
    public string ProgramOutcomeCode { get; init; } = string.Empty;
    public string LearningOutcomeCode { get; init; } = string.Empty;
    public string ToolTip { get; init; } = string.Empty;

    [ObservableProperty] private int? _contributionLevel;

    public string DisplayValue => ContributionLevel?.ToString() ?? string.Empty;

    partial void OnContributionLevelChanged(int? value)
        => OnPropertyChanged(nameof(DisplayValue));
}

public class MatrixColumnHeader
{
    public string Label { get; init; } = string.Empty;
    public string ToolTip { get; init; } = string.Empty;
}

public class MatrixRow
{
    public string RowLabel { get; set; } = string.Empty;
    public string ToolTip { get; set; } = string.Empty;
    public ObservableCollection<MatrixCell> Cells { get; } = new();
}

/// <summary>
/// Program Çıktısı (PÇ) ↔ Öğrenim Çıktısı (ÖÇ) eşleştirme ekranı.
/// Soldan PÇ seç, sağda eşleşmiş ÖÇ'leri gör + bağlanabilecek ÖÇ'ler havuzından ekle / kaldır.
/// </summary>
public partial class ProgramOutcomeMappingViewModel : ObservableObject
{
    private readonly IProgramOutcomeService _service;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    [ObservableProperty] private ObservableCollection<Program> _programs = new();
    [ObservableProperty] private Program? _selectedProgram;

    [ObservableProperty] private ObservableCollection<ProgramOutcomeDto> _programOutcomes = new();
    [ObservableProperty] private ProgramOutcomeDto? _selectedProgramOutcome;

    [ObservableProperty] private ObservableCollection<MappedLearningOutcomeDto> _mappedOutcomes = new();
    [ObservableProperty] private ObservableCollection<LearningOutcomeDto> _availableOutcomes = new();
    [ObservableProperty] private LearningOutcomeDto? _outcomeToAdd;
    [ObservableProperty] private int _newContributionLevel = 3;

    [ObservableProperty] private ObservableCollection<MatrixRow> _matrixRows = new();
    [ObservableProperty] private ObservableCollection<MatrixColumnHeader> _matrixColumns = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public ProgramOutcomeMappingViewModel(IProgramOutcomeService service)
    {
        _service = service;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        await _opLock.WaitAsync();
        try
        {
            var programs = await _service.GetProgramsAsync();
            Programs = new ObservableCollection<Program>(programs);
            if (SelectedProgram == null && Programs.Count > 0)
                SelectedProgram = Programs[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Programlar yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
            IsLoading = false;
        }
    }

    partial void OnSelectedProgramChanged(Program? value)
    {
        _ = ReloadProgramOutcomesAsync();
    }

    partial void OnSelectedProgramOutcomeChanged(ProgramOutcomeDto? value)
    {
        _ = ReloadMappingsAsync();
    }

    private async Task ReloadProgramOutcomesAsync()
    {
        if (SelectedProgram == null) return;
        await _opLock.WaitAsync();
        try
        {
            var programId = SelectedProgram.Id;
            var po = await _service.GetByProgramAsync(programId);
            ProgramOutcomes = new ObservableCollection<ProgramOutcomeDto>(po);

            var available = await _service.GetAllLearningOutcomesInProgramAsync(programId);
            AvailableOutcomes = new ObservableCollection<LearningOutcomeDto>(available);

            SelectedProgramOutcome = ProgramOutcomes.FirstOrDefault();
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

    private async Task ReloadMappingsAsync()
    {
        if (SelectedProgramOutcome == null)
        {
            MappedOutcomes = new ObservableCollection<MappedLearningOutcomeDto>();
            MatrixRows = new ObservableCollection<MatrixRow>();
            MatrixColumns = new ObservableCollection<MatrixColumnHeader>();
            return;
        }
        await _opLock.WaitAsync();
        try
        {
            var mappings = await _service.GetMappedLearningOutcomesAsync(SelectedProgramOutcome.Id);
            MappedOutcomes = new ObservableCollection<MappedLearningOutcomeDto>(mappings);

            // Tüm program çıktılarına göre matrix değerlerini topla ve yansıt
            if (SelectedProgram != null)
            {
               await RegenerateMatrixCoreAsync(SelectedProgram.Id);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eşleştirmeler yüklenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async Task RegenerateMatrixCoreAsync(int programId)
    {
        var programOutcomes = await _service.GetByProgramAsync(programId);
        var learningOutcomes = await _service.GetAllLearningOutcomesInProgramAsync(programId);

        var orderedProgramOutcomes = programOutcomes
            .Select(po => new { Outcome = po, Index = ParseOutcomeIndex(po.Code) })
            .Where(po => po.Index is >= 1 and <= 15)
            .OrderBy(po => po.Index)
            .ToList();

        MatrixColumns = new ObservableCollection<MatrixColumnHeader>(
            orderedProgramOutcomes.Select(po => new MatrixColumnHeader
            {
                Label = $"P{po.Index}",
                ToolTip = $"{po.Outcome.Code}: {po.Outcome.Description}"
            }));

        var contributionMap = new Dictionary<(int ProgramOutcomeId, int LearningOutcomeId), int>();
        foreach (var po in orderedProgramOutcomes)
        {
            var mappings = await _service.GetMappedLearningOutcomesAsync(po.Outcome.Id);
            foreach (var mapping in mappings)
                contributionMap[(po.Outcome.Id, mapping.LearningOutcomeId)] = mapping.ContributionLevel;
        }

        var rows = new List<MatrixRow>();
        foreach (var lo in learningOutcomes.OrderBy(lo => ParseOutcomeIndex(lo.Code)).ThenBy(lo => lo.Code))
        {
            var row = new MatrixRow
            {
                RowLabel = lo.Code,
                ToolTip = $"{lo.Code}: {lo.Name}"
            };

            foreach (var po in orderedProgramOutcomes)
            {
                contributionMap.TryGetValue((po.Outcome.Id, lo.Id), out var level);
                row.Cells.Add(new MatrixCell
                {
                    ProgramOutcomeId = po.Outcome.Id,
                    LearningOutcomeId = lo.Id,
                    ProgramOutcomeCode = po.Outcome.Code,
                    LearningOutcomeCode = lo.Code,
                    ContributionLevel = level == 0 ? null : level,
                    ToolTip = $"{lo.Code} - {lo.Name}\n{po.Outcome.Code} - {po.Outcome.Description}\nKatkı düzeyini listeden seçin."
                });
            }

            rows.Add(row);
        }

        MatrixRows = new ObservableCollection<MatrixRow>(rows);
    }

    private static int ParseOutcomeIndex(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return int.MaxValue;

        var match = Regex.Match(code, @"\d+");
        return match.Success && int.TryParse(match.Value, out var index)
            ? index
            : int.MaxValue;
    }

    [RelayCommand]
    private async Task LinkOutcomeAsync()
    {
        if (SelectedProgramOutcome == null || OutcomeToAdd == null)
        {
            ErrorMessage = "PÇ ve ÖÇ seçilmeli.";
            return;
        }

        await _opLock.WaitAsync();
        try
        {
            await _service.LinkToLearningOutcomeAsync(
                SelectedProgramOutcome.Id,
                OutcomeToAdd.Id,
                Math.Clamp(NewContributionLevel, 1, 5));

            SuccessMessage = $"✓ ÖÇ '{OutcomeToAdd.Code}' bağlandı (katkı: {NewContributionLevel}).";
            OutcomeToAdd = null;
            var mappings = await _service.GetMappedLearningOutcomesAsync(SelectedProgramOutcome.Id);
            MappedOutcomes = new ObservableCollection<MappedLearningOutcomeDto>(mappings);
            if (SelectedProgram != null)
                await RegenerateMatrixCoreAsync(SelectedProgram.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Bağlama hatası: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private async Task UnlinkOutcomeAsync(MappedLearningOutcomeDto? mapping)
    {
        if (mapping == null || SelectedProgramOutcome == null) return;

        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"'{mapping.Code} — {mapping.Name}' eşleştirmesini kaldırmak istediğinize emin misiniz?",
            "Eşleştirme Kaldır",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await _opLock.WaitAsync();
        try
        {
            await _service.UnlinkFromLearningOutcomeAsync(SelectedProgramOutcome.Id, mapping.LearningOutcomeId);
            SuccessMessage = "Eşleştirme kaldırıldı.";
            var mappings = await _service.GetMappedLearningOutcomesAsync(SelectedProgramOutcome.Id);
            MappedOutcomes = new ObservableCollection<MappedLearningOutcomeDto>(mappings);
            if (SelectedProgram != null)
                await RegenerateMatrixCoreAsync(SelectedProgram.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaldırma hatası: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private async Task UpdateContributionAsync(MappedLearningOutcomeDto? mapping)
    {
        if (mapping == null || SelectedProgramOutcome == null) return;
        await _opLock.WaitAsync();
        try
        {
            await _service.LinkToLearningOutcomeAsync(
                SelectedProgramOutcome.Id,
                mapping.LearningOutcomeId,
                Math.Clamp(mapping.ContributionLevel, 1, 5));

            SuccessMessage = $"✓ Katkı seviyesi güncellendi.";
            if (SelectedProgram != null)
                await RegenerateMatrixCoreAsync(SelectedProgram.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Güncelleme hatası: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }

    [RelayCommand]
    private async Task SaveMatrixCellAsync(MatrixCell? cell)
    {
        if (cell == null)
            return;

        await _opLock.WaitAsync();
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            if (cell.ContributionLevel == null)
            {
                await _service.UnlinkFromLearningOutcomeAsync(cell.ProgramOutcomeId, cell.LearningOutcomeId);
            }
            else
            {
                await _service.LinkToLearningOutcomeAsync(
                    cell.ProgramOutcomeId,
                    cell.LearningOutcomeId,
                    Math.Clamp(cell.ContributionLevel.Value, 1, 5));
            }

            SuccessMessage = cell.ContributionLevel == null
                ? $"✓ {cell.LearningOutcomeCode} - {cell.ProgramOutcomeCode} katkısı temizlendi."
                : $"✓ {cell.LearningOutcomeCode} - {cell.ProgramOutcomeCode} katkısı {cell.ContributionLevel} yapıldı.";

            if (SelectedProgramOutcome?.Id == cell.ProgramOutcomeId)
                MappedOutcomes = new ObservableCollection<MappedLearningOutcomeDto>(
                    await _service.GetMappedLearningOutcomesAsync(cell.ProgramOutcomeId));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Katkı güncellenemedi: {ex.Message}";
        }
        finally
        {
            _opLock.Release();
        }
    }
}
