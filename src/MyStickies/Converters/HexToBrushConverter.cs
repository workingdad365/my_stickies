using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MyStickies.Converters;

/// <summary>"#RRGGBB" 문자열을 SolidColorBrush로 변환</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Brush(value as string ?? "#CCCCCC");

    /// <summary>16진 색상 문자열을 캐시된 고정 브러시로 변환</summary>
    public static SolidColorBrush Brush(string hex)
    {
        if (!Cache.TryGetValue(hex, out var brush))
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            brush = new SolidColorBrush(color);
            brush.Freeze();
            Cache[hex] = brush;
        }
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
