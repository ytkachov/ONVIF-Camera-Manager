using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class OnvifClient
{
    private readonly HttpClient _http;
    private readonly CameraDevice _camera;

    public OnvifClient(CameraDevice camera)
    {
        _camera = camera;
        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(camera.Username, camera.Password),
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        var authBytes = Encoding.UTF8.GetBytes($"{camera.Username}:{camera.Password}");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    public async Task<string> SendSoapAsync(string servicePath, string action, string bodyXml)
    {
        var serviceUri = $"{_camera.Endpoint}:{_camera.Port}{servicePath}";
        var request = SoapMessageBuilder.Build(serviceUri, action, bodyXml, _camera.Username, _camera.Password);

        var content = new StringContent(request.Xml, Encoding.UTF8, "application/soap+xml");
        content.Headers.Add("Content-Type", "application/soap+xml; charset=utf-8");

        var response = await _http.PostAsync(serviceUri, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public CameraDevice Camera => _camera;
}
