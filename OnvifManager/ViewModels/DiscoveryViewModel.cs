using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public class NetworkInterfaceItem
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string DisplayText => string.IsNullOrEmpty(IpAddress) ? Name : $"{Name} [{IpAddress}]";

    public override string ToString() => DisplayText;
}

public partial class DiscoveryViewModel : ObservableObject
{
    private readonly DiscoveryService _discoveryService;

    [ObservableProperty]
    private ObservableCollection<CameraDevice> _cameras = new();

    [ObservableProperty]
    private CameraDevice? _selectedCamera;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Click Scan to discover ONVIF cameras on the network";

    [ObservableProperty]
    private ObservableCollection<NetworkInterfaceItem> _networkInterfaces = new();

    [ObservableProperty]
    private NetworkInterfaceItem? _selectedNetworkInterface;

    [ObservableProperty]
    private string _manualIp = "";

    [ObservableProperty]
    private string _manualPort = "80";

    [ObservableProperty]
    private string _manualUsername = "admin";

    [ObservableProperty]
    private string _manualPassword = "";

    public event Action? CameraSelected;
    public event Action? DeviceInfoRequested;
    public event Action? VideoConfigRequested;
    public event Action? NetworkConfigRequested;

    public DiscoveryViewModel(DiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
        LoadNetworkInterfaces();
    }

    private void LoadNetworkInterfaces()
    {
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
        SelectedNetworkInterface = NetworkInterfaces.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        var target = SelectedNetworkInterface?.IpAddress;
        var targetText = string.IsNullOrEmpty(target) ? "all interfaces" : target;
        StatusText = $"Scanning network on {targetText}...";
        Cameras.Clear();

        try
        {
            var localIp = string.IsNullOrEmpty(target) ? null : target;
            var cameras = await Task.Run(() => _discoveryService.DiscoverAsync(localIp));

            if (cameras.Count == 0)
                StatusText = $"No ONVIF cameras found on {targetText}.";
            else
            {
                foreach (var cam in cameras)
                    Cameras.Add(cam);
                StatusText = $"Found {cameras.Count} camera(s) on {targetText}. Select one to view details.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task AddManualAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualIp)) return;

        var ip = ManualIp.Trim();
        var port = int.TryParse(ManualPort, out var p) ? p : 80;

        StatusText = $"Probing {ip}:{port}...";

        try
        {
            var camera = new CameraDevice
            {
                IpAddress = ip,
                Endpoint = $"http://{ip}",
                Port = port,
                Username = ManualUsername,
                Password = ManualPassword,
                IsManual = true,
                IsDiscovered = false,
                Name = $"Manual: {ip}:{port}"
            };

            // Try to connect and get device info
            try
            {
                var client = new OnvifClient(camera);
                var deviceService = new DeviceService(client);
                await Task.Run(() => deviceService.GetDeviceInformationAsync());
                camera.IsConnected = true;
                camera.StatusMessage = "Connected";
                camera.Name = string.IsNullOrEmpty(camera.Manufacturer)
                    ? $"{ip}:{port}" : $"{camera.Manufacturer} {camera.Model} ({ip})";
            }
            catch (Exception ex)
            {
                camera.StatusMessage = $"Probe failed: {ex.Message}";
            }

            Cameras.Add(camera);
            SelectedCamera = camera;
            StatusText = $"Added camera {ip}:{port}";
        }
        catch (Exception ex)
        {
            StatusText = $"Add error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewDeviceInfo()
    {
        if (SelectedCamera != null)
            DeviceInfoRequested?.Invoke();
    }

    [RelayCommand]
    private void ViewVideoConfig()
    {
        if (SelectedCamera != null)
            VideoConfigRequested?.Invoke();
    }

    [RelayCommand]
    private void ViewNetworkConfig()
    {
        if (SelectedCamera != null)
            NetworkConfigRequested?.Invoke();
    }

    [RelayCommand]
    private void RemoveCamera()
    {
        if (SelectedCamera != null && Cameras.Contains(SelectedCamera))
        {
            Cameras.Remove(SelectedCamera);
            SelectedCamera = Cameras.FirstOrDefault();
        }
    }

    partial void OnSelectedCameraChanged(CameraDevice? value)
    {
        if (value != null)
            CameraSelected?.Invoke();
    }
}
