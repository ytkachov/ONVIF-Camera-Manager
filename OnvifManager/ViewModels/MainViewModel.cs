using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public enum ParamTab
{
    Info,
    Video,
    Network,
    Ptz,
    Events
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private ParamTab _selectedTab = ParamTab.Info;
    [ObservableProperty] private string _connectionStatus = "Не подключено";
    [ObservableProperty] private string _profileText = "ONVIF Profile S";
    [ObservableProperty] private string _readyText = "Готово";

    public DiscoveryViewModel Discovery { get; }
    public DeviceInfoViewModel DeviceInfo { get; }
    public VideoConfigViewModel VideoConfig { get; }
    public NetworkConfigViewModel NetworkConfig { get; }
    public PtzViewModel Ptz { get; }
    public EventsViewModel Events { get; }

    private bool _disposed;

    public MainViewModel(
        DiscoveryViewModel discovery,
        DeviceInfoViewModel deviceInfo,
        VideoConfigViewModel videoConfig,
        NetworkConfigViewModel networkConfig,
        PtzViewModel ptz,
        EventsViewModel events)
    {
        Discovery = discovery;
        DeviceInfo = deviceInfo;
        VideoConfig = videoConfig;
        NetworkConfig = networkConfig;
        Ptz = ptz;
        Events = events;

        Discovery.CameraSelected += OnCameraSelected;
        Discovery.DeviceInfoRequested += () => SelectedTab = ParamTab.Info;
        Discovery.VideoConfigRequested += () => SelectedTab = ParamTab.Video;
        Discovery.NetworkConfigRequested += () => SelectedTab = ParamTab.Network;
    }

    private void OnCameraSelected()
    {
        var c = Discovery.SelectedCamera;
        ConnectionStatus = c?.IsConnected == true ? "● Подключено" : "● Не подключено";
    }

    public string ActiveCameraLabel
    {
        get
        {
            var c = Discovery.SelectedCamera;
            if (c == null) return "Камера не выбрана";
            var name = string.IsNullOrEmpty(c.Name) ? "(unnamed)" : c.Name;
            return string.IsNullOrEmpty(c.IpAddress) ? name : $"{name} ({c.IpAddress})";
        }
    }

    [RelayCommand]
    private Task ScanAsync() => Discovery.ScanCommand.ExecuteAsync(null);

    [RelayCommand]
    private void AddManual() => Discovery.OpenAddManualCommand.Execute(null);

    [RelayCommand]
    private void StartStream() => ReadyText = "Старт потока (заглушка)";

    [RelayCommand]
    private void StopStream() => ReadyText = "Стоп потока (заглушка)";

    [RelayCommand]
    private void Snapshot() => ReadyText = "Снимок сохранён (заглушка)";

    [RelayCommand]
    private void ToggleRecord() => ReadyText = "Запись (заглушка)";

    [RelayCommand]
    private void OpenSettings() => ReadyText = "Настройки (заглушка)";

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (Enum.TryParse<ParamTab>(tab, true, out var t))
            SelectedTab = t;
    }

    [RelayCommand]
    private async Task ApplyActiveAsync()
    {
        switch (SelectedTab)
        {
            case ParamTab.Video:
                await VideoConfig.SaveCommand.ExecuteAsync(null);
                break;
            case ParamTab.Network:
                await NetworkConfig.SaveCommand.ExecuteAsync(null);
                break;
            case ParamTab.Ptz:
                Ptz.ApplyCommand.Execute(null);
                break;
            case ParamTab.Events:
                Events.ApplyCommand.Execute(null);
                break;
            case ParamTab.Info:
                await DeviceInfo.LoadCurrentCommand.ExecuteAsync(null);
                break;
        }
    }

    [RelayCommand]
    private void CancelActive()
    {
        switch (SelectedTab)
        {
            case ParamTab.Ptz: Ptz.CancelCommand.Execute(null); break;
            case ParamTab.Events: Events.CancelCommand.Execute(null); break;
        }
    }

    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set
        {
            var t = (ParamTab)value;
            if (SelectedTab != t) SelectedTab = t;
        }
    }

    partial void OnSelectedTabChanged(ParamTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Discovery.CameraSelected -= OnCameraSelected;
    }
}
