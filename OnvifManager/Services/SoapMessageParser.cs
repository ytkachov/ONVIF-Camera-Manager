using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace OnvifManager.Services;

public static class SoapMessageParser
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        CloseInput = false
    };

    public static XDocument LoadDocument(Stream stream)
    {
        using var reader = XmlReader.Create(stream, ReaderSettings);
        return XDocument.Load(reader);
    }

    public static XDocument LoadDocument(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return LoadDocument(ms);
    }

    public static XElement ParseBody(XDocument doc)
    {
        var body = doc.Root?.Element(OnvifXml.S + "Body");
        if (body == null)
            throw new InvalidOperationException("SOAP response has no Body element");

        var fault = body.Elements().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault != null)
        {
            var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value
                         ?? "Unknown fault";
            throw new OnvifFaultException(reason);
        }

        return body;
    }
}

public class OnvifFaultException : Exception
{
    public OnvifFaultException(string message) : base($"ONVIF Fault: {message}") { }
}
