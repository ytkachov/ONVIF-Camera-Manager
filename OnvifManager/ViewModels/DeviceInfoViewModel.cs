using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Vendors;

namespace OnvifManager.ViewModels;

public partial class DeviceInfoViewModel : ConfigEditorViewModel, IDisposable
{
    private readonly DiscoveryViewModel _discovery;
    private readonly OnvifClientProvider _provider;
    private readonly VendorRegistry _vendors;
    private CancellationTokenSource? _loadCts;
    private CameraDevice? _camera;
    private bool _disposed;

    // Only the device name is editable here; everything else is read-only info.
    private static readonly IReadOnlySet<string> Tracked = new HashSet<string> { nameof(Name) };
    protected override IReadOnlySet<string> TrackedProperties => Tracked;

    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _firmwareVersion = "";
    [ObservableProperty] private string _serialNumber = "";
    [ObservableProperty] private string _hardwareId = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _endpoint = "";
    [ObservableProperty] private string _timeZone = "";
    [ObservableProperty] private string _cameraTimeUtc = "";
    [ObservableProperty] private string _cameraTimeLocal = "";
    [ObservableProperty] private string _timeSyncSource = "";
    [ObservableProperty] private ObservableCollection<OnvifServiceUri> _services = new();
    [ObservableProperty] private ObservableCollection<CameraProfile> _profiles = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public DeviceInfoViewModel(DiscoveryViewModel discovery, OnvifClientProvider provider, VendorRegistry vendors)
    {
        _discovery = discovery;
        _provider = provider;
        _vendors = vendors;
        _discovery.CameraSelected += OnCameraChanged;
    }

    private void OnCameraChanged()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        _ = LoadAsync(_loadCts.Token);
    }

    [RelayCommand]
    private Task LoadCurrentAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        return LoadAsync(_loadCts.Token);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        _camera = _discovery.SelectedCamera;
        if (_camera == null) return;

        using var _track = SuspendTracking();
        ResetChanges();
        IsLoading = true;
        StatusText = "Connecting to camera...";

        try
        {
            var client = _provider.Get(_camera);
            var deviceService = new DeviceService(client);

            await deviceService.GetDeviceInformationAsync(ct);

            Manufacturer = _camera.Manufacturer;
            Model = _camera.Model;
            FirmwareVersion = _camera.FirmwareVersion;
            SerialNumber = _camera.SerialNumber;
            HardwareId = _camera.HardwareId;
            Endpoint = _camera.Endpoint;

            string resolvedName = string.Empty;
            var adapter = _vendors.For(_camera);

            try
            {
                // Friendly name comes over ISAPI — use the vendor client so the admin
                // account (if set) is used; the ONVIF user may be denied ISAPI on NVRs.
                var vendorClient = _provider.GetVendor(_camera);
                resolvedName = (await adapter.GetFriendlyNameAsync(vendorClient, ct))?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            if (string.IsNullOrEmpty(resolvedName))
            {
                try
                {
                    resolvedName = (await deviceService.GetDeviceNameAsync(ct)).Trim();
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            if (!string.IsNullOrEmpty(resolvedName))
            {
                _camera.Name = resolvedName;
                Name = resolvedName;
            }
            else
            {
                Name = _camera.Name;
            }

            try
            {
                var t = await deviceService.GetSystemDateAndTimeAsync(ct);
                TimeZone = t.TimeZone;
                TimeSyncSource = t.SyncSource;
                CameraTimeUtc = t.Utc?.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" ?? "";
                CameraTimeLocal = t.Local?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                TimeZone = "";
                CameraTimeUtc = "";
                CameraTimeLocal = "";
            }

            await deviceService.GetServicesAsync(ct);
            Services = new ObservableCollection<OnvifServiceUri>(_camera.Services);

            var mediaService = new MediaService(client);
            try
            {
                var profiles = await mediaService.GetProfilesAsync(ct);
                Profiles = new ObservableCollection<CameraProfile>(profiles);
            }
            catch (OperationCanceledException) { throw; }
            catch { Profiles.Clear(); }

            StatusText = "Connected";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_camera == null) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        StatusText = "Сохранение имени…";

        try
        {
            var newName = (Name ?? "").Trim();
            var client = _provider.Get(_camera);
            var deviceService = new DeviceService(client);

            var adapter = _vendors.For(_camera);
            try
            {
                await adapter.SetFriendlyNameAsync(_provider.GetVendor(_camera), newName, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                StatusText = $"{adapter.Vendor}: {ex.Message}";
            }

            await deviceService.SetDeviceNameAsync(newName, ct);

            _camera.Name = newName;
            ResetChanges();
            StatusText = "Имя сохранено";

            var idx = _discovery.Cameras.IndexOf(_camera);
            if (idx >= 0)
            {
                _discovery.Cameras.RemoveAt(idx);
                _discovery.Cameras.Insert(idx, _camera);
                _discovery.SelectedCamera = _camera;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Сохранение отменено";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _discovery.SelectedCamera = null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _discovery.CameraSelected -= OnCameraChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }
}
