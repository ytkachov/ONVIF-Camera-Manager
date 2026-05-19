using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Vendors;

namespace OnvifManager.ViewModels;

public class NetworkInterfaceItem
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string DisplayText => string.IsNullOrEmpty(IpAddress) ? Name : $"{Name} [{IpAddress}]";
    public override string ToString() => DisplayText;
}

public partial class DiscoveryViewModel : ObservableObject, IDisposable
{
    private const int SaveDebounceMs = 300;
    private const int ProbeMaxParallel = 5;
    private const int ProbeTimeoutMs = 8000;

    private readonly DiscoveryService _discoveryService;
    private readonly OnvifClientProvider _provider;
    private readonly VendorRegistry _vendors;
    private readonly ICameraStore? _store;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _saveDebounceCts;
    private CancellationTokenSource? _initCts;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;
    private int _autosaveSuspended;

    [ObservableProperty] private ObservableCollection<CameraDevice> _cameras = new();
    [ObservableProperty] private CameraDevice? _selectedCamera;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Нажмите «Поиск камер» для сканирования сети";
    [ObservableProperty] private ObservableCollection<NetworkInterfaceItem> _networkInterfaces = new();
    [ObservableProperty] private NetworkInterfaceItem? _selectedNetworkInterface;
    [ObservableProperty] private string _manualIp = "";
    [ObservableProperty] private string _manualPort = "80";
    [ObservableProperty] private string _manualUsername = "admin";
    [ObservableProperty] private string _manualPassword = "";
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _isProbing;
    [ObservableProperty] private string _lastProbeError = "";

    public ICollectionView CamerasView { get; }

    public event Action? CameraSelected;
    public event Action? DeviceInfoRequested;
    public event Action? VideoConfigRequested;
    public event Action? NetworkConfigRequested;
    public event Action? AddManualRequested;

    public DiscoveryViewModel(DiscoveryService discoveryService, OnvifClientProvider provider)
        : this(discoveryService, provider, VendorRegistry.Empty, store: null) { }

    public DiscoveryViewModel(
        DiscoveryService discoveryService,
        OnvifClientProvider provider,
        VendorRegistry vendors,
        ICameraStore? store)
    {
        _discoveryService = discoveryService;
        _provider = provider;
        _vendors = vendors;
        _store = store;

        LoadNetworkInterfaces();
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

        if (_store != null)
        {
            _autosaveSuspended++;
            try
            {
                foreach (var saved in _store.Load())
                    Cameras.Add(saved);
            }
            finally { _autosaveSuspended--; }

            if (Cameras.Count > 0)
                StatusText = $"Загружено камер: {Cameras.Count}";
        }

        Cameras.CollectionChanged += OnCamerasCollectionChanged;

        CamerasView = CollectionViewSource.GetDefaultView(Cameras);
        CamerasView.Filter = FilterCamera;
    }

    private void OnCamerasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_autosaveSuspended > 0 || _store is null) return;
        if (e.Action == NotifyCollectionChangedAction.Move) return;
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (_store is null || _disposed) return;

        // Snapshot must be taken on the UI thread (current call site) — the worker
        // thread cannot safely enumerate ObservableCollection.
        var snapshot = Cameras.ToArray();

        _saveDebounceCts?.Cancel();
        _saveDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _saveDebounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, cts.Token).ConfigureAwait(false);
                await SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        });
    }

    public Task FlushPendingSaveAsync()
    {
        if (_store is null) return Task.CompletedTask;
        _saveDebounceCts?.Cancel();
        var snapshot = Cameras.ToArray();
        return Task.Run(() => SaveAsync(snapshot));
    }

    private async Task SaveAsync(CameraDevice[] snapshot)
    {
        if (_store is null) return;

        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _store.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Swallowed: callers can't react to autosave failures synchronously.
            // Surfacing via StatusText would require a Dispatcher hop, which risks
            // a deadlock if the UI thread is currently blocked inside FlushPendingSaveAsync.
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private bool FilterCamera(object item)
    {
        if (item is not CameraDevice c) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        var f = FilterText.Trim();
        return (c.Name?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
            || (c.IpAddress?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
            || (c.Model?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
            || (c.Manufacturer?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    partial void OnFilterTextChanged(string value) => CamerasView.Refresh();

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(LoadNetworkInterfaces);
    }

    private void LoadNetworkInterfaces()
    {
        var previousIp = SelectedNetworkInterface?.IpAddress;

        var interfaces = new List<NetworkInterfaceItem>
        {
            new() { Name = "Any (default)", IpAddress = "" }
        };

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    interfaces.Add(new NetworkInterfaceItem
                    {
                        Name = ni.Name,
                        IpAddress = addr.Address.ToString()
                    });
                }
            }
        }

        NetworkInterfaces = new ObservableCollection<NetworkInterfaceItem>(interfaces);
        SelectedNetworkInterface = NetworkInterfaces.FirstOrDefault(i => i.IpAddress == previousIp)
                                   ?? NetworkInterfaces.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        var target = SelectedNetworkInterface?.IpAddress;
        var targetText = string.IsNullOrEmpty(target) ? "all interfaces" : target;
        StatusText = $"Сканирование сети ({targetText})…";

        try
        {
            var localIp = string.IsNullOrEmpty(target) ? null : target;
            var found = await Task.Run(() => _discoveryService.DiscoverAsync(localIp, ct), ct);

            int added = 0;
            int updated = 0;
            foreach (var cam in found)
            {
                var existing = Cameras.FirstOrDefault(
                    c => c.IpAddress == cam.IpAddress && c.Port == cam.Port);
                if (existing != null)
                {
                    if (!string.IsNullOrEmpty(cam.Endpoint)) existing.Endpoint = cam.Endpoint;
                    if (!string.IsNullOrEmpty(cam.Manufacturer)) existing.Manufacturer = cam.Manufacturer;
                    if (!string.IsNullOrEmpty(cam.Model)) existing.Model = cam.Model;
                    if (!string.IsNullOrEmpty(cam.FirmwareVersion)) existing.FirmwareVersion = cam.FirmwareVersion;
                    if (!string.IsNullOrEmpty(cam.SerialNumber)) existing.SerialNumber = cam.SerialNumber;
                    if (!string.IsNullOrEmpty(cam.HardwareId)) existing.HardwareId = cam.HardwareId;
                    if (!string.IsNullOrEmpty(cam.Name)) existing.Name = cam.Name;
                    existing.IsDiscovered = true;
                    updated++;
                }
                else
                {
                    Cameras.Add(cam);
                    added++;
                }
            }

            if (updated > 0) ScheduleSave();

            StatusText = found.Count == 0
                ? $"ONVIF-камер не найдено ({targetText})"
                : $"Найдено {found.Count}, новых: {added}, обновлено: {updated} ({targetText})";
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
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void OpenAddManual() => AddManualRequested?.Invoke();

    public async Task<bool> TryAddManualAsync(CancellationToken ct = default)
    {
        LastProbeError = "";
        if (string.IsNullOrWhiteSpace(ManualIp))
        {
            LastProbeError = "Укажите IP-адрес";
            return false;
        }

        var ip = ManualIp.Trim();
        var port = int.TryParse(ManualPort, out var p) ? p : 80;

        var dup = Cameras.FirstOrDefault(c => c.IpAddress == ip && c.Port == port);
        if (dup != null)
        {
            LastProbeError = $"Камера {ip}:{port} уже в списке";
            SelectedCamera = dup;
            return false;
        }

        IsProbing = true;
        StatusText = $"Проверка {ip}:{port}…";

        try
        {
            var camera = await _discoveryService.ProbeUnicastAsync(ip, port, ManualUsername, ManualPassword, ct);

            if (!camera.IsConnected)
            {
                LastProbeError = camera.StatusMessage;
                StatusText = $"Не удалось добавить {ip}:{port} — {camera.StatusMessage}";
                return false;
            }

            if (string.IsNullOrEmpty(camera.Name))
            {
                camera.Name = !string.IsNullOrEmpty(camera.Manufacturer)
                    ? $"{camera.Manufacturer} {camera.Model}"
                    : $"Manual: {ip}:{port}";
            }

            Cameras.Add(camera);
            SelectedCamera = camera;
            StatusText = $"Добавлена камера {ip}:{port}";
            return true;
        }
        catch (OperationCanceledException)
        {
            LastProbeError = "Отменено";
            StatusText = "Добавление отменено";
            return false;
        }
        catch (Exception ex)
        {
            LastProbeError = ex.Message;
            StatusText = $"Ошибка: {ex.Message}";
            return false;
        }
        finally
        {
            IsProbing = false;
        }
    }

    [RelayCommand]
    private Task AddManualAsync() => TryAddManualAsync();

    [RelayCommand]
    private void ViewDeviceInfo()
    {
        if (SelectedCamera != null) DeviceInfoRequested?.Invoke();
    }

    [RelayCommand]
    private void ViewVideoConfig()
    {
        if (SelectedCamera != null) VideoConfigRequested?.Invoke();
    }

    [RelayCommand]
    private void ViewNetworkConfig()
    {
        if (SelectedCamera != null) NetworkConfigRequested?.Invoke();
    }

    [RelayCommand]
    private void RemoveCamera()
    {
        if (SelectedCamera == null || !Cameras.Contains(SelectedCamera)) return;
        var toRemove = SelectedCamera;
        Cameras.Remove(toRemove);
        SelectedCamera = Cameras.FirstOrDefault();
    }

    [RelayCommand]
    private Task ClearAllAsync()
    {
        if (Cameras.Count == 0) return Task.CompletedTask;
        SelectedCamera = null;
        Cameras.Clear();
        StatusText = "Список камер очищен";
        return Task.CompletedTask;
    }

    partial void OnSelectedCameraChanged(CameraDevice? value)
    {
        if (value != null) CameraSelected?.Invoke();
    }

    public void StartBackgroundProbeOfSaved()
    {
        if (_disposed) return;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();
        var ct = _initCts.Token;

        var targets = Cameras
            .Where(c => !c.IsConnected
                        && !string.IsNullOrWhiteSpace(c.IpAddress)
                        && !string.IsNullOrWhiteSpace(c.Username))
            .ToArray();
        if (targets.Length == 0) return;

        _ = Task.Run(() => ProbeAllAsync(targets, ct), ct);
    }

    private async Task ProbeAllAsync(CameraDevice[] targets, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(ProbeMaxParallel);

        var tasks = targets.Select(async cam =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { await ProbeOneAsync(cam, ct).ConfigureAwait(false); }
            finally { gate.Release(); }
        });

        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private async Task ProbeOneAsync(CameraDevice cam, CancellationToken ct)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(ProbeTimeoutMs);

        try
        {
            var client = _provider.Get(cam);
            var deviceService = new DeviceService(client);
            await deviceService.GetDeviceInformationAsync(probeCts.Token).ConfigureAwait(false);

            var adapter = _vendors.For(cam);
            var friendly = await adapter.GetFriendlyNameAsync(client, probeCts.Token).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(friendly))
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (IsAutoName(cam.Name, cam.Manufacturer, cam.Model))
                        cam.Name = friendly!.Trim();
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (OperationCanceledException)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                cam.IsConnected = false;
                cam.StatusMessage = "Offline (timeout)";
            });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                cam.IsConnected = false;
                cam.StatusMessage = $"Offline: {msg}";
            });
        }
    }

    private static bool IsAutoName(string? current, string? manufacturer, string? model)
    {
        if (string.IsNullOrWhiteSpace(current)) return true;
        var trimmed = current.Trim();
        var autoMfgModel = $"{manufacturer?.Trim()} {model?.Trim()}".Trim();
        if (!string.IsNullOrEmpty(autoMfgModel) &&
            string.Equals(trimmed, autoMfgModel, StringComparison.OrdinalIgnoreCase))
            return true;
        if (trimmed.StartsWith("Manual:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;
        _saveDebounceCts?.Cancel();
        _saveDebounceCts?.Dispose();
        _saveDebounceCts = null;
        _saveLock.Dispose();
    }
}
