namespace OnvifManager.Web.Contracts;

// Wire shape for a single ProbeMatch surfaced over SignalR during a discovery
// session. Does NOT include manufacturer/model/firmware — those require a
// separate GetDeviceInformation call (M3) and are not part of the multicast
// payload.
public sealed record DiscoveredDeviceDto(
    string Id,
    string Endpoint,
    string Ip,
    int Port,
    string? Name,
    string? Hardware,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Types);
