using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Services;

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

    private readonly SnapshotService _snapshot;
    private readonly OnvifClientProvider _provider;
    private bool _disposed;

    public MainViewModel(
        DiscoveryViewModel discovery,
        DeviceInfoViewModel deviceInfo,
        VideoConfigViewModel videoConfig,
        NetworkConfigViewModel networkConfig,
        PtzViewModel ptz,
        EventsViewModel events,
        SnapshotService snapshot,
        OnvifClientProvider provider)
    {
        Discovery = discovery;
        DeviceInfo = deviceInfo;
        VideoConfig = videoConfig;
        NetworkConfig = networkConfig;
        Ptz = ptz;
        Events = events;
        _snapshot = snapshot;
        _provider = provider;

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
    private async Task SnapshotAsync()
    {
        var camera = Discovery.SelectedCamera;
        if (camera == null)
        {
            ReadyText = "Сначала выберите камеру";
            return;
        }

        var profileToken = VideoConfig.SelectedProfile?.Token
                           ?? camera.Profiles.FirstOrDefault()?.Token;

        ReadyText = "Снимок…";
        try
        {
            if (string.IsNullOrEmpty(profileToken))
            {
                var media = new MediaService(_provider.Get(camera));
                var profiles = await media.GetProfilesAsync();
                profileToken = profiles.FirstOrDefault()?.Token;
            }
            if (string.IsNullOrEmpty(profileToken))
                throw new InvalidOperationException("У камеры нет media profile");

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "OnvifManager");
            var result = await _snapshot.CaptureAsync(camera, profileToken, dir);
            ReadyText = $"Снимок сохранён: {Path.GetFileName(result.Path)}";
            Discovery.StatusText = $"Снимок: {result.Path} ({result.Bytes / 1024} KB)";
        }
        catch (OperationCanceledException) { ReadyText = "Снимок отменён"; }
        catch (Exception ex)
        {
            ReadyText = $"Ошибка снимка: {ex.Message}";
            Discovery.StatusText = $"Ошибка снимка: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleRecord() => ReadyText = "Запись (заглушка)";

    [RelayCommand]
    private async Task RebootCameraAsync()
    {
        var camera = Discovery.SelectedCamera;
        if (camera == null || !camera.IsConnected)
        {
            ReadyText = "Сначала выберите подключённую камеру";
            return;
        }

        var name = string.IsNullOrEmpty(camera.Name) ? camera.IpAddress : camera.Name;
        var confirm = MessageBox.Show(
            $"Перезагрузить камеру «{name}»?\nСвязь оборвётся примерно на 1–2 минуты.",
            "Перезагрузка камеры",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        ReadyText = "Перезагрузка камеры…";
        try
        {
            var client = _provider.Get(camera);
            var device = new DeviceService(client);
            var reply = await device.RebootAsync();
            camera.IsConnected = false;
            camera.StatusMessage = "Reboot requested";
            ConnectionStatus = "● Не подключено";
            Discovery.StatusText = $"Перезагрузка «{name}»: {reply}";
            ReadyText = "Камера перезагружается";
        }
        catch (Exception ex)
        {
            ReadyText = $"Ошибка перезагрузки: {ex.Message}";
            Discovery.StatusText = $"Ошибка перезагрузки: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenSettings() => ReadyText = "Настройки (заглушка)";

    [RelayCommand]
    private async Task ClearCameraListAsync()
    {
        if (Discovery.Cameras.Count == 0)
        {
            ReadyText = "Список камер уже пуст";
            return;
        }

        var confirm = MessageBox.Show(
            $"Очистить список камер ({Discovery.Cameras.Count})?\nСохранённая конфигурация будет удалена.",
            "Очистка списка",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        await Discovery.ClearAllCommand.ExecuteAsync(null);
        ReadyText = "Список камер очищен";
    }

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
                await DeviceInfo.SaveCommand.ExecuteAsync(null);
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
