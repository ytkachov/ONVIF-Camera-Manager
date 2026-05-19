using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnvifManager.Models;

public enum CameraStatus
{
    Online,
    Warning,
    Offline
}

public partial class CameraDevice : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Protocol))]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    private string _model = string.Empty;

    [ObservableProperty] private string _firmwareVersion = string.Empty;
    [ObservableProperty] private string _serialNumber = string.Empty;
    [ObservableProperty] private string _hardwareId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    private int _port = 80;

    [ObservableProperty] private string _username = "admin";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ObservableProperty] private string _password = string.Empty;

    [ObservableProperty] private List<CameraProfile> _profiles = new();
    [ObservableProperty] private List<OnvifServiceUri> _services = new();
    [ObservableProperty] private bool _isDiscovered;
    [ObservableProperty] private bool _isManual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(Protocol))]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(Protocol))]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string _statusMessage = string.Empty;

    public string DisplayLabel => string.IsNullOrEmpty(Name)
        ? $"{IpAddress}:{Port}" : $"{Name} ({IpAddress})";

    public CameraStatus Status
    {
        get
        {
            if (IsConnected) return CameraStatus.Online;
            var msg = StatusMessage ?? string.Empty;
            if (msg.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("401", StringComparison.Ordinal) ||
                msg.Contains("403", StringComparison.Ordinal) ||
                msg.Contains("creden", StringComparison.OrdinalIgnoreCase))
                return CameraStatus.Warning;
            return CameraStatus.Offline;
        }
    }

    public string Protocol => IsConnected ? "ONVIF"
        : Status == CameraStatus.Warning ? "auth required"
        : "offline";

    public string MetaLine => string.IsNullOrEmpty(IpAddress)
        ? Protocol
        : $"{IpAddress} · {Protocol}";

    public override string ToString() =>
        $"{(string.IsNullOrEmpty(Name) ? "(unnamed)" : Name)} {IpAddress}:{Port}";
}
