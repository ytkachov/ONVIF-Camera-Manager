using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
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

public partial class DiscoveryViewModel : ObservableObject, IDisposable
{
    private readonly DiscoveryService _discoveryService;
    private readonly OnvifClientProvider _provider;
    private CancellationTokenSource? _scanCts;
    private bool _disposed;

    [ObservableProperty] private ObservableCollection<CameraDevice> _cameras = new();
    [ObservableProperty] private CameraDevice? _selectedCamera;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Click Scan to discover ONVIF cameras on the network";
    [ObservableProperty] private ObservableCollection<NetworkInterfaceItem> _networkInterfaces = new();
    [ObservableProperty] private NetworkInterfaceItem? _selectedNetworkInterface;
    [ObservableProperty] private string _manualIp = "";
    [ObservableProperty] private string _manualPort = "80";
    [ObservableProperty] private string _manualUsername = "admin";
    [ObservableProperty] private string _manualPassword = "";

    public event Action? CameraSelected;
    public event Action? DeviceInfoRequested;
    public event Action? VideoConfigRequested;
    public event Action? NetworkConfigRequested;

    public DiscoveryViewModel(DiscoveryService discoveryService, OnvifClientProvider provider)
    {
        _discoveryService = discoveryService;
        _provider = provider;
        LoadNetworkInterfaces();
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

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
        StatusText = $"Scanning network on {targetText}...";

        try
        {
            var localIp = string.IsNullOrEmpty(target) ? null : target;
            var found = await Task.Run(() => _discoveryService.DiscoverAsync(localIp, ct), ct);

            Cameras.Clear();
            foreach (var cam in found)
                Cameras.Add(cam);

            StatusText = found.Count == 0
                ? $"No ONVIF cameras found on {targetText}."
                : $"Found {found.Count} camera(s) on {targetText}. Select one to view details.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
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
            var camera = await _discoveryService.ProbeUnicastAsync(ip, port, ManualUsername, ManualPassword);

            if (camera.IsConnected && !string.IsNullOrEmpty(camera.Manufacturer))
                camera.Name = $"{camera.Manufacturer} {camera.Model} ({ip})";
            else if (string.IsNullOrEmpty(camera.Name))
                camera.Name = $"Manual: {ip}:{port}";

            Cameras.Add(camera);
            SelectedCamera = camera;
            StatusText = camera.IsConnected
                ? $"Added camera {ip}:{port}"
                : $"Added {ip}:{port} (probe: {camera.StatusMessage})";
        }
        catch (Exception ex)
        {
            StatusText = $"Add error: {ex.Message}";
        }
    }

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

    partial void OnSelectedCameraChanged(CameraDevice? value)
    {
        if (value != null) CameraSelected?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
    }
}
