using System.IO;
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
        ShutdownMode = ShutdownMode.OnMainWindowClose;

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

        services.AddSingleton<IPasswordProtector, DpapiPasswordProtector>();
        services.AddSingleton<ICameraStore>(sp =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "SeaGull", "cameras.json");
            return new JsonCameraStore(path, sp.GetRequiredService<IPasswordProtector>());
        });

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
        MainWindow = mainWindow;
        mainWindow.Loaded += (_, _) =>
        {
            CloseSplash(splash, splashShownAt);
            _serviceProvider.GetRequiredService<DiscoveryViewModel>().StartBackgroundProbeOfSaved();
        };
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
        if (_serviceProvider != null)
        {
            try
            {
                _serviceProvider.GetRequiredService<DiscoveryViewModel>()
                    .FlushPendingSaveAsync()
                    .GetAwaiter().GetResult();
            }
            catch { }
            _serviceProvider.Dispose();
        }
        base.OnExit(e);

        // Pending background HTTP / ISAPI tasks may keep the process alive after the
        // last WPF window closes; force a hard exit so we never leak a zombie.
        Environment.Exit(e.ApplicationExitCode);
    }
}
