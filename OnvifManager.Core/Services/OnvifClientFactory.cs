using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public sealed class OnvifClientProvider : IDisposable
{
    private readonly OnvifClientOptions _options;
    private readonly ConcurrentDictionary<string, HttpClient> _clients = new();
    private bool _disposed;

    public OnvifClientProvider(OnvifClientOptions options) => _options = options;

    public OnvifClient Get(CameraDevice camera)
    {
        var key = $"{camera.IpAddress}:{camera.Port}|{camera.Username}|{camera.Password}";
        var http = _clients.GetOrAdd(key, _ => CreateClient(camera.Username, camera.Password));
        return new OnvifClient(http, camera);
    }

    // Client for vendor-specific (ISAPI) calls. When the camera carries a separate
    // web-admin account, those credentials are used (some firmware denies the ONVIF
    // user ISAPI access); otherwise this is identical to Get. ONVIF SOAP must keep
    // using Get — the admin account may lack ONVIF service access.
    public OnvifClient GetVendor(CameraDevice camera)
    {
        if (!camera.HasAdminCredentials) return Get(camera);

        var key = $"{camera.IpAddress}:{camera.Port}|{camera.AdminUsername}|{camera.AdminPassword}|vendor";
        var http = _clients.GetOrAdd(key, _ => CreateClient(camera.AdminUsername, camera.AdminPassword));
        // The OnvifClient's Camera is used for the digest credentials and to resolve the
        // vendor profile (by Manufacturer), so hand it a copy carrying the admin creds.
        return new OnvifClient(http, CloneWithCredentials(camera, camera.AdminUsername, camera.AdminPassword));
    }

    private static CameraDevice CloneWithCredentials(CameraDevice c, string user, string password) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Endpoint = c.Endpoint,
        Port = c.Port,
        IpAddress = c.IpAddress,
        Manufacturer = c.Manufacturer,
        Model = c.Model,
        FirmwareVersion = c.FirmwareVersion,
        SerialNumber = c.SerialNumber,
        HardwareId = c.HardwareId,
        Username = user,
        Password = password
    };

    private HttpClient CreateClient(string username, string password)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            PreAuthenticate = false,
            AllowAutoRedirect = false
        };
        // HttpClient's built-in Digest works for some Hikvision firmwares (the IP
        // cameras). For others (NVR firmware) it silently fails to compute a valid
        // response, in which case OnvifClient.SendRawAsync does a manual Digest
        // retry as a second attempt.
        if (!string.IsNullOrEmpty(username))
            handler.Credentials = new NetworkCredential(username, password);
        if (_options.AllowSelfSignedCertificates)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        return new HttpClient(handler) { Timeout = _options.Timeout };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var http in _clients.Values) http.Dispose();
        _clients.Clear();
    }
}

public sealed class OnvifClient
{
    private readonly HttpClient _http;

    public CameraDevice Camera { get; }

    internal OnvifClient(HttpClient http, CameraDevice camera)
    {
        _http = http;
        Camera = camera;
    }

    public async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var first = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (first.StatusCode != HttpStatusCode.Unauthorized) return first;
        if (string.IsNullOrEmpty(Camera.Username)) return first;

        // Read WWW-Authenticate via TryGetValues (raw) rather than .WwwAuthenticate.
        // HttpClient's typed parser silently drops Digest challenges containing
        // Hikvision quirks like stale="FALSE" (uppercase, quoted), which is why the
        // built-in Digest auth also fails for those firmwares.
        string? digestRaw = null;
        if (first.Headers.TryGetValues("WWW-Authenticate", out var values))
        {
            foreach (var v in values)
            {
                var trimmed = v.TrimStart();
                if (trimmed.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
                {
                    digestRaw = trimmed.Substring("Digest".Length).TrimStart();
                    break;
                }
            }
        }
        if (digestRaw is null) return first;

        var challenge = DigestChallenge.Parse(digestRaw);
        if (challenge is null) return first;

        var retry = await CloneRequestAsync(request, ct).ConfigureAwait(false);
        var authValue = challenge.BuildAuthorization(
            Camera.Username, Camera.Password, retry.Method.Method, retry.RequestUri!.PathAndQuery);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Digest", authValue);

        first.Dispose();
        return await _http.SendAsync(retry, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage src, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(src.Method, src.RequestUri) { Version = src.Version };

        if (src.Content is not null)
        {
            var bytes = await src.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var copy = new ByteArrayContent(bytes);
            foreach (var h in src.Content.Headers)
                copy.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Content = copy;
        }

        foreach (var h in src.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        return clone;
    }

    public async Task<XDocument> SendSoapAsync(string servicePath, string action, XElement body,
        CancellationToken ct = default)
    {
        var serviceUri = $"{Camera.Endpoint}:{Camera.Port}{servicePath}";
        var request = SoapMessageBuilder.Build(serviceUri, action, body, Camera.Username, Camera.Password);

        using var content = new StringContent(request.Xml, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(
            new NameValueHeaderValue("action", $"\"{action}\""));

        using var message = new HttpRequestMessage(HttpMethod.Post, serviceUri) { Content = content };
        message.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

        SoapLog.WriteRequest(serviceUri, action, request.Xml);

        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        SoapLog.WriteResponse(serviceUri, response, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            var fault = ExtractFault(responseBody);
            var detail = string.IsNullOrEmpty(fault) ? Trim(responseBody, 240) : fault;
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} at {serviceUri}" +
                (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
        }

        return SoapMessageParser.LoadDocument(new MemoryStream(Encoding.UTF8.GetBytes(responseBody)));
    }

    private static string ExtractFault(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return string.Empty;
        try
        {
            var doc = XDocument.Parse(responseBody);
            var reason = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Reason");
            var text = reason?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value;
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();

            var code = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Subcode")
                       ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value;
            return code ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

internal sealed class DigestChallenge
{
    public string Realm { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public string? Qop { get; init; }
    public string? Opaque { get; init; }
    public string Algorithm { get; init; } = "MD5";

    public static DigestChallenge? Parse(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return null;
        var values = ParseParameters(parameter);
        if (!values.TryGetValue("realm", out var realm) ||
            !values.TryGetValue("nonce", out var nonce))
            return null;
        values.TryGetValue("qop", out var qop);
        values.TryGetValue("opaque", out var opaque);
        values.TryGetValue("algorithm", out var algorithm);
        return new DigestChallenge
        {
            Realm = realm,
            Nonce = nonce,
            Qop = qop,
            Opaque = opaque,
            Algorithm = string.IsNullOrEmpty(algorithm) ? "MD5" : algorithm
        };
    }

    public string BuildAuthorization(string user, string password, string httpMethod, string uri)
    {
        var ha1 = Md5Hex($"{user}:{Realm}:{password}");
        var ha2 = Md5Hex($"{httpMethod}:{uri}");

        string response;
        var sb = new StringBuilder();
        sb.Append($"username=\"{user}\", realm=\"{Realm}\", nonce=\"{Nonce}\", uri=\"{uri}\", algorithm={Algorithm}");

        if (!string.IsNullOrEmpty(Qop))
        {
            var qop = ChooseQop(Qop!);
            var cnonce = RandomHex(8);
            var nc = "00000001";
            response = Md5Hex($"{ha1}:{Nonce}:{nc}:{cnonce}:{qop}:{ha2}");
            sb.Append($", qop={qop}, nc={nc}, cnonce=\"{cnonce}\"");
        }
        else
        {
            response = Md5Hex($"{ha1}:{Nonce}:{ha2}");
        }

        sb.Append($", response=\"{response}\"");
        if (!string.IsNullOrEmpty(Opaque))
            sb.Append($", opaque=\"{Opaque}\"");
        return sb.ToString();
    }

    private static string ChooseQop(string qop)
    {
        foreach (var part in qop.Split(','))
        {
            var p = part.Trim();
            if (string.Equals(p, "auth", StringComparison.OrdinalIgnoreCase)) return "auth";
        }
        return qop.Split(',')[0].Trim();
    }

    private static Dictionary<string, string> ParseParameters(string input)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < input.Length)
        {
            while (i < input.Length && (input[i] == ' ' || input[i] == ',')) i++;
            int keyStart = i;
            while (i < input.Length && input[i] != '=' && input[i] != ',') i++;
            if (i >= input.Length || input[i] != '=') break;
            var key = input[keyStart..i].Trim();
            i++; // skip '='
            string value;
            if (i < input.Length && input[i] == '"')
            {
                i++;
                int valStart = i;
                while (i < input.Length && input[i] != '"') i++;
                value = input[valStart..i];
                if (i < input.Length) i++; // skip closing quote
            }
            else
            {
                int valStart = i;
                while (i < input.Length && input[i] != ',') i++;
                value = input[valStart..i].Trim();
            }
            if (!string.IsNullOrEmpty(key)) dict[key] = value;
        }
        return dict;
    }

    private static string Md5Hex(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string RandomHex(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        var sb = new StringBuilder(bytes * 2);
        foreach (var b in buf) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
