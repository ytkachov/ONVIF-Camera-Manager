using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OnvifManager.Models;

namespace OnvifManager.Converters;

public class CameraStatusToBrushConverter : IValueConverter
{
    private static readonly Brush OnlineBrush = new SolidColorBrush(Color.FromRgb(0x61, 0xC5, 0x54));
    private static readonly Brush StoppedBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xBD, 0x4F));
    private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x58, 0x4A));
    private static readonly Brush OfflineBrush = new SolidColorBrush(Color.FromRgb(0x6C, 0x72, 0x7F));

    static CameraStatusToBrushConverter()
    {
        OnlineBrush.Freeze();
        StoppedBrush.Freeze();
        WarnBrush.Freeze();
        OfflineBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CameraStatus s)
        {
            return s switch
            {
                CameraStatus.Online => OnlineBrush,
                CameraStatus.Stopped => StoppedBrush,
                CameraStatus.Warning => WarnBrush,
                _ => OfflineBrush
            };
        }
        return OfflineBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
