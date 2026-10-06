using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EduAnalytics.Business.Services.Interfaces;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EduAnalytics.UI.ViewModels;

public partial class AdminExportViewModel : ObservableObject
{
    private readonly IDataExportService _exportService;

    [ObservableProperty]
    private bool _isExporting;

    public AdminExportViewModel(IDataExportService exportService)
    {
        _exportService = exportService;
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "EduAnalytics Data (*.edudata)|*.edudata",
                DefaultExt = ".edudata",
                FileName = $"okul_veritabani_{DateTime.Now:yyyyMMdd}.edudata"
            };

            if (dialog.ShowDialog() != true)
                return;

            IsExporting = true;
            var package = await _exportService.ExportDatabaseAsync();
            var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(dialog.FileName, json);
            
            EduAnalytics.UI.Services.AppMessageBox.Show(
                "Sistem veritabanı başarıyla .edudata formatında paketlendi!\n\nBu dosyayı öğretmenlere dağıtıp Ayarlar sayfasından yüklemelerini sağlayabilirsiniz.", 
                "Admin Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            EduAnalytics.UI.Services.AppMessageBox.Show($"Dışa aktarılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }
}
