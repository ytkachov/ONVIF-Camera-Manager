using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public partial class NetworkConfigViewModel : ObservableObject, IDisposable
{
    private readonly DiscoveryViewModel _discovery;
    private readonly OnvifClientProvider _provider;
    private CancellationTokenSource? _loadCts;
    private CameraDevice? _camera;
    private bool _disposed;

    [ObservableProperty] private ObservableCollection<NetworkInterfaceInfo> _interfaces = new();
    [ObservableProperty] private NetworkInterfaceInfo? _selectedInterface;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private bool _IPv4Enabled = true;
    [ObservableProperty] private bool _IPv4Dhcp = true;
    [ObservableProperty] private string _IPv4Address = "";
    [ObservableProperty] private int _IPv4PrefixLength = 24;
    [ObservableProperty] private string _hwAddress = "";
    [ObservableProperty] private int _mtu = 1500;
    [ObservableProperty] private ObservableCollection<string> _dnsServers = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public NetworkConfigViewModel(DiscoveryViewModel discovery, OnvifClientProvider provider)
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

    private async Task LoadAsync(CancellationToken ct)
    {
        _camera = _discovery.SelectedCamera;
        if (_camera == null) return;

        IsLoading = true;
        StatusText = "Loading network configuration...";

        try
        {
            var client = _provider.Get(_camera);
            var deviceService = new DeviceService(client);

            var interfaces = await deviceService.GetNetworkInterfacesAsync(ct);
            Interfaces = new ObservableCollection<NetworkInterfaceInfo>(interfaces);

            if (interfaces.Count > 0)
            {
                SelectedInterface = interfaces[0];
                LoadInterfaceIntoFields(interfaces[0]);
            }

            StatusText = "Network configuration loaded";
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

    private void LoadInterfaceIntoFields(NetworkInterfaceInfo ni)
    {
        Enabled = ni.Enabled;
        IPv4Enabled = ni.IPv4Enabled;
        IPv4Dhcp = ni.IPv4Dhcp;
        IPv4Address = ni.IPv4Address;
        IPv4PrefixLength = ni.IPv4PrefixLength;
        HwAddress = ni.HwAddress;
        Mtu = ni.Mtu;
        DnsServers = new ObservableCollection<string>(ni.DnsServers);
    }

    partial void OnSelectedInterfaceChanged(NetworkInterfaceInfo? value)
    {
        if (value != null) LoadInterfaceIntoFields(value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedInterface == null || _camera == null) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        StatusText = "Saving network configuration...";

        try
        {
            SelectedInterface.Enabled = Enabled;
            SelectedInterface.IPv4Enabled = IPv4Enabled;
            SelectedInterface.IPv4Dhcp = IPv4Dhcp;
            SelectedInterface.IPv4Address = IPv4Address;
            SelectedInterface.IPv4PrefixLength = IPv4PrefixLength;
            SelectedInterface.Mtu = Mtu;

            var client = _provider.Get(_camera);
            var deviceService = new DeviceService(client);
            await deviceService.SetNetworkInterfacesAsync(SelectedInterface, ct);

            StatusText = "Network configuration saved successfully";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Save cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
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
