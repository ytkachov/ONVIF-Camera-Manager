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
    [ObservableProperty] private bool _isFullscreen;

    public DiscoveryViewModel Discovery { get; }
    public DeviceInfoViewModel DeviceInfo { get; }
    public VideoConfigViewModel VideoConfig { get; }
    public NetworkConfigViewModel NetworkConfig { get; }
    public PtzViewModel Ptz { get; }
    public EventsViewModel Events { get; }
    public VideoPlayerService VideoPlayer { get; }
    public AppSettingsService Settings { get; }
    public VendorParametersHostViewModel VendorHost { get; }

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
        OnvifClientProvider provider,
        VideoPlayerService videoPlayer,
        AppSettingsService settings,
        VendorParametersHostViewModel vendorHost)
    {
        Discovery = discovery;
        DeviceInfo = deviceInfo;
        VideoConfig = videoConfig;
        NetworkConfig = networkConfig;
        Ptz = ptz;
        Events = events;
        VideoPlayer = videoPlayer;
        Settings = settings;
        VendorHost = vendorHost;
        _snapshot = snapshot;
        _provider = provider;

        Discovery.CameraSelected += OnCameraSelected;
        Discovery.DeviceInfoRequested += () => SelectedTab = ParamTab.Info;
        Discovery.VideoConfigRequested += () => SelectedTab = ParamTab.Video;
        Discovery.NetworkConfigRequested += () => SelectedTab = ParamTab.Network;
        VideoConfig.StreamProfileChanged += OnStreamProfileChanged;
    }

    public bool IsFullMode
    {
        get => Settings.ViewMode == AppViewMode.Full;
        set
        {
            var mode = value ? AppViewMode.Full : AppViewMode.Onvif;
            if (Settings.ViewMode == mode) return;
            Settings.ViewMode = mode;
            Settings.Save();
            OnPropertyChanged();
            _ = RefreshVendorParamsAsync();
        }
    }

    public bool VendorAvailable => VendorHost.HasProfile(Discovery.SelectedCamera);

    private async Task RefreshVendorParamsAsync()
    {
        if (IsFullMode)
            await VendorHost.LoadAsync(Discovery.SelectedCamera);
        else
            VendorHost.Clear();
    }

    private void OnCameraSelected()
    {
        var c = Discovery.SelectedCamera;
        ConnectionStatus = c?.IsConnected == true ? "● Подключено" : "● Не подключено";
        VideoPlayer.Stop();
        OnPropertyChanged(nameof(VendorAvailable));
        _ = RefreshVendorParamsAsync();
        if (Settings.AutoPlayOnSelect && c?.IsConnected == true)
            _ = StartStreamAsync();
    }

    private void OnStreamProfileChanged()
    {
        if (!VideoPlayer.IsPlaying) return;
        _ = StartStreamAsync();
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
    private async Task StartStreamAsync()
    {
        var camera = Discovery.SelectedCamera;
        if (camera == null)
        {
            ReadyText = "Сначала выберите камеру";
            return;
        }

        string uri;
        try
        {
            var media = new MediaService(_provider.Get(camera));
            var profileToken = VideoConfig.SelectedProfile?.Token
                               ?? camera.Profiles.FirstOrDefault()?.Token;
            if (string.IsNullOrEmpty(profileToken))
            {
                var profiles = await media.GetProfilesAsync();
                profileToken = profiles.FirstOrDefault()?.Token;
            }
            if (string.IsNullOrEmpty(profileToken))
                throw new InvalidOperationException("У камеры нет media profile");
            uri = await media.GetStreamUriAsync(profileToken);
        }
        catch (Exception ex)
        {
            ReadyText = $"Ошибка получения RTSP: {ex.Message}";
            return;
        }

        var recordPath = VideoPlayer.IsRecording
            ? VideoPlayer.CurrentRecordingPath
            : null;
        VideoPlayer.Play(uri, camera.Username, camera.Password, recordPath);
        ReadyText = $"Запуск потока: {uri}";
    }

    [RelayCommand]
    private void StopStream()
    {
        VideoPlayer.Stop();
        ReadyText = "Поток остановлен";
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    private void ExitFullscreen()
    {
        if (IsFullscreen) IsFullscreen = false;
    }

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
    private void ToggleRecord()
    {
        var camera = Discovery.SelectedCamera;
        if (camera == null)
        {
            ReadyText = "Сначала выберите камеру";
            return;
        }

        if (VideoPlayer.IsRecording)
        {
            var path = VideoPlayer.CurrentRecordingPath;
            VideoPlayer.StopRecording();
            ReadyText = $"Запись остановлена: {Path.GetFileName(path)}";
            Discovery.StatusText = $"Запись сохранена: {path}";
            return;
        }

        if (!VideoPlayer.IsPlaying)
        {
            ReadyText = "Сначала запустите поток";
            return;
        }

        try
        {
            var dir = string.IsNullOrWhiteSpace(Settings.RecordingsPath)
                ? AppSettings.DefaultRecordingsPath()
                : Settings.RecordingsPath;
            Directory.CreateDirectory(dir);
            var safeName = string.IsNullOrEmpty(camera.Name)
                ? camera.IpAddress
                : camera.Name;
            foreach (var ch in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(ch, '_');
            var fileName = $"{safeName}-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ts";
            var fullPath = Path.Combine(dir, fileName);
            VideoPlayer.StartRecording(fullPath);
            ReadyText = $"Запись: {fileName}";
            Discovery.StatusText = $"Идёт запись в {fullPath}";
        }
        catch (Exception ex)
        {
            ReadyText = $"Ошибка записи: {ex.Message}";
            Discovery.StatusText = $"Ошибка записи: {ex.Message}";
        }
    }

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
    private void OpenSettings()
    {
        var dlg = new Views.SettingsDialog(Settings)
        {
            Owner = Application.Current?.MainWindow
        };
        if (dlg.ShowDialog() == true)
            ReadyText = "Настройки сохранены";
    }

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

        if (IsFullMode)
        {
            try
            {
                var written = await VendorHost.SaveAsync(Discovery.SelectedCamera);
                if (written > 0)
                    ReadyText = $"Vendor-параметры сохранены: {written}";
            }
            catch (Exception ex)
            {
                ReadyText = $"Ошибка vendor-параметров: {ex.Message}";
            }
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
        VideoConfig.StreamProfileChanged -= OnStreamProfileChanged;
    }
}
