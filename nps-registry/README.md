English | [中文版](./README.cn.md)

# `nps-registry` — NPS Daemon (Layer 2, cross-machine NDP discovery)

> Reference implementation of the cross-machine NDP discovery registry.
> Centralised endpoint that responds to NDP `Resolve` / `Graph` queries
> and aggregates registrations from multiple machines. Per-host
> [`npsd`](../npsd/) only knows local sessions; cross-machine queries
> go here. See
> [`docs/daemons/architecture.md`](../docs/architecture.md)
> for the broader six-daemon topology.

## Status — alpha.3

**Phase 1 skeleton.** Listens on the NDP optional-dedicated port
`17436` (per [NPS-4 §1](https://github.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.md)) with a `/health`
endpoint. The `Resolve`, `Graph`, and `Announce` URL surface is
present but returns `NDP-REGISTRY-UNAVAILABLE` (HTTP 503) so consumers
can already wire against the daemon and fall back gracefully. The
SQLite-backed real registration store + cross-machine federation land
in alpha.4 → alpha.5.

## Quick start

```bash
NPSREGISTRY_PORT=17436 dotnet run --project tools/daemons/nps-registry/NpsRegistry.csproj
curl -s http://localhost:17436/health        | jq
curl -s -i http://localhost:17436/v1/resolve  # → 503 NDP-REGISTRY-UNAVAILABLE
```

### Docker

```bash
docker build -f tools/daemons/nps-registry/Dockerfile -t labacacia/nps-registry:1.0.0-alpha.3 .
docker run --rm -p 17436:17436 labacacia/nps-registry:1.0.0-alpha.3
```

## Configuration (env vars)

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSREGISTRY_PORT` | `17436` | TCP port to bind. NDP optional-dedicated per [NPS-4](https://github.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.md). |
| `NPSREGISTRY_HOST` | `0.0.0.0` | Bind address. A registry is intentionally network-facing (unlike `npsd`'s loopback default). |

## Spec references

- [NPS-4 NDP](https://github.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.md) — the discovery protocol whose registry surface this daemon implements.
- [Daemon architecture §④](../docs/architecture.md#-nps-registry--discovery-registry-l2-stage-optionally-hosted) — why the registry is its own daemon rather than part of `npsd`.

## License

Apache-2.0 — see `LICENSE` at the repository root.
