# syntax=docker/dockerfile:1.6
#
# Multi-stage build for OnvifManager.Web.
# Build context is the repository root (see deploy/docker-compose.yml).

# ---------------------------------------------------------------------------
# Stage 1: build the Vite/React front-end.
# Alpine keeps the layer small; only Node is needed here.
# ---------------------------------------------------------------------------
FROM node:20-alpine AS web-build
WORKDIR /src/OnvifManager.Web/ClientApp

# Restore npm deps in their own layer so source-only edits don't bust the cache.
COPY OnvifManager.Web/ClientApp/package.json OnvifManager.Web/ClientApp/package-lock.json ./
RUN npm ci

# Vite's outDir is `../wwwroot` (see vite.config.ts), so the build writes to
# /src/OnvifManager.Web/wwwroot which Stage 2 copies into the publish output.
COPY OnvifManager.Web/ClientApp/ ./
RUN npm run build


# ---------------------------------------------------------------------------
# Stage 2: restore and publish the ASP.NET Core backend.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /src

# Copy only csproj/sln first for an isolated `restore` layer. We deliberately
# skip OnvifManager (WPF) and OnvifManager.Cli — they aren't referenced by the
# Web project and pulling them in would force net8.0-windows workloads.
COPY OnvifManager.Core/OnvifManager.Core.csproj OnvifManager.Core/
COPY OnvifManager.Web/OnvifManager.Web.csproj OnvifManager.Web/
RUN dotnet restore OnvifManager.Web/OnvifManager.Web.csproj

# Copy the actual source for the two projects we need.
COPY OnvifManager.Core/ OnvifManager.Core/
COPY OnvifManager.Web/ OnvifManager.Web/

# Drop in the pre-built React assets from Stage 1. Doing it after the source
# copy guarantees we overwrite any stale wwwroot committed to the repo.
COPY --from=web-build /src/OnvifManager.Web/wwwroot/ OnvifManager.Web/wwwroot/

RUN dotnet publish OnvifManager.Web/OnvifManager.Web.csproj \
        -c Release \
        -o /app \
        --no-restore \
        /p:UseAppHost=false


# ---------------------------------------------------------------------------
# Stage 3: minimal ASP.NET runtime image.
# Debian-slim (default) avoids native-lib surprises that bite on Alpine.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the HEALTHCHECK; the aspnet base image ships neither curl
# nor wget. --no-install-recommends keeps the image lean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=api-build /app/ ./

# Default storage paths align with the volume mount in docker-compose.yml.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Storage__DataDirectory=/data \
    Storage__KeysDirectory=/data/keys

EXPOSE 8080

# /data is a volume mount target; create it so the app can write on first run
# even if no volume is attached.
RUN mkdir -p /data/keys

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "OnvifManager.Web.dll"]
