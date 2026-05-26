using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;
using OnvifManager.Vendors;

namespace OnvifManager.Services;

public class DiscoveryService
{
    private const int DiscoveryTimeoutMs = 5000;
    private readonly OnvifClientProvider _provider;
    private readonly VendorRegistry _vendors;

    public DiscoveryService(OnvifClientProvider provider, VendorRegistry? vendors = null)
    {
        _provider = provider;
        _vendors = vendors ?? VendorRegistry.Empty;
    }

    public Task<List<CameraDevice>> DiscoverAsync(string? localIp = null,
        CancellationToken ct = default)
        => DiscoverAsync(localIp, progress: null, timeoutMs: DiscoveryTimeoutMs, ct);

    // Streaming-friendly overload: each ProbeMatch is reported via `progress`
    // synchronously while the socket loop runs, so callers (web SignalR hub,
    // CLI long-listen modes) can react before the whole timeout elapses.
    public async Task<List<CameraDevice>> DiscoverAsync(
        string? localIp,
        IProgress<CameraDevice>? progress,
        int timeoutMs = DiscoveryTimeoutMs,
        CancellationToken ct = default)
    {
        var cameras = new List<CameraDevice>();
        var bindAddress = string.IsNullOrEmpty(localIp) ? IPAddress.Any : IPAddress.Parse(localIp);

        using var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.ReceiveTimeout = timeoutMs;
        udp.Client.Bind(new IPEndPoint(bindAddress, OnvifXml.DiscoveryPort));

        if (!string.IsNullOrEmpty(localIp))
        {
            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress), IPAddress.Parse(localIp)));
        }
        else
        {
            try { udp.JoinMulticastGroup(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress)); }
            catch
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress)));
            }
        }

        var messageId = Guid.NewGuid().ToString("N");
        var probeBytes = Encoding.UTF8.GetBytes(OnvifXml.GetProbeMessage(messageId));
        var multicastEp = new IPEndPoint(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress), OnvifXml.DiscoveryPort);
        await udp.SendAsync(probeBytes, probeBytes.Length, multicastEp);

        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var result = await udp.ReceiveAsync(ct);
                foreach (var match in ParseProbeMatches(result.Buffer))
                {
                    match.IpAddress = result.RemoteEndPoint.Address.ToString();
                    match.IsDiscovered = true;
                    cameras.Add(match);
                    progress?.Report(match);
                }
            }
            catch (SocketException) { break; }
            catch (OperationCanceledException) { break; }
        }

        return cameras;
    }

    public async Task<CameraDevice> ProbeUnicastAsync(string ipAddress, int port, string username, string password,
        CancellationToken ct = default)
    {
        var camera = new CameraDevice
        {
            Endpoint = $"http://{ipAddress}",
            Port = port,
            IpAddress = ipAddress,
            Username = username,
            Password = password,
            IsDiscovered = false,
            IsManual = true
        };

        try
        {
            var client = _provider.Get(camera);
            var deviceService = new DeviceService(client);
            await deviceService.GetDeviceInformationAsync(ct);

            var adapter = _vendors.For(camera);
            var vendorName = await adapter.GetFriendlyNameAsync(client, ct);
            if (!string.IsNullOrWhiteSpace(vendorName))
                camera.Name = vendorName;

            if (string.IsNullOrEmpty(camera.Name))
            {
                try
                {
                    var deviceName = await deviceService.GetDeviceNameAsync(ct);
                    if (!string.IsNullOrWhiteSpace(deviceName))
                        camera.Name = deviceName.Trim();
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            camera.IsConnected = true;
            camera.StatusMessage = "Connected";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var inner = ex.InnerException;
            camera.StatusMessage = inner != null
                ? $"Probe failed: {ex.Message} → {inner.Message}"
                : $"Probe failed: {ex.Message}";
        }

        return camera;
    }

    private static List<CameraDevice> ParseProbeMatches(byte[] bytes)
    {
        var cameras = new List<CameraDevice>();
        XDocument doc;
        try
        {
            using var ms = new MemoryStream(bytes);
            doc = SoapMessageParser.LoadDocument(ms);
        }
        catch { return cameras; }

        var body = doc.Root?.Element(OnvifXml.S + "Body");
        if (body == null) return cameras;

        var probeMatches = body.Element(OnvifXml.WsdNs + "ProbeMatches");
        if (probeMatches == null) return cameras;

        foreach (var match in probeMatches.Elements(OnvifXml.WsdNs + "ProbeMatch"))
        {
            var camera = new CameraDevice();

            var xAddrs = match.Element(OnvifXml.WsdNs + "XAddrs")?.Value;
            if (xAddrs != null)
            {
                foreach (var uri in xAddrs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                    {
                        camera.Endpoint = $"{parsedUri.Scheme}://{parsedUri.Host}";
                        camera.Port = parsedUri.Port;
                    }
                }
            }

            var scopes = match.Element(OnvifXml.WsdNs + "Scopes")?.Value;
            if (scopes != null)
            {
                foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (scope.Contains("name/"))
                        camera.Name = Uri.UnescapeDataString(scope[(scope.IndexOf("name/") + 5)..]);
                    else if (scope.Contains("hardware/"))
                        camera.HardwareId = Uri.UnescapeDataString(scope[(scope.IndexOf("hardware/") + 9)..]);
                }
            }

            if (!string.IsNullOrEmpty(camera.Endpoint))
                cameras.Add(camera);
        }

        return cameras;
    }
}
