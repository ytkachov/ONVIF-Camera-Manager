using Microsoft.AspNetCore.Mvc;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Web.Contracts;
using OnvifManager.Web.Services;

namespace OnvifManager.Web.Controllers;

[ApiController]
[Route("api/cameras/{id}")]
public sealed class CameraOnvifController : ControllerBase
{
    private readonly CameraStoreFacade _store;
    private readonly OnvifClientProvider _provider;
    private readonly ILogger<CameraOnvifController> _logger;

    public CameraOnvifController(
        CameraStoreFacade store,
        OnvifClientProvider provider,
        ILogger<CameraOnvifController> logger)
    {
        _store = store;
        _provider = provider;
        _logger = logger;
    }

    [HttpGet("device-info")]
    public async Task<ActionResult<DeviceInfoDto>> GetDeviceInfo(string id, CancellationToken ct)
    {
        var camera = _store.Find(id);
        if (camera is null) return NotFoundProblem(id);

        try
        {
            var client = _provider.Get(camera);
            var deviceService = new DeviceService(client);

            await deviceService.GetDeviceInformationAsync(ct);

            // Persist the refreshed identity fields so the cameras list shows fresh
            // metadata without forcing the frontend to re-fetch device-info per row.
            await _store.MutateAsync(cameras =>
            {
                var stored = cameras.FirstOrDefault(c =>
                    string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                if (stored is null) return false;
                stored.Manufacturer = client.Camera.Manufacturer;
                stored.Model = client.Camera.Model;
                stored.FirmwareVersion = client.Camera.FirmwareVersion;
                stored.SerialNumber = client.Camera.SerialNumber;
                stored.HardwareId = client.Camera.HardwareId;
                return true;
            }, ct);

            SystemDateTimeDto? sdt = null;
            try
            {
                var t = await deviceService.GetSystemDateAndTimeAsync(ct);
                sdt = new SystemDateTimeDto(t.TimeZone, t.SyncSource, t.Utc, t.Local);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GetSystemDateAndTime failed for camera {Id} ({Ip}); omitting time block",
                    id, camera.IpAddress);
            }

            _logger.LogInformation("Device info loaded for camera {Id} ({Ip})", id, camera.IpAddress);

            return Ok(new DeviceInfoDto(
                Manufacturer: client.Camera.Manufacturer,
                Model: client.Camera.Model,
                FirmwareVersion: client.Camera.FirmwareVersion,
                SerialNumber: client.Camera.SerialNumber,
                HardwareId: client.Camera.HardwareId,
                Endpoint: client.Camera.Endpoint,
                SystemDateTime: sdt));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpstreamProblem(camera, ex, "GetDeviceInformation");
        }
    }

    [HttpGet("profiles")]
    public async Task<ActionResult<IReadOnlyList<CameraProfileDto>>> GetProfiles(string id, CancellationToken ct)
    {
        var camera = _store.Find(id);
        if (camera is null) return NotFoundProblem(id);

        try
        {
            var client = _provider.Get(camera);
            var mediaService = new MediaService(client);
            var profiles = await mediaService.GetProfilesAsync(ct);

            _logger.LogInformation(
                "Loaded {Count} media profiles for camera {Id} ({Ip})",
                profiles.Count, id, camera.IpAddress);

            return Ok(profiles.Select(ToDto).ToList());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpstreamProblem(camera, ex, "GetProfiles");
        }
    }

    [HttpGet("video-encoder")]
    public async Task<ActionResult<VideoEncoderConfigsResponse>> GetVideoEncoder(string id, CancellationToken ct)
    {
        var camera = _store.Find(id);
        if (camera is null) return NotFoundProblem(id);

        var client = _provider.Get(camera);
        var mediaService = new MediaService(client);

        // Prefer Media2 (ver20) — it carries the real codec, including H265, which the
        // ver10 Encoding enum cannot express. Mirrors VideoConfigViewModel.LoadAsync.
        List<VideoEncoderConfig> configs;
        string mediaVersion;
        try
        {
            configs = await mediaService.GetVideoEncoderConfigurations2Async(ct);
            mediaVersion = "media2";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Media2 GetVideoEncoderConfigurations failed for camera {Id} ({Ip}); falling back to Media1",
                id, camera.IpAddress);
            configs = new List<VideoEncoderConfig>();
            mediaVersion = "media1";
        }

        if (configs.Count == 0)
        {
            try
            {
                configs = await mediaService.GetAllVideoEncoderConfigurationsAsync(ct);
                mediaVersion = "media1";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return UpstreamProblem(camera, ex, "GetVideoEncoderConfigurations");
            }
        }

        _logger.LogInformation(
            "Loaded {Count} video encoder configs ({Version}) for camera {Id} ({Ip})",
            configs.Count, mediaVersion, id, camera.IpAddress);

        return Ok(new VideoEncoderConfigsResponse(mediaVersion, configs.Select(ToDto).ToList()));
    }

    private static CameraProfileDto ToDto(CameraProfile p) => new(
        Token: p.Token,
        Name: p.Name,
        Fixed: p.Fixed,
        VideoSourceToken: p.VideoSourceToken,
        VideoEncoderToken: p.VideoEncoderToken);

    private static VideoEncoderConfigDto ToDto(VideoEncoderConfig c) => new(
        Token: c.Token,
        Name: c.Name,
        Encoding: c.Encoding,
        UseCount: c.UseCount,
        Width: c.Width,
        Height: c.Height,
        FrameRateLimit: c.FrameRateLimit,
        EncodingInterval: c.EncodingInterval,
        BitrateLimit: c.BitrateLimit,
        GovLength: c.GovLength,
        H264Profile: c.H264Profile,
        ConstantBitRate: c.ConstantBitRate,
        QualityLevel: c.QualityLevel);

    private ActionResult NotFoundProblem(string id) =>
        Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Camera not found",
            detail: $"No camera with id '{id}'.");

    private ActionResult UpstreamProblem(CameraDevice camera, Exception ex, string operation)
    {
        _logger.LogWarning(ex,
            "ONVIF {Operation} failed for camera {Id} ({Ip}:{Port})",
            operation, camera.Id, camera.IpAddress, camera.Port);

        return Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Upstream ONVIF call failed",
            detail: $"{operation}: {ex.Message}");
    }
}
