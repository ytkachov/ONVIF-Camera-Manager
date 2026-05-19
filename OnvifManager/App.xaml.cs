using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OnvifManager.Services;
using OnvifManager.Vendors;
using OnvifManager.ViewModels;
using OnvifManager.Views;

namespace OnvifManager;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;

    private static readonly TimeSpan SplashMinDuration = TimeSpan.FromMilliseconds(3000);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();
        splash.StartProgress(SplashMinDuration);
        var splashShownAt = DateTime.UtcNow;

        var services = new ServiceCollection();

        services.AddSingleton(new OnvifClientOptions
        {
            AllowSelfSignedCertificates = true,
            Timeout = TimeSpan.FromSeconds(30)
        });
        services.AddSingleton<OnvifClientProvider>();

        services.AddSingleton<IVendorAdapter, HikvisionVendorAdapter>();
        services.AddSingleton<VendorRegistry>();

        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<SnapshotService>();

        services.AddSingleton<DiscoveryViewModel>();
        services.AddSingleton<DeviceInfoViewModel>();
        services.AddSingleton<VideoConfigViewModel>();
        services.AddSingleton<NetworkConfigViewModel>();
        services.AddSingleton<PtzViewModel>();
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Loaded += (_, _) => CloseSplash(splash, splashShownAt);
        mainWindow.Show();
    }

    private static void CloseSplash(SplashWindow splash, DateTime shownAt)
    {
        var elapsed = DateTime.UtcNow - shownAt;
        var remaining = SplashMinDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            var timer = new DispatcherTimer { Interval = remaining };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                splash.Close();
            };
            timer.Start();
        }
        else
        {
            splash.Close();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
