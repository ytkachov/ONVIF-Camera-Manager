using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public enum AppView
{
    Discovery,
    DeviceInfo,
    VideoConfig,
    NetworkConfig
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private AppView _currentView = AppView.DeviceInfo;
    [ObservableProperty] private object? _bottomViewModel;
    [ObservableProperty] private string _statusText = "Ready";

    public DiscoveryViewModel Discovery { get; }
    public DeviceInfoViewModel DeviceInfo { get; }
    public VideoConfigViewModel VideoConfig { get; }
    public NetworkConfigViewModel NetworkConfig { get; }

    private bool _disposed;

    public MainViewModel(
        DiscoveryViewModel discovery,
        DeviceInfoViewModel deviceInfo,
        VideoConfigViewModel videoConfig,
        NetworkConfigViewModel networkConfig)
    {
        Discovery = discovery;
        DeviceInfo = deviceInfo;
        VideoConfig = videoConfig;
        NetworkConfig = networkConfig;

        Discovery.CameraSelected += OnCameraSelected;
        Discovery.DeviceInfoRequested += OnDeviceInfoRequested;
        Discovery.VideoConfigRequested += OnVideoConfigRequested;
        Discovery.NetworkConfigRequested += OnNetworkConfigRequested;

        BottomViewModel = DeviceInfo;
    }

    private void OnCameraSelected() => NavigateTo(AppView.DeviceInfo);
    private void OnDeviceInfoRequested() => NavigateTo(AppView.DeviceInfo);
    private void OnVideoConfigRequested() => NavigateTo(AppView.VideoConfig);
    private void OnNetworkConfigRequested() => NavigateTo(AppView.NetworkConfig);

    [RelayCommand]
    private void NavigateToDeviceInfo() => NavigateTo(AppView.DeviceInfo);

    [RelayCommand]
    private void NavigateToVideoConfig() => NavigateTo(AppView.VideoConfig);

    [RelayCommand]
    private void NavigateToNetworkConfig() => NavigateTo(AppView.NetworkConfig);

    public void NavigateTo(AppView view)
    {
        CurrentView = view;
        BottomViewModel = view switch
        {
            AppView.DeviceInfo => DeviceInfo,
            AppView.VideoConfig => VideoConfig,
            AppView.NetworkConfig => NetworkConfig,
            _ => DeviceInfo
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Discovery.CameraSelected -= OnCameraSelected;
        Discovery.DeviceInfoRequested -= OnDeviceInfoRequested;
        Discovery.VideoConfigRequested -= OnVideoConfigRequested;
        Discovery.NetworkConfigRequested -= OnNetworkConfigRequested;
    }
}
