using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public partial class DeviceInfoViewModel : ObservableObject, IDisposable
{
    private readonly DiscoveryViewModel _discovery;
    private readonly OnvifClientProvider _provider;
    private CancellationTokenSource? _loadCts;
    private CameraDevice? _camera;
    private bool _disposed;

    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _firmwareVersion = "";
    [ObservableProperty] private string _serialNumber = "";
    [ObservableProperty] private string _hardwareId = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _endpoint = "";
    [ObservableProperty] private ObservableCollection<OnvifServiceUri> _services = new();
    [ObservableProperty] private ObservableCollection<CameraProfile> _profiles = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public DeviceInfoViewModel(DiscoveryViewModel discovery, OnvifClientProvider provider)
    {
        _discovery = discovery;
        _provider = provider;
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

            try
            {
                var deviceName = await deviceService.GetDeviceNameAsync(ct);
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    _camera.Name = deviceName.Trim();
                    Name = deviceName.Trim();
                }
                else
                {
                    Name = _camera.Name;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                Name = _camera.Name;
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
            await deviceService.SetDeviceNameAsync(newName, ct);

            _camera.Name = newName;
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
