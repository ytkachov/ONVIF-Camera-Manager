using CommunityToolkit.Mvvm.ComponentModel;
using OnvifManager.Models;

namespace OnvifManager.ViewModels;

// UI-only wrapper around a discovered CameraDevice. IsAdded/IsTesting are transient
// dialog state, kept out of the Core model and off the persisted camera record.
public partial class DiscoveredCameraRow : ObservableObject
{
    public CameraDevice Device { get; }

    public DiscoveredCameraRow(CameraDevice device) => Device = device;

    [ObservableProperty] private bool _isAdded;
    [ObservableProperty] private bool _isTesting;

    public string Key => $"{Device.IpAddress}:{Device.Port}";
}
