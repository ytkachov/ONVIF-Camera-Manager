using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class HikvisionIsapiService
{
    private const string DeviceInfoPath = "/ISAPI/System/deviceInfo";
    private readonly OnvifClient _client;

    public HikvisionIsapiService(OnvifClient client) => _client = client;

    public static bool Matches(CameraDevice camera) =>
        !string.IsNullOrEmpty(camera?.Manufacturer)
        && camera!.Manufacturer.Contains("HIKVISION", StringComparison.OrdinalIgnoreCase);

    public async Task<string> GetDeviceNameAsync(CancellationToken ct = default)
    {
        var uri = $"{_client.Camera.Endpoint}:{_client.Camera.Port}{DeviceInfoPath}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);

        SoapLog.WriteRequest(uri, "GET (ISAPI deviceInfo)", string.Empty);

        using var resp = await _client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        if (!resp.IsSuccessStatusCode) return string.Empty;

        try
        {
            var doc = XDocument.Parse(body);
            var name = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("deviceName", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return name?.Trim() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    public async Task SetDeviceNameAsync(string deviceName, CancellationToken ct = default)
    {
        var uri = $"{_client.Camera.Endpoint}:{_client.Camera.Port}{DeviceInfoPath}";
        var current = await GetRawAsync(uri, ct);
        if (current == null) return;

        XDocument doc;
        try { doc = XDocument.Parse(current); }
        catch { return; }

        var nameElem = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals("deviceName", StringComparison.OrdinalIgnoreCase));
        if (nameElem == null) return;

        if (string.Equals(nameElem.Value?.Trim(), deviceName?.Trim(), StringComparison.Ordinal))
            return;

        nameElem.Value = deviceName ?? string.Empty;

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        using var req = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(xml, Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };

        SoapLog.WriteRequest(uri, "PUT (ISAPI deviceInfo)", xml);

        using var resp = await _client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        resp.EnsureSuccessStatusCode();
    }

    private async Task<string?> GetRawAsync(string uri, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        SoapLog.WriteRequest(uri, "GET (ISAPI deviceInfo)", string.Empty);
        using var resp = await _client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);
        return resp.IsSuccessStatusCode ? body : null;
    }
}
