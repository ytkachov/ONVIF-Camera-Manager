using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OnvifManager.Services;
using OnvifManager.ViewModels;
using OnvifManager.Views;

namespace OnvifManager;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddSingleton(new OnvifClientOptions
        {
            AllowSelfSignedCertificates = true,
            Timeout = TimeSpan.FromSeconds(15)
        });
        services.AddSingleton<OnvifClientProvider>();

        services.AddSingleton<DiscoveryService>();

        services.AddSingleton<DiscoveryViewModel>();
        services.AddSingleton<DeviceInfoViewModel>();
        services.AddSingleton<VideoConfigViewModel>();
        services.AddSingleton<NetworkConfigViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
