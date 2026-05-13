using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class DiscoveryService
{
    private const int DiscoveryTimeoutMs = 5000;

    public async Task<List<CameraDevice>> DiscoverAsync(string? localIp = null, CancellationToken ct = default)
    {
        var cameras = new List<CameraDevice>();
        var bindAddress = string.IsNullOrEmpty(localIp) ? IPAddress.Any : IPAddress.Parse(localIp);

        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.ReceiveTimeout = DiscoveryTimeoutMs;
        udpClient.Client.Bind(new IPEndPoint(bindAddress, OnvifXml.DiscoveryPort));

        if (!string.IsNullOrEmpty(localIp))
        {
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress), IPAddress.Parse(localIp)));
        }
        else
        {
            try { udpClient.JoinMulticastGroup(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress)); }
            catch
            {
                udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress)));
            }
        }

        var messageId = Guid.NewGuid().ToString("N");
        var probeXml = OnvifXml.GetProbeMessage(messageId);
        var probeBytes = Encoding.UTF8.GetBytes(probeXml);

        var multicastEp = new IPEndPoint(IPAddress.Parse(OnvifXml.DiscoveryMulticastAddress), OnvifXml.DiscoveryPort);
        await udpClient.SendAsync(probeBytes, probeBytes.Length, multicastEp);

        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < DiscoveryTimeoutMs)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var result = await udpClient.ReceiveAsync(ct);
                var responseXml = Encoding.UTF8.GetString(result.Buffer);

                var probeMatches = ParseProbeMatches(responseXml);
                foreach (var match in probeMatches)
                {
                    match.IpAddress = result.RemoteEndPoint.Address.ToString();
                    match.IsDiscovered = true;
                    cameras.Add(match);
                }
            }
            catch (SocketException) { break; }
            catch (OperationCanceledException) { break; }
        }

        return cameras;
    }

    public async Task<CameraDevice?> ProbeUnicastAsync(string ipAddress, int port, CancellationToken ct = default)
    {
        var camera = new CameraDevice
        {
            Endpoint = $"http://{ipAddress}",
            Port = port,
            IpAddress = ipAddress,
            IsDiscovered = false,
            IsManual = true
        };

        try
        {
            var client = new OnvifClient(camera);
            var deviceService = new DeviceService(client);
            await deviceService.GetDeviceInformationAsync();
            camera.IsConnected = true;
            camera.StatusMessage = "Connected";
            return camera;
        }
        catch
        {
            return camera; // Return camera even if probe fails — user can still try credentials
        }
    }

    private static List<CameraDevice> ParseProbeMatches(string xml)
    {
        var cameras = new List<CameraDevice>();
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
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
                var uris = xAddrs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var uri in uris)
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
                var scopeList = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var scope in scopeList)
                {
                    if (scope.Contains("name/"))
                        camera.Name = scope[(scope.IndexOf("name/") + 5)..];
                    else if (scope.Contains("hardware/"))
                        camera.HardwareId = scope[(scope.IndexOf("hardware/") + 9)..];
                }
            }

            if (!string.IsNullOrEmpty(camera.Endpoint))
                cameras.Add(camera);
        }

        return cameras;
    }
}
