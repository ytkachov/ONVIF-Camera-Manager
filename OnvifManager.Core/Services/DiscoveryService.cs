using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;
using OnvifManager.Vendors;

namespace OnvifManager.Services;

public class DiscoveryService
{
    private const int DiscoveryTimeoutMs = 5000;
    private const int ProbeRounds = 2;
    private const int ProbeRoundDelayMs = 500;
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
        var seen = new HashSet<string>();
        var multicastGroup = IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress);
        var multicastEp = new IPEndPoint(multicastGroup, OnvifXml.DiscoveryPort);
        var sendInterfaces = ResolveSendInterfaces(localIp);

        // Bind to an ephemeral port (NOT 3702) and steer each probe out a chosen interface
        // via the MulticastInterface option. ProbeMatch replies are unicast back to our
        // source endpoint, so a single socket receives answers from every interface — and
        // binding ephemeral avoids losing those replies to other 3702 listeners on the host
        // (the Windows WS-Discovery service, or a running ODM/competing tool) under SO_REUSEADDR.
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);

        foreach (var ip in sendInterfaces)
        {
            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(multicastGroup, ip));
            }
            catch { /* interface may already be joined or not support multicast */ }
        }

        var sendTask = SendProbesAsync(udp, sendInterfaces, multicastEp, ct);

        var startTime = DateTime.UtcNow;
        while (true)
        {
            var remaining = timeoutMs - (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            if (remaining <= 0 || ct.IsCancellationRequested) break;

            try
            {
                using var recvCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                recvCts.CancelAfter(remaining);
                var result = await udp.ReceiveAsync(recvCts.Token).ConfigureAwait(false);
                foreach (var match in ParseProbeMatches(result.Buffer))
                {
                    match.IpAddress = result.RemoteEndPoint.Address.ToString();
                    match.IsDiscovered = true;
                    if (!seen.Add($"{match.IpAddress}:{match.Port}")) continue;
                    cameras.Add(match);
                    progress?.Report(match);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { break; } // receive window elapsed
            catch (SocketException) { /* transient ICMP/port-unreachable; keep listening */ }
        }

        try { await sendTask.ConfigureAwait(false); } catch { }
        return cameras;
    }

    private static async Task SendProbesAsync(
        UdpClient udp, IReadOnlyList<IPAddress> interfaces, IPEndPoint multicastEp, CancellationToken ct)
    {
        for (var round = 0; round < ProbeRounds; round++)
        {
            foreach (var ip in interfaces)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                        ip.Equals(IPAddress.Any) ? new byte[] { 0, 0, 0, 0 } : ip.GetAddressBytes());

                    var probeBytes = Encoding.UTF8.GetBytes(
                        OnvifXml.GetProbeMessage(Guid.NewGuid().ToString("N")));
                    await udp.SendAsync(probeBytes, probeBytes.Length, multicastEp).ConfigureAwait(false);
                }
                catch (SocketException) { /* interface can't send multicast; skip it */ }
                catch (ObjectDisposedException) { return; }
            }

            if (round < ProbeRounds - 1)
            {
                try { await Task.Delay(ProbeRoundDelayMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // The interfaces a probe is sent from: the one the user picked, or every up,
    // multicast-capable IPv4 interface so multi-homed hosts reach all subnets.
    private static List<IPAddress> ResolveSendInterfaces(string? localIp)
    {
        if (!string.IsNullOrEmpty(localIp) && IPAddress.TryParse(localIp, out var picked))
            return new List<IPAddress> { picked };

        var ips = new List<IPAddress>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!ni.SupportsMulticast) continue;
            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    ips.Add(addr.Address);
            }
        }

        if (ips.Count == 0) ips.Add(IPAddress.Any);
        return ips;
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

            // The multicast group is shared with Windows WSD (Function Discovery), whose
            // hosts answer our probe too. Keep only true ONVIF devices: NVT type or an
            // /onvif service address. Avoids listing PCs as "cameras".
            var types = match.Element(OnvifXml.WsdNs + "Types")?.Value ?? string.Empty;
            var isOnvif = types.Contains("NetworkVideoTransmitter", StringComparison.OrdinalIgnoreCase)
                          || (xAddrs?.Contains("onvif", StringComparison.OrdinalIgnoreCase) ?? false);
            if (!isOnvif) continue;

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
