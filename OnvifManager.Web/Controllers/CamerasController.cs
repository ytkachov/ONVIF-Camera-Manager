using System.Net;
using Microsoft.AspNetCore.Mvc;
using OnvifManager.Models;
using OnvifManager.Web.Contracts;
using OnvifManager.Web.Services;

namespace OnvifManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CamerasController : ControllerBase
{
    private const int DefaultPort = 80;

    private readonly CameraStoreFacade _store;
    private readonly ILogger<CamerasController> _logger;

    public CamerasController(CameraStoreFacade store, ILogger<CamerasController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<CameraDto>> List()
    {
        var cameras = _store.List();
        _logger.LogDebug("Loaded {Count} cameras from store {Path}", cameras.Count, _store.StorePath);
        return Ok(cameras.Select(ToDto));
    }

    [HttpGet("{id}")]
    public ActionResult<CameraDto> GetOne(string id)
    {
        var camera = _store.Find(id);
        if (camera is null) return NotFoundProblem(id);
        return Ok(ToDto(camera));
    }

    [HttpPost]
    public async Task<ActionResult<CameraDto>> Create([FromBody] CreateCameraRequest request, CancellationToken ct)
    {
        if (!IsValidIp(request.Ip))
            return ValidationProblem(nameof(CreateCameraRequest.Ip), "Must be a valid IPv4 or IPv6 address.");

        var port = request.Port ?? DefaultPort;

        try
        {
            var created = await _store.MutateAsync(cameras =>
            {
                if (cameras.Any(c => MatchesEndpoint(c, request.Ip, port)))
                    throw new DuplicateEndpointException(request.Ip, port);

                var camera = new CameraDevice
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = request.Name.Trim(),
                    IpAddress = request.Ip.Trim(),
                    Port = port,
                    Username = request.Username.Trim(),
                    Password = request.Password,
                    IsManual = true,
                    IsDiscovered = false,
                    IsConnected = false
                };

                cameras.Add(camera);
                return camera;
            }, ct);

            _logger.LogInformation("Camera {Id} created for {Ip}:{Port}", created.Id, created.IpAddress, created.Port);
            return CreatedAtAction(nameof(GetOne), new { id = created.Id }, ToDto(created));
        }
        catch (DuplicateEndpointException dup)
        {
            return ConflictProblem(dup.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CameraDto>> Update(string id, [FromBody] UpdateCameraRequest request, CancellationToken ct)
    {
        if (!IsValidIp(request.Ip))
            return ValidationProblem(nameof(UpdateCameraRequest.Ip), "Must be a valid IPv4 or IPv6 address.");

        var port = request.Port ?? DefaultPort;

        try
        {
            var updated = await _store.MutateAsync(cameras =>
            {
                var existing = cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                if (existing is null) throw new CameraNotFoundException(id);

                if (cameras.Any(c => !ReferenceEquals(c, existing) && MatchesEndpoint(c, request.Ip, port)))
                    throw new DuplicateEndpointException(request.Ip, port);

                existing.Name = request.Name.Trim();
                existing.IpAddress = request.Ip.Trim();
                existing.Port = port;
                existing.Username = request.Username.Trim();
                if (!string.IsNullOrEmpty(request.Password))
                    existing.Password = request.Password;

                return existing;
            }, ct);

            _logger.LogInformation("Camera {Id} updated", updated.Id);
            return Ok(ToDto(updated));
        }
        catch (CameraNotFoundException)
        {
            return NotFoundProblem(id);
        }
        catch (DuplicateEndpointException dup)
        {
            return ConflictProblem(dup.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        try
        {
            await _store.MutateAsync(cameras =>
            {
                var removed = cameras.RemoveAll(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed == 0) throw new CameraNotFoundException(id);
                return removed;
            }, ct);
        }
        catch (CameraNotFoundException)
        {
            return NotFoundProblem(id);
        }

        _logger.LogInformation("Camera {Id} deleted", id);
        return NoContent();
    }

    private static bool IsValidIp(string value) =>
        !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value.Trim(), out _);

    private static bool MatchesEndpoint(CameraDevice camera, string ip, int port) =>
        string.Equals(camera.IpAddress, ip.Trim(), StringComparison.OrdinalIgnoreCase) && camera.Port == port;

    private static CameraDto ToDto(CameraDevice c) => new(
        Id: c.Id,
        Name: c.Name,
        Ip: c.IpAddress,
        Port: c.Port,
        Username: c.Username,
        Manufacturer: c.Manufacturer,
        Model: c.Model,
        FirmwareVersion: c.FirmwareVersion);

    private ActionResult NotFoundProblem(string id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Camera not found", detail: $"No camera with id '{id}'.");

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Duplicate camera endpoint", detail: detail);

    private ActionResult ValidationProblem(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    private sealed class CameraNotFoundException(string id) : Exception(id);

    private sealed class DuplicateEndpointException(string ip, int port)
        : Exception($"A camera with endpoint {ip}:{port} already exists.");
}
