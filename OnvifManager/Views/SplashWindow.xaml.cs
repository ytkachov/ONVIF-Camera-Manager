using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace OnvifManager.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{ResolveVersion()}";
    }

    public void StartProgress(TimeSpan duration)
    {
        var animation = new DoubleAnimation(0, 280, duration)
        {
            FillBehavior = FillBehavior.HoldEnd
        };
        ProgressFill.BeginAnimation(Rectangle.WidthProperty, animation);
    }

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            var file = FileVersionInfo.GetVersionInfo(location);
            if (!string.IsNullOrWhiteSpace(file.ProductVersion))
                return file.ProductVersion!;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
