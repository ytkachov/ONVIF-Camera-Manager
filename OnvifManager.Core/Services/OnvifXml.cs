using System.Xml.Linq;

namespace OnvifManager.Services;

public static class OnvifXml
{
    // SOAP
    public const string SoapEnvelope = "http://www.w3.org/2003/05/soap-envelope";

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
    public const string OnvifMedia2 = "http://www.onvif.org/ver20/media/wsdl";
    public const string OnvifImaging = "http://www.onvif.org/ver10/imaging/wsdl";
    public const string OnvifDiscovery = "http://www.onvif.org/ver10/network/wsdl";

    public static readonly XNamespace S = SoapEnvelope;
    public static readonly XNamespace WsaNs = Wsa;
    public static readonly XNamespace WsdNs = Wsd;
    public static readonly XNamespace WsseNs = Wsse;
    public static readonly XNamespace WsuNs = Wsu;
    public static readonly XNamespace Ttds = OnvifDevice;
    public static readonly XNamespace Ttrt = OnvifMedia;
    public static readonly XNamespace Ttr2 = OnvifMedia2;
    public static readonly XNamespace Tt = OnvifSchema;
    public static readonly XNamespace Timg = OnvifImaging;
    public static readonly XNamespace Dn = OnvifDiscovery;

    // Discovery endpoint
    public const string DiscoveryMulticastAddress = "239.255.255.250";
    public const int DiscoveryPort = 3702;

    // Service paths
    public const string DeviceServicePath = "/onvif/device_service";
    public const string MediaServicePath = "/onvif/media_service";
    public const string Media2ServicePath = "/onvif/Media2";
    public const string ImagingServicePath = "/onvif/imaging_service";

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
}
