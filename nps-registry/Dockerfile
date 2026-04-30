# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NpsRegistry.csproj ./
COPY nuget.config       ./
RUN dotnet restore NpsRegistry.csproj --configfile nuget.config

COPY Program.cs         ./
RUN dotnet publish NpsRegistry.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --gid 1000 npsreg \
 && adduser --uid 1000 --gid 1000 --disabled-password --gecos "" npsreg

COPY --from=build /app/publish .
RUN chown -R npsreg:npsreg /app

USER npsreg

# NDP optional dedicated port (NPS-4 §2).
EXPOSE 17436

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://0.0.0.0:17436

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://localhost:17436/health > /dev/null || exit 1

ENTRYPOINT ["/app/nps-registry"]
