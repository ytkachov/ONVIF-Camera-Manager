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
}

public sealed record StartDiscoveryResponse(string SessionId, int TimeoutSeconds);
