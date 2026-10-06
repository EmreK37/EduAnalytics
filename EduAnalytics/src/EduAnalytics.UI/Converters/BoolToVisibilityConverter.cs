using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EduAnalytics.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isTruthy = value switch
        {
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            long l => l != 0,
            null => false,
            _ => true
        };

        // "invert" parametresi: ters çevir (görünürlük için ListBox.Count == 0 gibi durumlar)
        if (parameter is string p && string.Equals(p, "invert", StringComparison.OrdinalIgnoreCase))
            isTruthy = !isTruthy;

        // Button.IsEnabled için bool dönüşü (Visibility yerine)
        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return isTruthy;

        return isTruthy ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Enum ve parametre string'i eşitse true döner. Radio button gruplarında kullanılır.
/// </summary>
public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            try
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }
            catch
            {
                return System.Windows.Data.Binding.DoNothing;
            }
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// MultiBinding ile iki string'i karşılaştırır. Sidebar aktif menü göstergesi için.
/// </summary>
public class StringEqualsMultiConverter : System.Windows.Data.IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var a = values[0]?.ToString();
        var b = values[1]?.ToString();
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Başarı seviyesi (KRİTİK/ZAYIF/ORTA/İYİ) → renkli arka plana dönüştürür.
/// </summary>
public class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value as string ?? "";
        return level switch
        {
            "KRİTİK" => System.Windows.Application.Current.Resources["DangerBrush"],
            "ZAYIF" => System.Windows.Application.Current.Resources["WarningBrush"],
            "ORTA" => System.Windows.Application.Current.Resources["PrimaryBrush"],
            "İYİ" => System.Windows.Application.Current.Resources["SuccessBrush"],
            _ => System.Windows.Application.Current.Resources["TextSecondaryBrush"]
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RateThresholdToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rate = value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            _ => 0d
        };

        var resources = Application.Current.Resources;
        if (rate >= 70) return resources["SuccessBrush"];
        if (rate >= 50) return resources["WarningBrush"];
        return resources["DangerBrush"];
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class FirstWordConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
