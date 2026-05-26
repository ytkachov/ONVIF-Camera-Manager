namespace OnvifManager.Web.Contracts;

// Wire shape for cameras. Deliberately omits passwords and any runtime UI state
// (IsConnected/StatusMessage) carried by the WPF CameraDevice observable model.
public sealed record CameraDto(
    string Id,
    string Name,
    string Ip,
    int Port,
    string Username,
    string Manufacturer,
    string Model,
    string FirmwareVersion);
