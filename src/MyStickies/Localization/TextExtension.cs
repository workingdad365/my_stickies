using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using MyStickies.Models;

namespace MyStickies.Localization;

/// <summary>언어가 바뀌면 XAML 문구와 날짜·숫자 서식을 함께 갱신함</summary>
public sealed class TextExtension(string key) : MarkupExtension
{
    public BindingBase? Value { get; set; }
    public bool Relative { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Value is null) return Strings.CreateBinding(key).ProvideValue(serviceProvider);
        var binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = new FormatConverter(Relative) };
        binding.Bindings.Add(Strings.CreateBinding(key));
        binding.Bindings.Add(Value);
        return binding.ProvideValue(serviceProvider);
    }

    private sealed class FormatConverter(bool relative) : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string format || values[1] == DependencyProperty.UnsetValue)
                return string.Empty;
            var value = relative && values[1] is DateTime date
                ? RelativeTime.Format(date, DateTime.Now) : values[1];
            return string.Format(Strings.Current.Culture, format, value);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
