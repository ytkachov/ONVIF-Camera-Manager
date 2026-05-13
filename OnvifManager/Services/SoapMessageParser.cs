using System.Xml.Linq;

namespace OnvifManager.Services;

public static class SoapMessageParser
{
    public static XElement ParseBody(string soapXml)
    {
        var doc = XDocument.Parse(soapXml);
        var body = doc.Root?.Element(OnvifXml.S + "Body");
        if (body == null)
            throw new InvalidOperationException("SOAP response has no Body element");

        if (body.Elements().FirstOrDefault() is { } fault && fault.Name.LocalName == "Fault")
        {
            var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value ?? "Unknown fault";
            throw new OnvifFaultException(reason);
        }

        return body;
    }

    public static XElement? ParseElement(string soapXml, string elementName)
    {
        var body = ParseBody(soapXml);
        return body.Elements().FirstOrDefault(e => e.Name.LocalName == elementName);
    }

    public static string? GetElementValue(string soapXml, string elementName, string? childElementName = null)
    {
        var el = ParseElement(soapXml, elementName);
        if (el == null) return null;
        if (childElementName != null)
            return el.Element(el.Name.Namespace + childElementName)?.Value ?? el.Element(childElementName)?.Value;
        return el.Value;
    }
}

public class OnvifFaultException : Exception
{
    public OnvifFaultException(string message) : base($"ONVIF Fault: {message}") { }
}
