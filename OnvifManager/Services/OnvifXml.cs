using System.Xml.Linq;

namespace OnvifManager.Services;

public static class OnvifXml
{
    // SOAP
    public const string SoapEnvelope = "http://www.w3.org/2003/05/soap-envelope";
    public const string SoapEncoding = "http://www.w3.org/2003/05/soap-encoding";

    // WS-Addressing
    public const string Wsa = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

    // WS-Discovery
    public const string Wsd = "http://schemas.xmlsoap.org/ws/2005/04/discovery";

    // WS-Security
    public const string Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    public const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    // ONVIF
    public const string OnvifSchema = "http://www.onvif.org/ver10/schema";
    public const string OnvifDevice = "http://www.onvif.org/ver10/device/wsdl";
    public const string OnvifMedia = "http://www.onvif.org/ver10/media/wsdl";
    public const string OnvifImaging = "http://www.onvif.org/ver10/imaging/wsdl";
    public const string OnvifNetwork = "http://www.onvif.org/ver10/network/wsdl";
    public const string OnvifDiscovery = "http://www.onvif.org/ver10/network/wsdl";

    // XNames for common namespaces
    public static readonly XNamespace S = SoapEnvelope;
    public static readonly XNamespace WsaNs = Wsa;
    public static readonly XNamespace WsdNs = Wsd;
    public static readonly XNamespace WsseNs = Wsse;
    public static readonly XNamespace WsuNs = Wsu;
    public static readonly XNamespace Ttds = OnvifDevice;
    public static readonly XNamespace Ttrt = OnvifMedia;
    public static readonly XNamespace Tt = OnvifSchema;
    public static readonly XNamespace Timg = OnvifImaging;
    public static readonly XNamespace Dn = OnvifNetwork;

    // Discovery endpoint
    public const string DiscoveryMulticastAddress = "239.255.255.250";
    public const int DiscoveryPort = 3702;

    // Common service paths
    public const string DeviceServicePath = "/onvif/device_service";
    public const string MediaServicePath = "/onvif/media_service";
    public const string ImagingServicePath = "/onvif/imaging_service";

    // WS-Discovery Probe template
    public static string GetProbeMessage(string messageId)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{SoapEnvelope}""
               xmlns:wsa=""{Wsa}""
               xmlns:wsd=""{Wsd}""
               xmlns:dn=""{OnvifDiscovery}"">
  <soap:Header>
    <wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action>
    <wsa:MessageID>urn:uuid:{messageId}</wsa:MessageID>
    <wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To>
  </soap:Header>
  <soap:Body>
    <wsd:Probe>
      <wsd:Types>dn:NetworkVideoTransmitter</wsd:Types>
    </wsd:Probe>
  </soap:Body>
</soap:Envelope>";
    }

    // SOAP envelope wrapper (SOAP 1.2)
    public static string WrapSoap(string action, string to, string bodyXml, string? securityHeader = null)
    {
        var headerXml = $@"
    <wsa:Action xmlns:wsa=""{Wsa}"">{action}</wsa:Action>
    <wsa:MessageID xmlns:wsa=""{Wsa}"">urn:uuid:{Guid.NewGuid():N}</wsa:MessageID>
    <wsa:To xmlns:wsa=""{Wsa}"">{to}</wsa:To>";

        if (securityHeader != null)
            headerXml += securityHeader;

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{SoapEnvelope}""
               xmlns:wsa=""{Wsa}""
               xmlns:wsse=""{Wsse}""
               xmlns:wsu=""{Wsu}""
               xmlns:tt=""{OnvifSchema}""
               xmlns:tds=""{OnvifDevice}""
               xmlns:trt=""{OnvifMedia}""
               xmlns:timg=""{OnvifImaging}"">
  <soap:Header>
{headerXml}
  </soap:Header>
  <soap:Body>
{bodyXml}
  </soap:Body>
</soap:Envelope>";
    }
}
