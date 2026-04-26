# ── Build stage ───────────────────────────────────────────────────────────────
# Self-contained build: only the daemon's own files + nuget.org packages.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Npsd.csproj   ./
COPY nuget.config  ./
RUN dotnet restore Npsd.csproj --configfile nuget.config

COPY Program.cs    ./
RUN dotnet publish Npsd.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --gid 1000 npsd \
 && adduser --uid 1000 --gid 1000 --disabled-password --gecos "" npsd

# Persistent state (root keypair + future inbox storage).
RUN mkdir -p /data && chown npsd:npsd /data

COPY --from=build /app/publish .
RUN chown -R npsd:npsd /app

USER npsd

# NPS unified port (NPS-1 §2.3).
EXPOSE 17433

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV NPSD_HOST=0.0.0.0
ENV NPSD_PORT=17433
ENV NPSD_DATA_DIR=/data
ENV ASPNETCORE_URLS=http://0.0.0.0:17433

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://localhost:17433/health > /dev/null || exit 1

ENTRYPOINT ["/app/npsd"]
