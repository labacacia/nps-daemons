# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NpsGateway.csproj ./
COPY nuget.config      ./
RUN dotnet restore NpsGateway.csproj --configfile nuget.config

COPY Program.cs        ./
RUN dotnet publish NpsGateway.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --gid 1000 npsgw \
 && adduser --uid 1000 --gid 1000 --disabled-password --gecos "" npsgw

COPY --from=build /app/publish .
RUN chown -R npsgw:npsgw /app

USER npsgw

# Public ingress port (alpha.3 plain HTTP; TLS terminates upstream).
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://localhost:8080/health > /dev/null || exit 1

ENTRYPOINT ["/app/nps-gateway"]
