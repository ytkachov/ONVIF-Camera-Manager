using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public sealed class OnvifClientProvider : IDisposable
{
    private readonly HttpClient _http;
    private readonly OnvifClientOptions _options;
    private bool _disposed;

    public OnvifClientProvider(OnvifClientOptions options)
    {
        _options = options;
        var handler = new HttpClientHandler { UseCookies = false };
        if (options.AllowSelfSignedCertificates)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        _http = new HttpClient(handler) { Timeout = options.Timeout };
    }

    public OnvifClient Get(CameraDevice camera) => new(_http, camera);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
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

    public async Task<XDocument> SendSoapAsync(string servicePath, string action, XElement body,
        CancellationToken ct = default)
    {
        var serviceUri = $"{Camera.Endpoint}:{Camera.Port}{servicePath}";
        var request = SoapMessageBuilder.Build(serviceUri, action, body, Camera.Username, Camera.Password);

        using var content = new StringContent(request.Xml, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(
            new NameValueHeaderValue("action", $"\"{action}\""));

        using var message = new HttpRequestMessage(HttpMethod.Post, serviceUri) { Content = content };
        if (!string.IsNullOrEmpty(Camera.Username))
        {
            var token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{Camera.Username}:{Camera.Password}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return SoapMessageParser.LoadDocument(stream);
    }
}
