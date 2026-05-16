using System.CommandLine;

namespace OnvifManager.Cli;

internal static class CliOptions
{
    public static readonly Option<string> Host = new(
        name: "--host",
        description: "Camera IP address or hostname")
    { IsRequired = true };

    public static readonly Option<int> Port = new(
        name: "--port",
        getDefaultValue: () => 80,
        description: "Camera ONVIF port (default 80)");

    public static readonly Option<string> User = new(
        name: "--user",
        description: "ONVIF username")
    { IsRequired = true };

    public static readonly Option<string> Pass = new(
        name: "--pass",
        getDefaultValue: () => Environment.GetEnvironmentVariable("ONVIF_PASSWORD") ?? string.Empty,
        description: "ONVIF password (falls back to env ONVIF_PASSWORD)");

    public static readonly Option<int> Timeout = new(
        name: "--timeout",
        getDefaultValue: () => 5,
        description: "Request timeout in seconds (default 5)");

    public static readonly Option<bool> Json = new(
        name: "--json",
        description: "Emit machine-readable JSON instead of text");

    public static readonly Option<string?> LocalIp = new(
        name: "--local-ip",
        description: "Local interface IP to bind the multicast probe");

    public static readonly Option<string> HostnameValue = new(
        name: "--value",
        description: "New hostname value to write")
    { IsRequired = true };

    public static void AddConnectionOptions(Command cmd)
    {
        cmd.AddOption(Host);
        cmd.AddOption(Port);
        cmd.AddOption(User);
        cmd.AddOption(Pass);
        cmd.AddOption(Timeout);
    }
}

internal sealed record ConnectionOptions(string Host, int Port, string User, string Pass, TimeSpan Timeout);
