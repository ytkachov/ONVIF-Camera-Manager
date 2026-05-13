using System.Xml.Linq;

namespace OnvifManager.Services;

public class SoapRequest
{
    public string Xml { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}

public static class SoapMessageBuilder
{
    public static SoapRequest Build(string serviceUri, string action, string bodyXml,
        string? username = null, string? password = null)
    {
        string? securityHeader = null;
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            securityHeader = WsSecurityHelper.BuildSecurityHeader(username, password);

        var xml = OnvifXml.WrapSoap(action, serviceUri, bodyXml, securityHeader);
        return new SoapRequest { Xml = xml, Action = action };
    }
}
