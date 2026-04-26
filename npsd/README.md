English | [中文版](./README.cn.md)

# `npsd` — NPS Daemon (Layer 1, host-local)

> Reference implementation of the host-local NPS daemon. Listens on the
> unified suite port `17433`, holds the host's root Ed25519 keypair,
> and is the local entry point for every NPS client on the machine
> (MCP shim, resident agent, worker, gateway). See
> [`docs/daemons/architecture.md`](../docs/architecture.md)
> for the broader six-daemon topology.

## What this binary does (alpha.3 scope)

- Binds `127.0.0.1:17433` by default (loopback only — public ingress is `nps-gateway`'s job).
- Generates a root Ed25519 keypair on first start, persists it to `~/.local/share/npsd/root.ed25519.pkcs8` with mode `0600` (satisfies the NPS-Node Profile L1 conformance test `TC-N1-NIP-01 — Root keypair generation and permission`).
- Serves `GET /health` for Docker `HEALTHCHECK` / systemd liveness.
- Serves `GET /.nwm` returning the daemon's own minimal Neural Web Manifest (memory-node shape, anonymous-auth, no actions). Application-level `/.nwm` is served by whatever the operator wires through the application's own NWP stack — not by `npsd`.

## What is NOT yet implemented at alpha.3

These are tracked in `docs/daemons/architecture.md` under the per-daemon phasing table:

- NCP native-mode wire transport (HTTP-only here; native-mode preamble runtime per [NPS-RFC-0001](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.md) lands at alpha.4).
- Per-NID inbox queue persistence + push to `resident` agents.
- Sub-NID issuance for local agents.
- AnnounceFrame emission to the local NDP registry.

## Quick start

### Local

```bash
dotnet run --project tools/daemons/npsd/Npsd.csproj
# → Now listening on: http://127.0.0.1:17433
# → Logs: "npsd starting; root NID host fingerprint = <8-hex>"

curl -s http://127.0.0.1:17433/health | jq
curl -s http://127.0.0.1:17433/.nwm   | jq
```

### Docker

```bash
docker build -f tools/daemons/npsd/Dockerfile -t labacacia/npsd:1.0.0-alpha.3 .
docker run --rm -p 17433:17433 \
  -v npsd-data:/data \
  labacacia/npsd:1.0.0-alpha.3
```

## Configuration (env vars)

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSD_PORT` | `17433` | TCP port to bind. |
| `NPSD_HOST` | `127.0.0.1` | Bind address. Use `0.0.0.0` only inside an isolated network namespace — never expose `npsd` directly to the public internet (use `nps-gateway`). |
| `NPSD_DATA_DIR` | `~/.local/share/npsd` | Persistent state (root keypair, future inbox storage). |

## Spec references

- [NPS-Node Profile](https://github.com/labacacia/NPS-Release/blob/main/spec/services/NPS-Node-Profile.md) — the compliance specification this daemon targets.
- [NPS-Node-L1 conformance suite](https://github.com/labacacia/NPS-Release/blob/main/spec/services/conformance/NPS-Node-L1.md) — the 21 `TC-N1-*` cases this daemon is being built to pass.
- [Daemon architecture](../docs/architecture.md) — the six-daemon, three-layer reference deployment.
- [NPS-1 NCP](https://github.com/labacacia/NPS-Release/blob/main/spec/NPS-1-NCP.md) — the wire layer.
- [NPS-3 NIP](https://github.com/labacacia/NPS-Release/blob/main/spec/NPS-3-NIP.md) — root keypair / IdentFrame semantics.

## License

Apache-2.0 — see `LICENSE` at the repository root.
