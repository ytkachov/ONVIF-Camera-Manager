using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OnvifManager.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool b => b,
            _ => true
        };
        var invert = parameter is string s && s == "Invert";
        if (invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        var invert = parameter is string s && s == "Invert";
        return invert ? !visible : visible;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}
