using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnvifManager.Models;

public enum CameraStatus
{
    Online,
    Warning,
    Offline,
    // Online but the user stopped the live stream (list dot turns amber).
    Stopped
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

    // Set once the user renames the camera in the manager. Prevents the device-reported
    // name from overwriting that choice on reload/rediscovery — important for cloud cameras
    // (IMOU/Lechange) that cannot be renamed locally and would otherwise revert to the old name.
    [ObservableProperty] private bool _nameIsUserDefined;

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

    // Optional web-admin account, distinct from the ONVIF user above. On some firmware
    // (notably Hikvision NVRs) the ONVIF user is denied ISAPI access while the admin
    // account is not, so vendor-specific (ISAPI) calls use these when present; ONVIF SOAP
    // keeps using Username/Password. Entered when the user switches to Full parameter mode.
    [ObservableProperty] private string _adminUsername = string.Empty;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ObservableProperty] private string _adminPassword = string.Empty;

    public bool HasAdminCredentials => !string.IsNullOrEmpty(AdminUsername);

    // Whether this camera shows the full (ONVIF + vendor) parameter set. Per-camera and
    // persisted, so the "Режим" toggle reflects the selected camera, not a global setting.
    [ObservableProperty] private bool _fullMode;

    [ObservableProperty] private List<CameraProfile> _profiles = new();
    [ObservableProperty] private List<OnvifServiceUri> _services = new();
    [ObservableProperty] private bool _isDiscovered;
    [ObservableProperty] private bool _isManual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(Protocol))]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    [NotifyPropertyChangedFor(nameof(IndicatorState))]
    private bool _isConnected;

    // Runtime-only playback intent (not persisted): cameras default to "showing" on launch.
    // Set true when the user manually stops the stream, so switching back doesn't auto-restart
    // it and the list dot turns amber.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IndicatorState))]
    private bool _isStreamStopped;

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

    // List dot: green when online and showing, amber when online but the user stopped the
    // stream, otherwise the plain connection status (offline/auth-warning).
    public CameraStatus IndicatorState =>
        IsConnected && IsStreamStopped ? CameraStatus.Stopped : Status;

    public string MetaLine => string.IsNullOrEmpty(IpAddress)
        ? Protocol
        : $"{IpAddress} · {Protocol}";

    public override string ToString() =>
        $"{(string.IsNullOrEmpty(Name) ? "(unnamed)" : Name)} {IpAddress}:{Port}";
}
