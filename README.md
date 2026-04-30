English | [中文版](./README.cn.md)

# NPS Daemons

[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](./LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/labacacia/nps-daemons?include_prereleases)](https://github.com/labacacia/nps-daemons/releases)
[![Architecture](https://img.shields.io/badge/architecture-3--layer-success)](./docs/architecture.md)

Reference deployment binaries for the **Neural Protocol Suite (NPS)** —
**four open-source daemons** spanning the host-local and network-entry
layers of the standard NPS deployment topology.

> Source of truth: [github.com/labacacia/nps-daemons](https://github.com/labacacia/nps-daemons) ·
> Mirror: [gitee.com/labacacia/nps-daemons](https://gitee.com/labacacia/nps-daemons) ·
> Suite: [NPS-Release](https://github.com/labacacia/NPS-Release) ·
> Architecture: [docs/architecture.md](./docs/architecture.md)

---

## What's in this repo

| Layer | Daemon | Default port | Status at `v1.0.0-alpha.4` |
|-------|--------|--------------|----------------------------|
| 1 | [`npsd`](./npsd/) | `127.0.0.1:17433` | L1 minimum: HTTP listener, root keypair generation (POSIX `0600`), `/.nwm`, `/health`. |
| 1 | [`nps-runner`](./nps-runner/) | — (worker) | Phase 1 skeleton — Generic Host scaffolding + 30 s heartbeat. Inbox watcher + spawn-spec resolver land alpha.5+. |
| 2 | [`nps-gateway`](./nps-gateway/) | `:8080` | Phase 1 skeleton — public HTTP listener + `/health`. TLS termination + rate limit + auth + NPT debit + reputation lookup land alpha.4 → alpha.5. |
| 2 | [`nps-registry`](./nps-registry/) | `:17436` | Phase 1 skeleton — NDP `Resolve` / `Graph` / `Announce` URLs return `NDP-REGISTRY-UNAVAILABLE` so consumers can wire and gracefully fall back. SQLite-backed real registry lands alpha.4. |

Each daemon lives in its own subdirectory with its own
`Dockerfile` / `docker-compose.yml` / README — they share a release
cadence and a base image but build and ship independently.

### What is NOT in this repo

The **trust-anchor / cloud** layer of NPS lives in two private repos
under the `innolotus` GitHub organisation, available with NPS Cloud
when it ships (2027 Q1+):

- `innolotus/nps-cloud-ca` — cross-organisation NID Certificate Authority + CRL/OCSP.
- `innolotus/nps-ledger` — append-only Certificate-Transparency-style reputation log per [NPS-RFC-0004](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md).

For self-host CA needs **today**, use [`labacacia/nip-ca-server`](https://github.com/labacacia/nip-ca-server)
— the OSS single-organisation CA — instead.

---

## Quick start (all four daemons together)

```bash
git clone https://github.com/labacacia/nps-daemons.git
cd nps-daemons
docker compose up -d

# Each daemon's /health is reachable on its own port
curl -s http://localhost:17433/health   # npsd
curl -s http://localhost:8080/health    # nps-gateway
curl -s http://localhost:17436/health   # nps-registry

docker compose logs -f nps-runner       # nps-runner has no HTTP surface
```

Or stand up just one:

```bash
docker compose up -d npsd
```

## Quick start (single daemon, no compose)

Each subdirectory ships a self-contained Dockerfile:

```bash
cd npsd
docker build -t labacacia/npsd:1.0.0-alpha.4 .
docker run --rm -p 17433:17433 -v npsd-data:/data labacacia/npsd:1.0.0-alpha.4
```

The .NET 10 SDK works for source builds too:

```bash
cd npsd
dotnet restore
dotnet run
```

All daemons depend on the published `LabAcacia.NPS.*` NuGet packages
(`Core`, `NIP`, `NDP`, `NWP`, `NWP.Anchor`, `NOP`) — no monorepo
dependency.

## Architecture

The full three-layer reference topology, including the two private
trust-anchor daemons, is in [`docs/architecture.md`](./docs/architecture.md).
Short version:

```
┌─────────────────────────────────────────────────────────┐
│ Layer 3 (private — innolotus org, NPS Cloud 2027 Q1+)   │
│   nps-cloud-ca · nps-ledger                             │
├─────────────────────────────────────────────────────────┤
│ Layer 2 (this repo) — network entry                     │
│   nps-gateway (public ingress) · nps-registry (NDP)     │
├─────────────────────────────────────────────────────────┤
│ Layer 1 (this repo) — host-local                        │
│   npsd (state host, port 17433) · nps-runner (FaaS)     │
└─────────────────────────────────────────────────────────┘
```

## Spec & SDK references

- [NPS-Release](https://github.com/labacacia/NPS-Release) — protocol specifications.
- [NPS-Node Profile](https://github.com/labacacia/NPS-Release/blob/main/spec/services/NPS-Node-Profile.md) — the compliance specification `npsd` is being built to satisfy.
- [NPS-Node-L1 conformance](https://github.com/labacacia/NPS-Release/blob/main/spec/services/conformance/NPS-Node-L1.md) — 21 `TC-N1-*` cases.
- [NPS-sdk-dotnet](https://github.com/labacacia/NPS-sdk-dotnet) — the .NET SDK these daemons consume.
- [labacacia/nip-ca-server](https://github.com/labacacia/nip-ca-server) — the single-org OSS CA for actual cert issuance today.

## Versioning

Tracks the umbrella SemVer of the NPS suite. While NPS is pre-1.0,
every component ships at the same `1.0.0-alpha.x` tag. Per-daemon
change history lives in each subdirectory's `CHANGELOG.md`; rolled-up
notes live at the repo root [`CHANGELOG.md`](./CHANGELOG.md).

## License

Apache License 2.0 — see [`LICENSE`](./LICENSE) and [`NOTICE`](./NOTICE).

Copyright © 2026 LabAcacia (INNO LOTUS PTY LTD).
