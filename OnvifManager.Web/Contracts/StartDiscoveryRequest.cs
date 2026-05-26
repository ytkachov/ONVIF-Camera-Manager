using System.ComponentModel.DataAnnotations;

namespace OnvifManager.Web.Contracts;

public sealed class StartDiscoveryRequest
{
    [Range(1, 60)]
    public int? TimeoutSeconds { get; set; }

    // Optional bind-interface IP. Empty/null means "Any". WPF exposes this via
    // a network-interface dropdown; the web UI may follow once we surface
    // local NICs in an endpoint.
    public string? LocalIp { get; set; }

    // Optional client-generated session id. When supplied, the hub group is
    // joined by the client BEFORE this POST returns, eliminating the race
    // where early ProbeMatch responses broadcast into an empty group and get
    // dropped. Server falls back to generating its own id when omitted.
    [RegularExpression("^[A-Za-z0-9_-]{8,64}$", ErrorMessage = "SessionId must be 8-64 chars of [A-Za-z0-9_-].")]
    public string? SessionId { get; set; }
}

public sealed record StartDiscoveryResponse(string SessionId, int TimeoutSeconds);
