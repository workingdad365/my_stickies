using System.Globalization;
using System.Windows.Data;
using MyStickies.Models;

namespace MyStickies.Converters;

/// <summary>DateTime을 "3분 전" 형식으로 변환</summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTime dt ? RelativeTime.Format(dt, DateTime.Now) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
