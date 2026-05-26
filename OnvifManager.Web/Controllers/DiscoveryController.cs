using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OnvifManager.Web.Contracts;
using OnvifManager.Web.Hubs;
using OnvifManager.Web.Services;

namespace OnvifManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DiscoveryController : ControllerBase
{
    private const int DefaultTimeoutSeconds = 10;
    private const int ProgressTickMs = 1000;

    private readonly DiscoverySessionManager _sessions;
    private readonly IHubContext<DiscoveryHub> _hub;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        DiscoverySessionManager sessions,
        IHubContext<DiscoveryHub> hub,
        ILogger<DiscoveryController> logger)
    {
        _sessions = sessions;
        _hub = hub;
        _logger = logger;
    }

    [HttpPost("start")]
    public ActionResult<StartDiscoveryResponse> Start([FromBody] StartDiscoveryRequest? request)
    {
        var timeout = request?.TimeoutSeconds ?? DefaultTimeoutSeconds;
        if (timeout < 1 || timeout > 60)
            return ValidationProblem(nameof(StartDiscoveryRequest.TimeoutSeconds), "Must be between 1 and 60.");

        var localIp = string.IsNullOrWhiteSpace(request?.LocalIp) ? null : request!.LocalIp!.Trim();

        var startedUtc = DateTime.UtcNow;
        var found = 0;

        // Lightweight progress ticker so the UI can show "still scanning…" even
        // before the first DeviceFound. Cancelled by completion callback below.
        var tickerCts = new CancellationTokenSource();

        string sessionId = string.Empty;
        sessionId = _sessions.Start(
            timeoutSeconds: timeout,
            localIp: localIp,
            onDevice: async dto =>
            {
                Interlocked.Increment(ref found);
                var group = DiscoveryHub.GroupName(sessionId);
                await _hub.Clients.Group(group).SendAsync("DeviceFound", dto).ConfigureAwait(false);
                await _hub.Clients.Group(group).SendAsync(
                    "DiscoveryProgress",
                    new { found, elapsedSeconds = (DateTime.UtcNow - startedUtc).TotalSeconds })
                    .ConfigureAwait(false);
            },
            onCompleted: async result =>
            {
                tickerCts.Cancel();
                tickerCts.Dispose();
                var group = DiscoveryHub.GroupName(sessionId);
                await _hub.Clients.Group(group).SendAsync("DiscoveryCompleted", new
                {
                    found = result.Found,
                    durationSeconds = result.DurationSeconds,
                    cancelled = result.Cancelled
                }).ConfigureAwait(false);
                _logger.LogInformation(
                    "Discovery session {SessionId} completed (found {Found}, duration {Duration:F2}s, cancelled {Cancelled})",
                    sessionId, result.Found, result.DurationSeconds, result.Cancelled);
            });

        _ = Task.Run(async () =>
        {
            try
            {
                while (!tickerCts.IsCancellationRequested)
                {
                    await Task.Delay(ProgressTickMs, tickerCts.Token).ConfigureAwait(false);
                    var group = DiscoveryHub.GroupName(sessionId);
                    await _hub.Clients.Group(group).SendAsync(
                        "DiscoveryProgress",
                        new { found, elapsedSeconds = (DateTime.UtcNow - startedUtc).TotalSeconds })
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        });

        _logger.LogInformation(
            "Discovery session {SessionId} started (timeout {Timeout}s, localIp {LocalIp})",
            sessionId, timeout, localIp ?? "any");

        return StatusCode(StatusCodes.Status202Accepted, new StartDiscoveryResponse(sessionId, timeout));
    }

    [HttpPost("{sessionId}/cancel")]
    public IActionResult Cancel(string sessionId)
    {
        if (!_sessions.Cancel(sessionId))
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Discovery session not found",
                detail: $"No active session with id '{sessionId}'.");

        _logger.LogInformation("Discovery session {SessionId} cancel requested", sessionId);
        return NoContent();
    }

    private ActionResult ValidationProblem(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
