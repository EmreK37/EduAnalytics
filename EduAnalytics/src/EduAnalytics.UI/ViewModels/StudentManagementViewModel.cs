using System.Collections.ObjectModel;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Dtos;
using EduAnalytics.Business.Services.Interfaces;
using Microsoft.Win32;

namespace EduAnalytics.UI.ViewModels;

/// <summary>
/// Öğrenci yönetimi ekranı: liste + ekle/düzenle/sil + Excel ile toplu yükleme + boş şablon indirme.
/// </summary>
public partial class StudentManagementViewModel : ObservableObject
{
    private readonly IStudentService _service;

    [ObservableProperty] private ObservableCollection<StudentDto> _students = new();
    [ObservableProperty] private ObservableCollection<StudentDto> _filteredStudents = new();
    [ObservableProperty] private string _searchText = string.Empty;

    // Form alanları
    [ObservableProperty] private StudentDto? _editingStudent;
    [ObservableProperty] private string _formStudentNumber = string.Empty;
    [ObservableProperty] private string _formFullName = string.Empty;
    [ObservableProperty] private string _formClassName = string.Empty;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public StudentManagementViewModel(IStudentService service)
    {
        _service = service;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Students = new ObservableCollection<StudentDto>(list);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Öğrenciler yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredStudents = new ObservableCollection<StudentDto>(Students);
            return;
        }

        var q = SearchText.Trim();
        var filtered = Students.Where(s =>
            (s.StudentNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.FullName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.ClassName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        FilteredStudents = new ObservableCollection<StudentDto>(filtered);
    }

    [RelayCommand]
    private void StartNew()
    {
        EditingStudent = null;
        FormStudentNumber = string.Empty;
        FormFullName = string.Empty;
        FormClassName = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    private void StartEdit(StudentDto? student)
    {
        if (student == null) return;
        EditingStudent = student;
        FormStudentNumber = student.StudentNumber;
        FormFullName = student.FullName;
        FormClassName = student.ClassName;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(FormStudentNumber))
        {
            ErrorMessage = "Öğrenci numarası boş olamaz.";
            return;
        }
        if (string.IsNullOrWhiteSpace(FormFullName))
        {
            ErrorMessage = "Ad-Soyad boş olamaz.";
            return;
        }

        var model = new StudentSaveModel
        {
            StudentNumber = FormStudentNumber,
            FullName = FormFullName,
            ClassName = FormClassName
        };

        try
        {
            if (EditingStudent == null)
            {
                await _service.CreateAsync(model);
                SuccessMessage = "✓ Öğrenci eklendi.";
            }
            else
            {
                await _service.UpdateAsync(EditingStudent.Id, model);
                SuccessMessage = "✓ Öğrenci güncellendi.";
            }

            StartNew();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kayıt hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(StudentDto? student)
    {
        if (student == null) return;
        var confirm = EduAnalytics.UI.Services.AppMessageBox.Show(
            $"'{student.StudentNumber} — {student.FullName}' öğrencisini silmek istediğinize emin misiniz?",
            "Öğrenci Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _service.DeleteAsync(student.Id);
            SuccessMessage = "Öğrenci silindi.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DownloadTemplate()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                Title = "Öğrenci Şablonunu İndir",
                FileName = "Ogrenci_Sablonu.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Öğrenciler");

            ws.Cell(1, 1).Value = "Öğrenci Numarası";
            ws.Cell(1, 2).Value = "Ad Soyad";
            ws.Cell(1, 3).Value = "Sınıf / Bölüm";

            var header = ws.Range("A1:C1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Örnek satırlar
            ws.Cell(2, 1).Value = "2025001";
            ws.Cell(2, 2).Value = "Ahmet Yılmaz";
            ws.Cell(2, 3).Value = "Yazılım - 3.Sınıf";

            ws.Cell(3, 1).Value = "2025002";
            ws.Cell(3, 2).Value = "Ayşe Demir";
            ws.Cell(3, 3).Value = "Bilgisayar - 2.Sınıf";

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);

            SuccessMessage = $"✓ Şablon kaydedildi: {sfd.FileName}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şablon hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx;*.xls",
                Title = "Öğrenci Listesi Yükle"
            };
            if (ofd.ShowDialog() != true) return;

            using var wb = new XLWorkbook(ofd.FileName);
            var ws = wb.Worksheets.First();

            var rows = new List<StudentSaveModel>();
            int rowIdx = 2; // 1. satır başlık
            while (!ws.Cell(rowIdx, 1).IsEmpty())
            {
                rows.Add(new StudentSaveModel
                {
                    StudentNumber = ws.Cell(rowIdx, 1).GetString().Trim(),
                    FullName = ws.Cell(rowIdx, 2).GetString().Trim(),
                    ClassName = ws.Cell(rowIdx, 3).GetString().Trim()
                });
                rowIdx++;
            }

            if (rows.Count == 0)
            {
                ErrorMessage = "Excel'de veri bulunamadı (1. satır başlık olmalı, 2. satırdan itibaren öğrenciler).";
                return;
            }

            var result = await _service.ImportAsync(rows);
            SuccessMessage = $"✓ {result.InsertedCount} eklendi, {result.UpdatedCount} güncellendi" +
                             (result.SkippedCount > 0 ? $", {result.SkippedCount} atlandı" : "") + ".";
            if (result.Warnings.Count > 0)
                ErrorMessage = string.Join(" | ", result.Warnings.Take(5));

            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Excel yükleme hatası: {ex.Message}";
        }
    }
}
