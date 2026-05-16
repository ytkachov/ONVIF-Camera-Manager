using System.Xml.Linq;

namespace OnvifManager.Services;

public class SoapRequest
{
    public string Xml { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}

public static class SoapMessageBuilder
{
    public static SoapRequest Build(string serviceUri, string action, XElement body,
        string? username = null, string? password = null)
    {
        var header = new XElement(OnvifXml.S + "Header",
            new XElement(OnvifXml.WsaNs + "Action", action),
            new XElement(OnvifXml.WsaNs + "MessageID", $"urn:uuid:{Guid.NewGuid():N}"),
            new XElement(OnvifXml.WsaNs + "To", serviceUri));

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            header.Add(WsSecurityHelper.BuildSecurityElement(username, password));

        var envelope = new XElement(OnvifXml.S + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", OnvifXml.SoapEnvelope),
            new XAttribute(XNamespace.Xmlns + "wsa", OnvifXml.Wsa),
            new XAttribute(XNamespace.Xmlns + "wsse", OnvifXml.Wsse),
            new XAttribute(XNamespace.Xmlns + "wsu", OnvifXml.Wsu),
            new XAttribute(XNamespace.Xmlns + "tt", OnvifXml.OnvifSchema),
            new XAttribute(XNamespace.Xmlns + "tds", OnvifXml.OnvifDevice),
            new XAttribute(XNamespace.Xmlns + "trt", OnvifXml.OnvifMedia),
            new XAttribute(XNamespace.Xmlns + "timg", OnvifXml.OnvifImaging),
            header,
            new XElement(OnvifXml.S + "Body", body));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
        return new SoapRequest
        {
            Xml = doc.ToString(SaveOptions.DisableFormatting),
            Action = action
        };
    }
}
