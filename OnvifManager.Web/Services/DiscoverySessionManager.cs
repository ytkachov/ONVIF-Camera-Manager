using System.Collections.Concurrent;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Web.Contracts;

namespace OnvifManager.Web.Services;

// Tracks in-flight discovery sessions so cancel/cleanup is decoupled from the
// SignalR/controller code. Single-user deployment so a small ConcurrentDictionary
// is enough; if multi-user is ever needed the eviction policy would need rethinking.
public sealed class DiscoverySessionManager
{
    private readonly ConcurrentDictionary<string, DiscoverySession> _sessions = new();
    private readonly DiscoveryService _discoveryService;
    private readonly ILogger<DiscoverySessionManager> _logger;

    public DiscoverySessionManager(
        DiscoveryService discoveryService,
        ILogger<DiscoverySessionManager> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    public string Start(
        int timeoutSeconds,
        string? localIp,
        Func<DiscoveredDeviceDto, Task> onDevice,
        Func<DiscoverySessionResult, Task> onCompleted,
        string? requestedSessionId = null)
    {
        var sessionId = string.IsNullOrEmpty(requestedSessionId)
            ? Guid.NewGuid().ToString("N")
            : requestedSessionId;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));
        var session = new DiscoverySession(sessionId, DateTime.UtcNow, cts);
        if (!_sessions.TryAdd(sessionId, session))
        {
            cts.Dispose();
            throw new InvalidOperationException($"Discovery session '{sessionId}' is already running.");
        }

        // Fire-and-forget worker; the awaiter (controller) returned 202 already.
        // Exceptions are logged here because no caller is around to observe them.
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cancelled = false;
            try
            {
                var progress = new Progress<CameraDevice>(cam =>
                {
                    session.IncrementFound();
                    var dto = ToDto(cam);
                    _ = onDevice(dto);
                });

                await _discoveryService
                    .DiscoverAsync(localIp, progress, timeoutSeconds * 1000, cts.Token)
                    .ConfigureAwait(false);

                cancelled = session.CancelRequested;
            }
            catch (OperationCanceledException)
            {
                cancelled = session.CancelRequested;
                _logger.LogInformation("Discovery session {SessionId} cancelled (user={User})", sessionId, cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery session {SessionId} failed", sessionId);
            }
            finally
            {
                sw.Stop();
                _sessions.TryRemove(sessionId, out _);
                cts.Dispose();

                var result = new DiscoverySessionResult(
                    Found: session.FoundCount,
                    DurationSeconds: sw.Elapsed.TotalSeconds,
                    Cancelled: cancelled);

                try
                {
                    await onCompleted(result).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Discovery completion callback failed for {SessionId}", sessionId);
                }
            }
        });

        return sessionId;
    }

    public bool Cancel(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session)) return false;
        session.MarkCancelRequested();
        try { session.Cts.Cancel(); }
        catch (ObjectDisposedException) { }
        return true;
    }

    private static DiscoveredDeviceDto ToDto(CameraDevice cam)
    {
        // Core's ProbeMatch parser already collapses XAddrs into Endpoint/Port and
        // pulls name/hardware out of Scopes; raw scope/type arrays aren't preserved,
        // so we surface empty collections rather than fabricating them.
        var id = !string.IsNullOrEmpty(cam.HardwareId)
            ? cam.HardwareId
            : $"{cam.IpAddress}:{cam.Port}";

        return new DiscoveredDeviceDto(
            Id: id,
            Endpoint: cam.Endpoint,
            Ip: cam.IpAddress,
            Port: cam.Port,
            Name: string.IsNullOrEmpty(cam.Name) ? null : cam.Name,
            Hardware: string.IsNullOrEmpty(cam.HardwareId) ? null : cam.HardwareId,
            Scopes: Array.Empty<string>(),
            Types: Array.Empty<string>());
    }

    private sealed class DiscoverySession
    {
        private int _foundCount;

        public DiscoverySession(string id, DateTime startedUtc, CancellationTokenSource cts)
        {
            Id = id;
            StartedUtc = startedUtc;
            Cts = cts;
        }

        public string Id { get; }
        public DateTime StartedUtc { get; }
        public CancellationTokenSource Cts { get; }
        public int FoundCount => _foundCount;
        public bool CancelRequested { get; private set; }

        public void IncrementFound() => Interlocked.Increment(ref _foundCount);
        public void MarkCancelRequested() => CancelRequested = true;
    }
}

public sealed record DiscoverySessionResult(int Found, double DurationSeconds, bool Cancelled);
