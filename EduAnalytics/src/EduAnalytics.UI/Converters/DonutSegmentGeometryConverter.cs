using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EduAnalytics.UI.Converters;

public sealed class DonutSegmentGeometryConverter : IMultiValueConverter
{
    private const double Center = 84;
    private const double Radius = 57;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var correct = ToDouble(values.ElementAtOrDefault(0));
        var wrong = ToDouble(values.ElementAtOrDefault(1));
        var empty = ToDouble(values.ElementAtOrDefault(2));
        var total = correct + wrong + empty;
        if (total <= 0)
            return Geometry.Empty;

        var index = int.TryParse(parameter?.ToString(), out var parsed) ? parsed : 0;
        var parts = new[] { correct, wrong, empty };
        if (index < 0 || index >= parts.Length || parts[index] <= 0)
            return Geometry.Empty;

        var previous = parts.Take(index).Sum();
        var startAngle = -90 + previous / total * 360;
        var sweep = parts[index] / total * 360;
        if (sweep >= 359.99)
            sweep = 359.99;

        var endAngle = startAngle + sweep;
        var start = PointOnCircle(startAngle);
        var end = PointOnCircle(endAngle);

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(Radius, Radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweep > 180
        });

        return new PathGeometry(new[] { figure });
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static double ToDouble(object? value)
        => value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => 0d
        };

    private static Point PointOnCircle(double angle)
    {
        var radians = angle * Math.PI / 180.0;
        return new Point(
            Center + Radius * Math.Cos(radians),
            Center + Radius * Math.Sin(radians));
    }
}

