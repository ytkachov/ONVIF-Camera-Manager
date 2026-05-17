using System.IO;
using System.Net.Http;
using OnvifManager.Models;

namespace OnvifManager.Services;

public sealed class SnapshotResult
{
    public string Path { get; init; } = string.Empty;
    public int Bytes { get; init; }
    public string ContentType { get; init; } = string.Empty;
}

public class SnapshotService
{
    private readonly OnvifClientProvider _provider;

    public SnapshotService(OnvifClientProvider provider) => _provider = provider;

    public async Task<SnapshotResult> CaptureAsync(CameraDevice camera, string profileToken,
        string targetDirectory, CancellationToken ct = default)
    {
        var client = _provider.Get(camera);
        var media = new MediaService(client);
        var uri = await media.GetSnapshotUriAsync(profileToken, ct);
        if (string.IsNullOrWhiteSpace(uri))
            throw new InvalidOperationException("Camera did not return a snapshot URI");

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        SoapLog.WriteRequest(uri, "GET (snapshot)", string.Empty);

        using var resp = await client.SendRawAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            SoapLog.WriteResponse(uri, resp, errBody);
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} at {uri}");
        }

        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        SoapLog.WriteNote(
            $"snapshot ok: HTTP {(int)resp.StatusCode} content-type={contentType} content-length={resp.Content.Headers.ContentLength}");
        var ext = contentType.EndsWith("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";

        Directory.CreateDirectory(targetDirectory);
        var safeName = MakeFileSafe(string.IsNullOrEmpty(camera.Name) ? camera.IpAddress : camera.Name);
        var path = Path.Combine(targetDirectory,
            $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.{ext}");

        await using (var stream = await resp.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(path))
            await stream.CopyToAsync(file, ct);

        return new SnapshotResult
        {
            Path = path,
            Bytes = (int)(new FileInfo(path).Length),
            ContentType = contentType
        };
    }

    private static string MakeFileSafe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim().Length == 0 ? "snapshot" : name.Trim();
    }
}
