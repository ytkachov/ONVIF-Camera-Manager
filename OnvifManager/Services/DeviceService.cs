using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class DeviceService
{
    private readonly OnvifClient _client;

    public DeviceService(OnvifClient client) => _client = client;

    public async Task GetDeviceInformationAsync()
    {
        var bodyXml = "<tds:GetDeviceInformation/>";
        var xml = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var info = body.Element(OnvifXml.Ttds + "GetDeviceInformationResponse");
        if (info == null) info = body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetDeviceInformationResponse");
        if (info == null) return;

        _client.Camera.Manufacturer = info.Element(OnvifXml.Ttds + "Manufacturer")?.Value
            ?? info.Element(OnvifXml.Tt + "Manufacturer")?.Value ?? "";
        _client.Camera.Model = info.Element(OnvifXml.Ttds + "Model")?.Value
            ?? info.Element(OnvifXml.Tt + "Model")?.Value ?? "";
        _client.Camera.FirmwareVersion = info.Element(OnvifXml.Ttds + "FirmwareVersion")?.Value
            ?? info.Element(OnvifXml.Tt + "FirmwareVersion")?.Value ?? "";
        _client.Camera.SerialNumber = info.Element(OnvifXml.Ttds + "SerialNumber")?.Value
            ?? info.Element(OnvifXml.Tt + "SerialNumber")?.Value ?? "";
        _client.Camera.HardwareId = info.Element(OnvifXml.Ttds + "HardwareId")?.Value
            ?? info.Element(OnvifXml.Tt + "HardwareId")?.Value ?? "";

        _client.Camera.IsConnected = true;
        _client.Camera.StatusMessage = "Connected";
    }

    public async Task<List<OnvifServiceUri>> GetServicesAsync()
    {
        var services = new List<OnvifServiceUri>();
        var bodyXml = "<tds:GetServices><tds:IncludeCapability>true</tds:IncludeCapability></tds:GetServices>";

        try
        {
            var xml = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
                "http://www.onvif.org/ver10/device/wsdl/GetServices", bodyXml);

            var body = SoapMessageParser.ParseBody(xml);
            var response = body.Element(OnvifXml.Ttds + "GetServicesResponse")
                ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetServicesResponse");
            if (response == null) return services;

            foreach (var svcEl in response.Elements())
            {
                if (svcEl.Name.LocalName != "Service") continue;

                var ns = svcEl.Element(OnvifXml.Ttds + "Namespace")?.Value
                    ?? svcEl.Element(OnvifXml.Tt + "Namespace")?.Value ?? "";
                var uri = svcEl.Element(OnvifXml.Ttds + "XAddr")?.Value
                    ?? svcEl.Element(OnvifXml.Tt + "XAddr")?.Value ?? "";

                services.Add(new OnvifServiceUri
                {
                    Namespace = ns,
                    Uri = uri,
                    Version = ""
                });
            }
        }
        catch
        {
            // GetServices is optional on some cameras
        }

        _client.Camera.Services = services;
        return services;
    }

    public async Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();
        var bodyXml = "<tds:GetNetworkInterfaces/>";

        var xml = await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/GetNetworkInterfaces", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Ttds + "GetNetworkInterfacesResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetNetworkInterfacesResponse");
        if (response == null) return interfaces;

        foreach (var ifaceEl in response.Elements())
        {
            if (ifaceEl.Name.LocalName != "NetworkInterfaces") continue;

            var ni = new NetworkInterfaceInfo
            {
                Token = ifaceEl.Attribute("token")?.Value ?? "",
                Enabled = ParseBool(ifaceEl.Element(OnvifXml.Tt + "Enabled")?.Value, true),
                HwAddress = ifaceEl.Element(OnvifXml.Tt + "HwAddress")?.Value ?? "",
                Mtu = ParseInt(ifaceEl.Element(OnvifXml.Tt + "MTU")?.Value, 1500)
            };

            var ipv4 = ifaceEl.Element(OnvifXml.Tt + "IPv4");
            if (ipv4 != null)
            {
                ni.IPv4Enabled = ParseBool(ipv4.Element(OnvifXml.Tt + "Enabled")?.Value, true);
                ni.IPv4Dhcp = ParseBool(ipv4.Element(OnvifXml.Tt + "DHCP")?.Value, true);

                var manual = ipv4.Element(OnvifXml.Tt + "Manual");
                if (manual != null)
                {
                    ni.IPv4Address = manual.Element(OnvifXml.Tt + "Address")?.Value ?? "";
                    ni.IPv4PrefixLength = ParseInt(manual.Element(OnvifXml.Tt + "PrefixLength")?.Value, 24);
                }

                var fromDhcp = ipv4.Element(OnvifXml.Tt + "FromDHCP");
                if (fromDhcp != null && string.IsNullOrEmpty(ni.IPv4Address))
                {
                    ni.IPv4Address = fromDhcp.Element(OnvifXml.Tt + "Address")?.Value ?? "";
                    ni.IPv4PrefixLength = ParseInt(fromDhcp.Element(OnvifXml.Tt + "PrefixLength")?.Value, 24);
                }
            }

            var dns = ifaceEl.Element(OnvifXml.Tt + "DNS");
            if (dns != null)
            {
                foreach (var dnsEntry in dns.Elements(OnvifXml.Tt + "DNSManual"))
                {
                    foreach (var addr in dnsEntry.Elements(OnvifXml.Tt + "IPv4Address"))
                        if (!string.IsNullOrEmpty(addr.Value)) ni.DnsServers.Add(addr.Value);
                }
            }

            interfaces.Add(ni);
        }

        return interfaces;
    }

    public async Task SetNetworkInterfacesAsync(NetworkInterfaceInfo ni)
    {
        var dhcp = ni.IPv4Dhcp ? "true" : "false";
        var bodyXml = $@"
<tds:SetNetworkInterfaces>
  <tds:NetworkInterface token=""{ni.Token}"">
    <tt:Enabled>{BoolStr(ni.Enabled)}</tt:Enabled>
    <tt:IPv4>
      <tt:Enabled>{BoolStr(ni.IPv4Enabled)}</tt:Enabled>
      <tt:Manual>
        <tt:Address>{ni.IPv4Address}</tt:Address>
        <tt:PrefixLength>{ni.IPv4PrefixLength}</tt:PrefixLength>
      </tt:Manual>
      <tt:DHCP>{dhcp}</tt:DHCP>
    </tt:IPv4>
    <tt:MTU>{ni.Mtu}</tt:MTU>
  </tds:NetworkInterface>
</tds:SetNetworkInterfaces>";

        await _client.SendSoapAsync(OnvifXml.DeviceServicePath,
            "http://www.onvif.org/ver10/device/wsdl/SetNetworkInterfaces", bodyXml);
    }

    private static bool ParseBool(string? val, bool def) =>
        bool.TryParse(val, out var r) ? r : def;

    private static int ParseInt(string? val, int def) =>
        int.TryParse(val, out var r) ? r : def;

    private static string BoolStr(bool v) => v ? "true" : "false";
}
