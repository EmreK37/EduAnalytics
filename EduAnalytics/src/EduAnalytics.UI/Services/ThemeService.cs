using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace EduAnalytics.UI.Services;

public enum AppThemeMode { Light, Dark, System }

public interface IThemeService
{
    AppThemeMode CurrentTheme { get; }
    event Action ThemeChanged;
    void Apply(AppThemeMode mode);
    void ApplySystemForStartup();
    void CycleTheme();
    void LoadAndApply();
    SKColorPalette GetChartPalette();
}

/// <summary>SkiaSharp chart renk paleti — tema bilincinde.</summary>
public record SKColorPalette(
    string Primary,
    string Success,
    string Warning,
    string Danger,
    string Info,
    string Purple,
    string Cyan,
    string Muted
);

public class ThemeService : IThemeService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EduAnalytics", "settings.json");

    public AppThemeMode CurrentTheme { get; private set; } = AppThemeMode.System;
    public event Action? ThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    // ─── Public API ──────────────────────────────────────────────────────

    public void LoadAndApply()
    {
        try
        {
            Apply(AppThemeMode.System, persistSettings: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] LoadAndApply failed, falling back to Light: {ex}");
            try { Apply(AppThemeMode.Light); }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Light fallback also failed: {fallbackEx}");
            }
        }
    }

    public void Apply(AppThemeMode mode)
        => Apply(mode, persistSettings: true);

    public void ApplySystemForStartup()
        => Apply(AppThemeMode.System, persistSettings: false);

    private void Apply(AppThemeMode mode, bool persistSettings)
    {
        try
        {
            CurrentTheme = mode;
            var effective = mode == AppThemeMode.System ? DetectSystemTheme() : mode;
            SwapBrushColors(effective);
            SyncWpfUiTheme(effective);
            if (persistSettings)
                SaveSettings(mode);
            ThemeChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Apply failed: {ex}");
        }
    }

    /// <summary>WPF-UI'nin ApplicationThemeManager'ını da senkron eder — FluentWindow/ui:* kontrolleri için.</summary>
    private static void SyncWpfUiTheme(AppThemeMode effective)
    {
        try
        {
            var wpfUiTheme = effective == AppThemeMode.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(wpfUiTheme, Wpf.Ui.Controls.WindowBackdropType.Mica, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] WPF-UI theme sync failed: {ex.Message}");
        }
    }

    public void CycleTheme()
    {
        var effective = CurrentTheme == AppThemeMode.System ? DetectSystemTheme() : CurrentTheme;
        var next = effective == AppThemeMode.Dark ? AppThemeMode.Light : AppThemeMode.Dark;
        Apply(next);
    }

    public SKColorPalette GetChartPalette()
    {
        var effective = CurrentTheme == AppThemeMode.System ? DetectSystemTheme() : CurrentTheme;
        return effective == AppThemeMode.Dark
            ? new SKColorPalette("#7C5CFF", "#34D399", "#FBBF24", "#F87171",
                                 "#60A5FA", "#8B6CFF", "#67E8F9", "#71717A")
            : new SKColorPalette("#6D45F0", "#059669", "#B45309", "#DC2626",
                                 "#2563EB", "#7C5CFF", "#0891B2", "#71717A");
    }

    // ─── Internal ────────────────────────────────────────────────────────

    private static AppThemeMode DetectSystemTheme()
    {
        try
        {
            var wpfUiSystemTheme = ApplicationThemeManager.GetSystemTheme();
            if (wpfUiSystemTheme is SystemTheme.Dark or SystemTheme.HCBlack)
                return AppThemeMode.Dark;
            if (wpfUiSystemTheme is SystemTheme.Light or SystemTheme.HCWhite)
                return AppThemeMode.Light;

            const string personalizationPath =
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            var val = Registry.GetValue(
                personalizationPath,
                "AppsUseLightTheme",
                null);
            if (val is int appThemeValue)
                return appThemeValue == 0 ? AppThemeMode.Dark : AppThemeMode.Light;

            val = Registry.GetValue(
                personalizationPath,
                "SystemUsesLightTheme",
                null);
            if (val is int systemThemeValue)
                return systemThemeValue == 0 ? AppThemeMode.Dark : AppThemeMode.Light;
        }
        catch { }

        return AppThemeMode.Light;
    }

    private static void SwapBrushColors(AppThemeMode effective)
    {
        var source = effective == AppThemeMode.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/EduAnalytics.UI;component/{source}")
        };

        var appResources = Application.Current.Resources;
        foreach (var key in dict.Keys)
        {
            // Brush: in-place mutate (StaticResource tüketicilerini de günceller)
            if (dict[key] is SolidColorBrush src && appResources[key] is SolidColorBrush target)
            {
                if (!target.IsFrozen)
                {
                    target.Color = src.Color;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ThemeService] Brush '{key}' is frozen; replacing instead of mutating.");
                    appResources[key] = new SolidColorBrush(src.Color);
                }
                continue;
            }

            if (dict[key] is Color colorSrc && appResources.Contains(key))
            {
                appResources[key] = colorSrc;
                continue;
            }

            // DropShadowEffect: yalnızca DynamicResource ile tüketildiği için replace yeterli
            if (dict[key] is DropShadowEffect shadowSrc && appResources.Contains(key))
            {
                appResources[key] = shadowSrc.Clone();
            }
        }
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentTheme == AppThemeMode.System &&
            e.Category == UserPreferenceCategory.General)
        {
            Application.Current.Dispatcher.Invoke(() => Apply(AppThemeMode.System));
        }
    }

    // ─── Persist ─────────────────────────────────────────────────────────

    private static AppThemeMode LoadSaved()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return AppThemeMode.System;
            var json = File.ReadAllText(SettingsPath);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("theme", out var val) &&
                Enum.TryParse<AppThemeMode>(val.GetString(), out var mode))
            {
                // Eski ayar dosyalarında manuel seçim bilgisi yoktu; bu yüzden
                // kullanıcı istemeden Light/Dark'a kilitlenmesin diye System'e döneriz.
                if (!doc.RootElement.TryGetProperty("isManual", out var manual))
                    return AppThemeMode.System;

                return manual.GetBoolean() ? mode : AppThemeMode.System;
            }
        }
        catch { }
        return AppThemeMode.System;
    }

    private static void SaveSettings(AppThemeMode mode)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new
            {
                theme = mode.ToString(),
                isManual = mode != AppThemeMode.System
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
