using System.CommandLine;

namespace OnvifManager.Cli;

internal static class CliOptions
{
    public static readonly Option<string?> Host = new(
        name: "--host",
        description: "Camera IP, hostname, or stored camera Name (looked up in cameras.json)");

    public static readonly Option<int> Port = new(
        name: "--port",
        getDefaultValue: () => 80,
        description: "Camera ONVIF port (default 80, or from store)");

    public static readonly Option<string?> User = new(
        name: "--user",
        description: "ONVIF username (optional if --host resolves a stored camera)");

    public static readonly Option<string?> Pass = new(
        name: "--pass",
        description: "ONVIF password (falls back to env ONVIF_PASSWORD, or store)");

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

    public static readonly Option<string> StringValue = new(
        name: "--value",
        description: "New value to write")
    { IsRequired = true };

    public static readonly Option<string> ProfileToken = new(
        name: "--profile",
        description: "Media profile token")
    { IsRequired = true };

    public static readonly Option<string> VideoSourceToken = new(
        name: "--video-source",
        description: "Video source token")
    { IsRequired = true };

    public static readonly Option<string> InterfaceToken = new(
        name: "--token",
        description: "Network interface token")
    { IsRequired = true };

    public static readonly Option<string> EncoderToken = new(
        name: "--token",
        description: "Video encoder configuration token")
    { IsRequired = true };

    public static readonly Option<string> StreamProtocol = new(
        name: "--protocol",
        getDefaultValue: () => "RTSP",
        description: "Stream transport protocol (RTSP, HTTP, UDP)");

    public static readonly Option<string?> SnapshotOutDir = new(
        name: "--out",
        description: "Output directory for snapshot (default: current dir)");

    public static readonly Option<int?> EncoderWidth = new(
        name: "--width",
        description: "Video width in pixels");
    public static readonly Option<int?> EncoderHeight = new(
        name: "--height",
        description: "Video height in pixels");
    public static readonly Option<int?> EncoderBitrate = new(
        name: "--bitrate",
        description: "Bitrate limit in kbps");
    public static readonly Option<int?> EncoderFps = new(
        name: "--fps",
        description: "Frame rate limit");
    public static readonly Option<string?> EncoderGov = new(
        name: "--gov",
        description: "GOP length (H264 only)");
    public static readonly Option<string?> EncoderH264Profile = new(
        name: "--h264-profile",
        description: "H264 profile (Baseline, Main, High, Extended)");
    public static readonly Option<string?> EncoderQuality = new(
        name: "--quality",
        description: "Quality mode: CBR, VBR, or CQ");

    public static readonly Option<bool?> NetDhcp = new(
        name: "--dhcp",
        description: "Enable (true) or disable (false) DHCP");
    public static readonly Option<string?> NetIpv4 = new(
        name: "--ipv4",
        description: "Static IPv4 address");
    public static readonly Option<int?> NetPrefix = new(
        name: "--prefix",
        description: "IPv4 prefix length (e.g. 24)");
    public static readonly Option<string?> NetGateway = new(
        name: "--gateway",
        description: "IPv4 default gateway");
    public static readonly Option<int?> NetMtu = new(
        name: "--mtu",
        description: "Network MTU");

    public static readonly Option<float?> ImgBrightness = new(
        name: "--brightness",
        description: "Brightness (0..100)");
    public static readonly Option<float?> ImgContrast = new(
        name: "--contrast",
        description: "Contrast (0..100)");
    public static readonly Option<float?> ImgSaturation = new(
        name: "--saturation",
        description: "Color saturation (0..100)");
    public static readonly Option<float?> ImgSharpness = new(
        name: "--sharpness",
        description: "Sharpness (0..100)");
    public static readonly Option<bool?> ImgIrCut = new(
        name: "--ir-cut",
        description: "IR cut filter on/off");
    public static readonly Option<string?> ImgBacklight = new(
        name: "--backlight",
        description: "Backlight compensation mode (OFF, ON)");
    public static readonly Option<string?> ImgExposure = new(
        name: "--exposure",
        description: "Exposure mode (AUTO, MANUAL)");
    public static readonly Option<string?> ImgWhiteBalance = new(
        name: "--white-balance",
        description: "White balance mode (AUTO, MANUAL)");

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
