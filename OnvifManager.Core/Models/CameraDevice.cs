using System.Diagnostics;

namespace OnvifManager.Models;

public enum CameraStatus
{
    Online,
    Warning,
    Offline
}

public class CameraDevice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Endpoint { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 80;
    public string Username { get; set; } = "admin";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string Password { get; set; } = string.Empty;

    public List<CameraProfile> Profiles { get; set; } = new();
    public List<OnvifServiceUri> Services { get; set; } = new();
    public bool IsDiscovered { get; set; }
    public bool IsManual { get; set; }
    public bool IsConnected { get; set; }
    public string StatusMessage { get; set; } = string.Empty;

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
