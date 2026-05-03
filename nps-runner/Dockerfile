# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NpsRunner.csproj ./
COPY nuget.config     ./
RUN dotnet restore NpsRunner.csproj --configfile nuget.config

COPY Program.cs       ./
RUN dotnet publish NpsRunner.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN groupadd --gid 1000 npsrunner \
 && useradd --uid 1000 --gid 1000 --no-create-home --shell /usr/sbin/nologin npsrunner

COPY --from=build /app/publish .
RUN chown -R npsrunner:npsrunner /app

USER npsrunner

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["/app/nps-runner"]
