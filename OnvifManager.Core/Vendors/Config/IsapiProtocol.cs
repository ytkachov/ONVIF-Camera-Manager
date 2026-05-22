using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Services;

namespace OnvifManager.Vendors.Config;

public sealed class IsapiProtocol : IVendorProtocol
{
    public string Name => "isapi";

    public async Task<XDocument?> ReadAsync(OnvifClient client, string path, CancellationToken ct = default)
    {
        var uri = BuildUri(client, path);
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        SoapLog.WriteRequest(uri, "GET (ISAPI vendor)", string.Empty);

        using var resp = await client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        if (!resp.IsSuccessStatusCode) return null;
        try { return XDocument.Parse(body); }
        catch { return null; }
    }

    public async Task WriteAsync(OnvifClient client, string path, XDocument doc, CancellationToken ct = default)
    {
        var uri = BuildUri(client, path);
        var xml = doc.ToString(SaveOptions.DisableFormatting);

        using var req = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(xml, Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };

        SoapLog.WriteRequest(uri, "PUT (ISAPI vendor)", xml);

        using var resp = await client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        resp.EnsureSuccessStatusCode();
    }

    private static string BuildUri(OnvifClient client, string path) =>
        $"{client.Camera.Endpoint}:{client.Camera.Port}{path}";
}
