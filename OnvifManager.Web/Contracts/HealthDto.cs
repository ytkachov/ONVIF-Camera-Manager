namespace OnvifManager.Web.Contracts;

public sealed record HealthDto(
    string Status,
    string Version,
    string Runtime,
    TimeSpan Uptime);
