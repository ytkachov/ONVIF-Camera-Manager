namespace OnvifManager.Models;

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
    public string Password { get; set; } = string.Empty;
    public List<CameraProfile> Profiles { get; set; } = new();
    public List<OnvifServiceUri> Services { get; set; } = new();
    public bool IsDiscovered { get; set; }
    public bool IsManual { get; set; }
    public bool IsConnected { get; set; }
    public string StatusMessage { get; set; } = string.Empty;

    public string DisplayLabel => string.IsNullOrEmpty(Name)
        ? $"{IpAddress}:{Port}" : $"{Name} ({IpAddress})";
}
