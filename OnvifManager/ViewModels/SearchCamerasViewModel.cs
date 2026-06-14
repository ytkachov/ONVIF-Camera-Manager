using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Vendors;

namespace OnvifManager.ViewModels;

// Synology-Surveillance-Station-style discovery dialog VM: scans the network with a
// live-filling table + progress bar, lets the user enter credentials and test-connect a
// found camera, then add the chosen ones to the persistent list owned by DiscoveryViewModel.
public partial class SearchCamerasViewModel : ObservableObject
{
    private const int DiscoveryTimeoutMs = 5000;

    private readonly DiscoveryService _discoveryService;
    private readonly OnvifClientProvider _provider;
    private readonly VendorRegistry _vendors;
    private readonly DiscoveryViewModel _parent;
    private CancellationTokenSource? _scanCts;
    private DispatcherTimer? _progressTimer;
    private DateTime _scanStartedAt;

    public SearchCamerasViewModel(
        DiscoveryService discoveryService,
        OnvifClientProvider provider,
        VendorRegistry vendors,
        DiscoveryViewModel parent)
    {
        _discoveryService = discoveryService;
        _provider = provider;
        _vendors = vendors;
        _parent = parent;
    }

    public ObservableCollection<DiscoveredCameraRow> Results { get; } = new();

    // Single source of truth for interfaces lives on the parent; proxy so the dialog and
    // the main window agree on the selected adapter.
    public ObservableCollection<NetworkInterfaceItem> NetworkInterfaces => _parent.NetworkInterfaces;

    public NetworkInterfaceItem? SelectedNetworkInterface
    {
        get => _parent.SelectedNetworkInterface;
        set { _parent.SelectedNetworkInterface = value; OnPropertyChanged(); }
    }

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusText = "Нажмите «Поиск», чтобы найти ONVIF-камеры в сети";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedCommand))]
    private DiscoveredCameraRow? _selectedRow;

    public int FoundCount => Results.Count;

    [RelayCommand]
    private async Task ScanAsync()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        Results.Clear();
        OnPropertyChanged(nameof(FoundCount));
        IsScanning = true;
        ScanProgress = 0;
        StartProgressTimer();

        var target = SelectedNetworkInterface?.IpAddress;
        var targetText = string.IsNullOrEmpty(target) ? "все интерфейсы" : target;
        StatusText = $"Сканирование сети ({targetText})…";

        var progress = new Progress<CameraDevice>(OnCameraFound);
        try
        {
            var localIp = string.IsNullOrEmpty(target) ? null : target;
            await Task.Run(() => _discoveryService.DiscoverAsync(localIp, progress, DiscoveryTimeoutMs, ct), ct);

            StatusText = FoundCount == 0
                ? $"ONVIF-камер не найдено ({targetText})"
                : $"Найдено камер: {FoundCount} ({targetText})";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Сканирование отменено";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка сканирования: {ex.Message}";
        }
        finally
        {
            StopProgressTimer();
            ScanProgress = 100;
            IsScanning = false;
        }
    }

    // Reported on the UI thread (Progress<T> captures the dispatcher context).
    private void OnCameraFound(CameraDevice cam)
    {
        var key = $"{cam.IpAddress}:{cam.Port}";
        if (Results.Any(r => r.Key == key)) return;

        var existing = _parent.Cameras.FirstOrDefault(
            c => c.IpAddress == cam.IpAddress && c.Port == cam.Port);
        if (existing != null)
        {
            // Carry over stored credentials so testing a known camera works out of the box.
            cam.Username = existing.Username;
            cam.Password = existing.Password;
        }

        var row = new DiscoveredCameraRow(cam) { IsAdded = existing != null };
        Results.Add(row);
        OnPropertyChanged(nameof(FoundCount));
    }

    private bool CanTestSelected() => SelectedRow is { IsTesting: false };

    [RelayCommand(CanExecute = nameof(CanTestSelected))]
    private async Task TestSelectedAsync()
    {
        var row = SelectedRow;
        if (row == null) return;

        row.IsTesting = true;
        TestSelectedCommand.NotifyCanExecuteChanged();
        StatusText = $"Проверка подключения к {row.Device.IpAddress}…";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await Task.Run(() => ProbeAsync(row.Device, cts.Token), cts.Token);
            StatusText = row.Device.IsConnected
                ? $"{row.Device.IpAddress}: подключение успешно"
                : $"{row.Device.IpAddress}: {row.Device.StatusMessage}";
        }
        catch (Exception ex)
        {
            StatusText = $"{row.Device.IpAddress}: {ex.Message}";
        }
        finally
        {
            row.IsTesting = false;
            TestSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ProbeAsync(CameraDevice cam, CancellationToken ct)
    {
        try
        {
            var client = _provider.Get(cam);
            var deviceService = new DeviceService(client);
            await deviceService.GetDeviceInformationAsync(ct).ConfigureAwait(false);

            var adapter = _vendors.For(cam);
            var friendly = await adapter.GetFriendlyNameAsync(client, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(friendly))
                Application.Current?.Dispatcher.BeginInvoke(() => cam.Name = friendly!.Trim());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                cam.IsConnected = false;
                cam.StatusMessage = msg;
            });
        }
    }

    private bool CanAddSelected() => SelectedRow is { IsAdded: false };

    [RelayCommand(CanExecute = nameof(CanAddSelected))]
    private void AddSelected()
    {
        if (SelectedRow != null) Add(SelectedRow, select: true);
    }

    [RelayCommand]
    private void AddAll()
    {
        var pending = Results.Where(r => !r.IsAdded).ToList();
        foreach (var row in pending) Add(row, select: false);
        StatusText = pending.Count == 0
            ? "Все найденные камеры уже в списке"
            : $"Добавлено камер: {pending.Count}";
    }

    private void Add(DiscoveredCameraRow row, bool select)
    {
        var cam = row.Device;
        var existing = _parent.Cameras.FirstOrDefault(
            c => c.IpAddress == cam.IpAddress && c.Port == cam.Port);
        if (existing == null)
        {
            if (string.IsNullOrEmpty(cam.Name))
            {
                cam.Name = !string.IsNullOrEmpty(cam.Manufacturer)
                    ? $"{cam.Manufacturer} {cam.Model}".Trim()
                    : $"{cam.IpAddress}:{cam.Port}";
            }
            _parent.Cameras.Add(cam);
            if (select) _parent.SelectedCamera = cam;
        }
        else if (select)
        {
            _parent.SelectedCamera = existing;
        }

        row.IsAdded = true;
        AddSelectedCommand.NotifyCanExecuteChanged();
        if (select) StatusText = $"Камера {cam.IpAddress} добавлена в список";
    }

    private void StartProgressTimer()
    {
        _scanStartedAt = DateTime.UtcNow;
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _progressTimer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - _scanStartedAt).TotalMilliseconds;
            ScanProgress = Math.Min(97, elapsed / DiscoveryTimeoutMs * 100);
        };
        _progressTimer.Start();
    }

    private void StopProgressTimer()
    {
        _progressTimer?.Stop();
        _progressTimer = null;
    }

    public void Cancel()
    {
        _scanCts?.Cancel();
        StopProgressTimer();
    }
}
