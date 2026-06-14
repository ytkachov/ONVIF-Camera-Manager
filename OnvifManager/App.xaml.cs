using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OnvifManager.Services;
using OnvifManager.Vendors;
using OnvifManager.Vendors.Config;
using OnvifManager.ViewModels;
using OnvifManager.Views;

namespace OnvifManager;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;
    private SplashWindow? _splash;
    private Thread? _splashThread;

    private static readonly TimeSpan SplashMinDuration = TimeSpan.FromMilliseconds(3000);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var splashShownAt = DateTime.UtcNow;
        // The splash runs on its own STA thread with a dedicated Dispatcher, so its progress
        // bar keeps animating while this (UI) thread builds the DI graph and inits LibVLC.
        // Running both on one thread froze the bar: the animation clock can't tick while the
        // thread is blocked in the synchronous startup work.
        ShowSplashOnOwnThread();

        var services = new ServiceCollection();

        services.AddSingleton(new OnvifClientOptions
        {
            AllowSelfSignedCertificates = true,
            Timeout = TimeSpan.FromSeconds(30)
        });
        services.AddSingleton<OnvifClientProvider>();

        services.AddSingleton<IVendorAdapter, HikvisionVendorAdapter>();
        services.AddSingleton<IVendorAdapter, DahuaVendorAdapter>();
        services.AddSingleton<VendorRegistry>();

        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<SnapshotService>();
        services.AddSingleton<VideoPlayerService>();
        services.AddSingleton<AppSettingsService>();

        services.AddSingleton<VendorProfileStore>(_ =>
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "Vendors", "Profiles");
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userDir = Path.Combine(appData, "SeaGull", "vendors");
            return new VendorProfileStore(new[] { bundled, userDir });
        });
        services.AddSingleton<IVendorProtocol, IsapiProtocol>();
        services.AddSingleton<VendorParameterService>();
        services.AddSingleton<VendorParametersHostViewModel>();

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
            CloseSplash(splashShownAt);
            _serviceProvider.GetRequiredService<DiscoveryViewModel>().StartBackgroundProbeOfSaved();
        };
        mainWindow.Show();
    }

    private void ShowSplashOnOwnThread()
    {
        using var ready = new ManualResetEventSlim(false);
        _splashThread = new Thread(() =>
        {
            _splash = new SplashWindow();
            _splash.Show();
            _splash.StartProgress(SplashMinDuration);
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "SplashThread"
        };
        _splashThread.SetApartmentState(ApartmentState.STA);
        _splashThread.Start();
        ready.Wait();
    }

    private void CloseSplash(DateTime shownAt)
    {
        var splash = _splash;
        if (splash == null) return;

        var remaining = SplashMinDuration - (DateTime.UtcNow - shownAt);
        var delay = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;

        // Marshal onto the splash's own dispatcher to close it and stop that thread's loop.
        splash.Dispatcher.InvokeAsync(() =>
        {
            void Finish()
            {
                splash.Close();
                splash.Dispatcher.InvokeShutdown();
            }

            if (delay <= TimeSpan.Zero) { Finish(); return; }

            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) => { timer.Stop(); Finish(); };
            timer.Start();
        });
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
