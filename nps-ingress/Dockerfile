# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NpsIngress.csproj ./
COPY nuget.config      ./
RUN dotnet restore NpsIngress.csproj --configfile nuget.config

COPY Program.cs        ./
RUN dotnet publish NpsIngress.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --gid 1000 npsing \
 && useradd --uid 1000 --gid 1000 --no-create-home --shell /usr/sbin/nologin npsing

COPY --from=build /app/publish .
RUN chown -R npsing:npsing /app

USER npsing

# Public ingress port (alpha.3 plain HTTP; TLS terminates upstream).
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://localhost:8080/health > /dev/null || exit 1

ENTRYPOINT ["/app/nps-ingress"]
