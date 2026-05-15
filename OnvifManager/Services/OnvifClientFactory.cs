using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        var http = _clients.GetOrAdd(key, _ => CreateClient(camera));
        return new OnvifClient(http, camera);
    }

    private HttpClient CreateClient(CameraDevice camera)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            PreAuthenticate = false,
            AllowAutoRedirect = false
        };
        if (!string.IsNullOrEmpty(camera.Username))
            handler.Credentials = new NetworkCredential(camera.Username, camera.Password);
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

    public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct = default) =>
        _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

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
