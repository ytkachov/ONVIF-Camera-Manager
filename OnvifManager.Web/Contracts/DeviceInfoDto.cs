namespace OnvifManager.Web.Contracts;

public sealed record DeviceInfoDto(
    string Manufacturer,
    string Model,
    string FirmwareVersion,
    string SerialNumber,
    string HardwareId,
    string Endpoint,
    SystemDateTimeDto? SystemDateTime);

public sealed record SystemDateTimeDto(
    string TimeZone,
    string SyncSource,
    DateTime? Utc,
    DateTime? Local);
