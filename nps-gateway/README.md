English | [中文版](./README.cn.md)

# `nps-gateway` — NPS Daemon (Layer 2, Internet ingress)

> Reference implementation of the public-facing NPS Internet ingress.
> Translates NPS-over-TLS traffic from the public internet into local
> frames; handles TLS termination, rate limiting, NeuronHub-customer
> authentication, NPT debit triggering, [NPS-RFC-0004](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md)
> reputation checks, and DDoS defense. See
> [`docs/daemons/architecture.md`](../docs/architecture.md)
> for the broader six-daemon topology.

## Status — alpha.4

**Phase 1 skeleton.** Public-facing HTTP listener with a `/health`
endpoint that documents the planned milestones. Real ingress logic
(TLS termination, rate limit, auth, NPT debit, reputation lookup,
Anchor Node middleware wiring per [NPS-CR-0001](https://github.com/labacacia/NPS-Release/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md))
lands at alpha.5+.

This skeleton has been in place since alpha.3 so that the deployment surface (process
name, NuGet package id, Docker image tag) is stable from the start of
the daemon ecosystem.

## Naming note

This is the **process** called `nps-gateway`. The *spec-level* role
of "cluster control plane that routes NPS frames into NOP" has been
renamed **Anchor Node** in the NWP specification by
[NPS-CR-0001](https://github.com/labacacia/NPS-Release/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md). The
`nps-gateway` process MAY host an Anchor Node middleware via
`NPS.NWP.Anchor` — that wiring is intentionally deferred to alpha.4
so this skeleton stays minimal.

## Quick start

```bash
NPSGATEWAY_PORT=8080 dotnet run --project tools/daemons/nps-gateway/NpsGateway.csproj
curl -s http://localhost:8080/health | jq
```

### Docker

```bash
docker build -f tools/daemons/nps-gateway/Dockerfile -t labacacia/nps-gateway:1.0.0-alpha.4 .
docker run --rm -p 8080:8080 labacacia/nps-gateway:1.0.0-alpha.4
```

## Configuration (env vars)

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSGATEWAY_PORT` | `8080` | TCP port to bind. Production deployments terminate TLS on `:443`; this Phase-1 skeleton only listens HTTP. |
| `NPSGATEWAY_HOST` | `0.0.0.0` | Bind address. The default is public — `nps-gateway`, unlike `npsd`, is intentionally Internet-facing. |

## License

Apache-2.0 — see `LICENSE` at the repository root.
