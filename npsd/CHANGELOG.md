English | [中文版](./CHANGELOG.cn.md)

# Changelog — `npsd`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

---

## [1.0.0-alpha.3] — 2026-04-26

### Added

- First release. Layer-1 host-local daemon for the NPS suite.
- Binds `127.0.0.1:17433` by default; override with `NPSD_HOST` / `NPSD_PORT`.
- Generates a root Ed25519 keypair on first start; persists to
  `${NPSD_DATA_DIR:-~/.local/share/npsd}/root.ed25519.pkcs8` with POSIX
  mode `0600` — satisfies NPS-Node Profile L1 conformance test
  `TC-N1-NIP-01 — Root keypair generation and permission`.
- Serves `GET /health` (Docker `HEALTHCHECK` / systemd liveness target).
- Serves `GET /.nwm` returning the daemon's own minimal Neural Web
  Manifest (Memory-node shape, anonymous-auth, no actions).
- Multi-stage Docker image (non-root `npsd` user, `/data` volume).

### Deferred to alpha.4 / alpha.5

- NCP native-mode wire transport (HTTP-only at alpha.3; native preamble runtime per NPS-RFC-0001 lands alpha.4).
- Per-NID inbox queue persistence + push to resident agents.
- Sub-NID issuance for local agents.
- AnnounceFrame emission to the local NDP registry.

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
