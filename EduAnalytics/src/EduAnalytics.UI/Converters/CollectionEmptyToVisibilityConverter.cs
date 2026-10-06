using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EduAnalytics.UI.Converters;

/// <summary>
/// int/collection count → Visibility.
/// Varsayılan veya parametre "empty" iken: 0 → Visible, >0 → Collapsed (boş durum göstergesi için).
/// Parametre "notempty" / "invert" iken: 0 → Collapsed, >0 → Visible (liste için).
/// </summary>
public class CollectionEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i    => i,
            IEnumerable<object> e => e.Count(),
            System.Collections.IEnumerable e => e.Cast<object>().Count(),
            _ => -1
        };

        var showWhenEmpty = true;
        if (parameter is string p)
        {
            if (string.Equals(p, "notempty", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "invert", StringComparison.OrdinalIgnoreCase))
                showWhenEmpty = false;
        }

        var isEmpty = count == 0;
        var visible = showWhenEmpty ? isEmpty : !isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
