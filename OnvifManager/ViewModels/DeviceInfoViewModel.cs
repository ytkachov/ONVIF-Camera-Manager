using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public partial class DeviceInfoViewModel : ObservableObject
{
    private readonly DiscoveryViewModel _discovery;
    private CameraDevice? _camera;

    [ObservableProperty]
    private string _manufacturer = "";

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private string _firmwareVersion = "";

    [ObservableProperty]
    private string _serialNumber = "";

    [ObservableProperty]
    private string _hardwareId = "";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private ObservableCollection<OnvifServiceUri> _services = new();

    [ObservableProperty]
    private ObservableCollection<CameraProfile> _profiles = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "";

    public DeviceInfoViewModel(DiscoveryViewModel discovery)
    {
        _discovery = discovery;
        _discovery.CameraSelected += OnCameraChanged;
    }

    private void OnCameraChanged() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        _camera = _discovery.SelectedCamera;
        if (_camera == null) return;

        IsLoading = true;
        StatusText = "Connecting to camera...";

        try
        {
            var client = new OnvifClient(_camera);
            var deviceService = new DeviceService(client);

            await deviceService.GetDeviceInformationAsync();

            Manufacturer = _camera.Manufacturer;
            Model = _camera.Model;
            FirmwareVersion = _camera.FirmwareVersion;
            SerialNumber = _camera.SerialNumber;
            HardwareId = _camera.HardwareId;
            Name = _camera.Name;
            Endpoint = _camera.Endpoint;

            await deviceService.GetServicesAsync();
            Services = new ObservableCollection<OnvifServiceUri>(_camera.Services);

            var mediaService = new MediaService(client);
            try
            {
                var profiles = await mediaService.GetProfilesAsync();
                Profiles = new ObservableCollection<CameraProfile>(profiles);
            }
            catch
            {
                Profiles.Clear();
            }

            StatusText = "Connected";
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
    private void Back() => _discovery.SelectedCamera = null;
}
