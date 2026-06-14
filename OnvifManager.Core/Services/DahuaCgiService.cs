using System.Net;
using System.Net.Http;

namespace OnvifManager.Services;

// Dahua (and OEM brands Lechange/Imou/Amcrest) expose a flat key=value CGI API over HTTP
// Digest, distinct from Hikvision's ISAPI XML surface. Only the device "machine name" is
// wired here, mirroring HikvisionIsapiService; richer image params would need a CGI-aware
// IVendorProtocol for the config-driven engine.
public class DahuaCgiService
{
    private const string GetNamePath = "/cgi-bin/magicBox.cgi?action=getMachineName";
    private const string SetNamePath = "/cgi-bin/configManager.cgi?action=setConfig&General.MachineName=";
    private readonly OnvifClient _client;

    public DahuaCgiService(OnvifClient client) => _client = client;

    public async Task<string> GetMachineNameAsync(CancellationToken ct = default)
    {
        var uri = $"{_client.Camera.Endpoint}:{_client.Camera.Port}{GetNamePath}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);

        SoapLog.WriteRequest(uri, "GET (Dahua getMachineName)", string.Empty);

        using var resp = await _client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        if (!resp.IsSuccessStatusCode) return string.Empty;

        // Response is "name=<value>" (one key=value per line).
        return ParseValue(body, "name");
    }

    public async Task SetMachineNameAsync(string name, CancellationToken ct = default)
    {
        var uri = $"{_client.Camera.Endpoint}:{_client.Camera.Port}{SetNamePath}{WebUtility.UrlEncode(name ?? string.Empty)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);

        SoapLog.WriteRequest(uri, "GET (Dahua setConfig MachineName)", string.Empty);

        using var resp = await _client.SendRawAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        SoapLog.WriteResponse(uri, resp, body);

        resp.EnsureSuccessStatusCode();
    }

    private static string ParseValue(string body, string key)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            if (line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return line[(eq + 1)..].Trim();
        }
        return string.Empty;
    }
}
