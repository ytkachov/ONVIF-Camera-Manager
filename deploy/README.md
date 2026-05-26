# OnvifManager.Web - Docker deployment

Two-container stack: the ASP.NET app (which also serves the React SPA from
`wwwroot/`) plus a MediaMTX sidecar for RTSP -> WebRTC bridging.

## Requirements

- Docker Engine 24+ and the `docker compose` plugin.
- **Linux host** for full functionality. WS-Discovery (UDP multicast) and
  WebRTC ICE both rely on `network_mode: host`, which only works on native
  Linux. Docker Desktop on Windows/macOS does not expose the host network
  directly.

## Run

From the repository root:

```bash
cd deploy
docker compose up --build
```

Check liveness:

```bash
curl http://localhost:8080/api/health
```

Tail logs:

```bash
docker compose logs -f onvif-web
docker compose logs -f mediamtx
```

Stop:

```bash
docker compose down
```

## Persistent state

`./deploy/data/` is bind-mounted into the container at `/data`. It holds the
camera store, ASP.NET Data Protection keys, and rolling Serilog files. **Do
not delete it** unless you intend to wipe all stored cameras and credentials.

## Network mode

Both services use `network_mode: host`. Why:

- WS-Discovery probes go out on UDP multicast 239.255.255.250:3702. Bridge
  networking would NAT the source port and most cameras drop the reply.
- MediaMTX advertises WebRTC ICE candidates with the host's IP. Without host
  networking the browser receives unreachable candidates.

The downside is that on Docker Desktop (Windows/macOS) the "host" is the
internal LinuxKit VM, not your workstation, so neither feature works. For
production deploy on a real Linux host (bare metal, VM, or a NAS that exposes
host networking).

A bridge-mode variant with explicit port mapping is possible but loses
discovery and requires manual entry of every camera; not provided in M0.

## Windows / dev workflow

Don't use Docker on Windows for day-to-day development. Run the two halves
natively:

```powershell
# Backend (port 5000)
dotnet run --project OnvifManager.Web

# Frontend (port 5173, proxies /api and /hubs to 5000)
cd OnvifManager.Web/ClientApp
npm run dev
```

The browser hits `http://localhost:5173` and Vite forwards API calls to the
.NET process.

## M0 scope note

The MediaMTX container starts with an empty `paths:` map. No streaming
actually happens yet - real RTSP-to-WebRTC wiring lands in M4. M0 just
verifies the build pipeline and that both processes come up healthy.
