using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class DeviceService
{
    private readonly OnvifClient _client;

    public DeviceService(OnvifClient client) => _client = client;

    public async Task GetDeviceInformationAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetDeviceInformation");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetDeviceInformationResponse");
        if (response == null) return;

        _client.Camera.Manufacturer = LocalValue(response, "Manufacturer");
        _client.Camera.Model = LocalValue(response, "Model");
        _client.Camera.FirmwareVersion = LocalValue(response, "FirmwareVersion");
        _client.Camera.SerialNumber = LocalValue(response, "SerialNumber");
        _client.Camera.HardwareId = LocalValue(response, "HardwareId");

        _client.Camera.IsConnected = true;
        _client.Camera.StatusMessage = "Connected";
    }

    private const string NameScopePrefix = "onvif://www.onvif.org/name/";

    public async Task<string> GetHostnameAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetHostname");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetHostname", body, ct);

        var info = SoapMessageParser.ParseBody(doc)
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "HostnameInformation");
        return info == null ? string.Empty : LocalValue(info, "Name");
    }

    public async Task SetHostnameAsync(string hostname, CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "SetHostname",
            new XElement(OnvifXml.Ttds + "Name", hostname ?? string.Empty));

        await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/SetHostname", body, ct);
    }

    public async Task<string> RebootAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "SystemReboot");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/SystemReboot", body, ct);

        var msg = SoapMessageParser.ParseBody(doc)
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value;
        return msg?.Trim() ?? "Rebooting";
    }

    public async Task<List<string>> GetScopesAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetScopes");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetScopes", body, ct);

        return SoapMessageParser.ParseBody(doc)
            .Descendants().Where(e => e.Name.LocalName == "ScopeItem")
            .Select(e => e.Value?.Trim() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    public async Task<string> GetDeviceNameAsync(CancellationToken ct = default)
    {
        var scopes = await GetScopesAsync(ct);
        var nameScope = scopes.FirstOrDefault(s =>
            s.StartsWith(NameScopePrefix, StringComparison.OrdinalIgnoreCase));
        return nameScope == null
            ? string.Empty
            : Uri.UnescapeDataString(nameScope[NameScopePrefix.Length..]);
    }

    public async Task SetDeviceNameAsync(string name, CancellationToken ct = default)
    {
        var trimmed = (name ?? string.Empty).Trim();

        var scopes = await GetScopesAsync(ct);
        var newScope = $"{NameScopePrefix}{Uri.EscapeDataString(trimmed)}";

        var oldScopes = scopes.Where(s =>
            s.StartsWith(NameScopePrefix, StringComparison.OrdinalIgnoreCase)).ToList();

        var alreadyHasNewScope = oldScopes.Any(s => string.Equals(s, newScope, StringComparison.Ordinal));

        foreach (var old in oldScopes.Where(s => !string.Equals(s, newScope, StringComparison.Ordinal)))
        {
            var rmBody = new XElement(OnvifXml.Ttds + "RemoveScopes",
                new XElement(OnvifXml.Ttds + "ScopeItem", old));
            try
            {
                await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
                    "http://www.onvif.org/ver10/device/wsdl/RemoveScopes", rmBody, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        if (!alreadyHasNewScope)
        {
            var addBody = new XElement(OnvifXml.Ttds + "AddScopes",
                new XElement(OnvifXml.Ttds + "ScopeItem", newScope));
            try
            {
                await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
                    "http://www.onvif.org/ver10/device/wsdl/AddScopes", addBody, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
    }

    public async Task<List<OnvifServiceUri>> GetServicesAsync(CancellationToken ct = default)
    {
        var services = new List<OnvifServiceUri>();
        var body = new XElement(OnvifXml.Ttds + "GetServices",
            new XElement(OnvifXml.Ttds + "IncludeCapability", "true"));

        try
        {
            var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
                "http://www.onvif.org/ver10/device/wsdl/GetServices", body, ct);

            var response = SoapMessageParser.ParseBody(doc)
                .Elements().FirstOrDefault(e => e.Name.LocalName == "GetServicesResponse");
            if (response == null) return services;

            foreach (var svc in response.Elements().Where(e => e.Name.LocalName == "Service"))
            {
                services.Add(new OnvifServiceUri
                {
                    Namespace = LocalValue(svc, "Namespace"),
                    Uri = LocalValue(svc, "XAddr"),
                    Version = ""
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // GetServices is optional on some cameras
        }

        _client.Camera.Services = services;
        return services;
    }

    public async Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync(CancellationToken ct = default)
    {
        var interfaces = new List<NetworkInterfaceInfo>();
        var body = new XElement(OnvifXml.Ttds + "GetNetworkInterfaces");

        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetNetworkInterfaces", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetNetworkInterfacesResponse");
        if (response == null) return interfaces;

        foreach (var iface in response.Elements().Where(e => e.Name.LocalName == "NetworkInterfaces"))
        {
            var info = iface.Elements().FirstOrDefault(e => e.Name.LocalName == "Info");
            var ni = new NetworkInterfaceInfo
            {
                Token = iface.Attribute("token")?.Value ?? "",
                Enabled = ParseBool(LocalValue(iface, "Enabled"), true),
                HwAddress = info != null ? LocalValue(info, "HwAddress") : string.Empty,
                Mtu = ParseInt(info != null ? LocalValue(info, "MTU") : null, 1500)
            };

            var ipv4 = iface.Elements().FirstOrDefault(e => e.Name.LocalName == "IPv4");
            if (ipv4 != null)
            {
                var cfg = ipv4.Elements().FirstOrDefault(e => e.Name.LocalName == "Config") ?? ipv4;
                ni.IPv4Enabled = ParseBool(LocalValue(ipv4, "Enabled"), true);
                ni.IPv4Dhcp = ParseBool(LocalValue(cfg, "DHCP"), true);

                var manual = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "Manual");
                if (manual != null)
                {
                    ni.IPv4Address = LocalValue(manual, "Address");
                    ni.IPv4PrefixLength = ParseInt(LocalValue(manual, "PrefixLength"), 24);
                }

                var fromDhcp = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "FromDHCP");
                if (fromDhcp != null && string.IsNullOrEmpty(ni.IPv4Address))
                {
                    ni.IPv4Address = LocalValue(fromDhcp, "Address");
                    ni.IPv4PrefixLength = ParseInt(LocalValue(fromDhcp, "PrefixLength"), 24);
                }
            }

            interfaces.Add(ni);
        }

        return interfaces;
    }

    public async Task<DnsInfo> GetDnsAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetDNS");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetDNS", body, ct);

        var info = SoapMessageParser.ParseBody(doc)
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "DNSInformation");
        var dns = new DnsInfo();
        if (info == null) return dns;

        dns.FromDhcp = ParseBool(LocalValue(info, "FromDHCP"), false);

        foreach (var server in info.Elements().Where(e => e.Name.LocalName == "DNSManual"))
        {
            var addr = LocalValue(server, "IPv4Address");
            if (!string.IsNullOrEmpty(addr)) dns.Manual.Add(addr);
        }

        foreach (var server in info.Elements().Where(e => e.Name.LocalName == "DNSFromDHCP"))
        {
            var addr = LocalValue(server, "IPv4Address");
            if (!string.IsNullOrEmpty(addr)) dns.FromDhcpServers.Add(addr);
        }

        return dns;
    }

    public async Task<NtpInfo> GetNtpAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetNTP");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetNTP", body, ct);

        var info = SoapMessageParser.ParseBody(doc)
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "NTPInformation");
        var ntp = new NtpInfo();
        if (info == null) return ntp;

        ntp.FromDhcp = ParseBool(LocalValue(info, "FromDHCP"), false);

        foreach (var server in info.Elements()
                     .Where(e => e.Name.LocalName == "NTPManual" || e.Name.LocalName == "NTPFromDHCP"))
        {
            var host = LocalValue(server, "DNSname");
            if (string.IsNullOrEmpty(host)) host = LocalValue(server, "IPv4Address");
            if (string.IsNullOrEmpty(host)) continue;
            if (server.Name.LocalName == "NTPManual") ntp.Manual.Add(host);
            else ntp.FromDhcpServers.Add(host);
        }

        return ntp;
    }

    public async Task<SystemDateTimeInfo> GetSystemDateAndTimeAsync(CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "GetSystemDateAndTime");
        var doc = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetSystemDateAndTime", body, ct);

        var sdt = SoapMessageParser.ParseBody(doc)
            .Descendants().FirstOrDefault(e => e.Name.LocalName == "SystemDateAndTime");
        var result = new SystemDateTimeInfo();
        if (sdt == null) return result;

        result.SyncSource = LocalValue(sdt, "DateTimeType");
        result.DaylightSavings = ParseBool(LocalValue(sdt, "DaylightSavings"), false);

        var tz = sdt.Elements().FirstOrDefault(e => e.Name.LocalName == "TimeZone");
        if (tz != null) result.TimeZone = LocalValue(tz, "TZ");

        var utc = sdt.Elements().FirstOrDefault(e => e.Name.LocalName == "UTCDateTime");
        if (utc != null) result.Utc = ParseDateTime(utc);

        var local = sdt.Elements().FirstOrDefault(e => e.Name.LocalName == "LocalDateTime");
        if (local != null) result.Local = ParseDateTime(local);

        return result;
    }

    private static DateTime? ParseDateTime(XElement el)
    {
        var date = el.Elements().FirstOrDefault(e => e.Name.LocalName == "Date");
        var time = el.Elements().FirstOrDefault(e => e.Name.LocalName == "Time");
        if (date == null || time == null) return null;
        if (!int.TryParse(LocalValue(date, "Year"), out var y)) return null;
        if (!int.TryParse(LocalValue(date, "Month"), out var mo)) return null;
        if (!int.TryParse(LocalValue(date, "Day"), out var d)) return null;
        if (!int.TryParse(LocalValue(time, "Hour"), out var h)) return null;
        if (!int.TryParse(LocalValue(time, "Minute"), out var mi)) return null;
        if (!int.TryParse(LocalValue(time, "Second"), out var s)) return null;
        try { return new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc); }
        catch { return null; }
    }

    public async Task SetNetworkInterfacesAsync(NetworkInterfaceInfo ni, CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttds + "SetNetworkInterfaces",
            new XElement(OnvifXml.Ttds + "InterfaceToken", ni.Token),
            new XElement(OnvifXml.Ttds + "NetworkInterface",
                new XElement(OnvifXml.Tt + "Enabled", BoolStr(ni.Enabled)),
                new XElement(OnvifXml.Tt + "IPv4",
                    new XElement(OnvifXml.Tt + "Enabled", BoolStr(ni.IPv4Enabled)),
                    new XElement(OnvifXml.Tt + "Manual",
                        new XElement(OnvifXml.Tt + "Address", ni.IPv4Address),
                        new XElement(OnvifXml.Tt + "PrefixLength", ni.IPv4PrefixLength)),
                    new XElement(OnvifXml.Tt + "DHCP", BoolStr(ni.IPv4Dhcp))),
                new XElement(OnvifXml.Tt + "MTU", ni.Mtu)));

        await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/SetNetworkInterfaces", body, ct);
    }

    private static string LocalValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value ?? "";

    private static bool ParseBool(string? val, bool def) =>
        bool.TryParse(val, out var r) ? r : def;

    private static int ParseInt(string? val, int def) =>
        int.TryParse(val, out var r) ? r : def;

    private static string BoolStr(bool v) => v ? "true" : "false";
}
