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
  Manifest (Memory-node shape, anonymous-auth, route catalog).
- **Sub-NID issuance** — `POST/GET /v1/agents`,
  `GET/POST /v1/agents/{nid}`, `POST /v1/agents/{nid}/revoke`. Mints
  IdentFrames signed by the host root key (`LabAcacia.NPS.NIP`'s
  `NipSigner` over canonical JSON). SQLite-backed at
  `${NPSD_DATA_DIR}/sub-nids.sqlite`. Caller can BYO `agent_pub_key`
  (npsd never sees the private half) or omit it (npsd mints an Ed25519
  keypair and returns the private key once).
- **Per-NID inbox** — `POST /v1/inbox/{nid}`,
  `GET /v1/inbox/{nid}?wait=N&batch=B` (long-poll),
  `DELETE /v1/inbox/{nid}/{message_id}` (ack), `GET /v1/inbox/{nid}/depth`.
  In-memory queue; supports priority, TTL, per-NID depth caps, payload
  size caps, and ergonomic 404/403/413/429 error mapping.
- Multi-stage Docker image (non-root `npsd` user, `/data` volume).
- Configurable knobs via `NPSD_HOST_NID_PREFIX`,
  `NPSD_SUB_NID_VALIDITY_DAYS`, `NPSD_MAX_INBOX_DEPTH_PER_NID`,
  `NPSD_MAX_INBOX_MESSAGE_BYTES`, `NPSD_MAX_INBOX_WAIT_SECONDS`.
- 17 integration tests under `NPS.Tests/Daemons/Npsd/` covering issue,
  list, get, revoke, inbox deposit, long-poll, ack, depth, priority
  ordering, oversize-payload rejection, and revoked-NID inbox refusal.

### Deferred to alpha.4 / alpha.5

- NCP native-mode wire transport (HTTP-only at alpha.3; native preamble
  runtime per NPS-RFC-0001 lands alpha.4).
- Inbox persistence (LMDB / SQLite) — alpha.4, alongside the NCP native
  runtime. They share the same delivery pipeline.
- AnnounceFrame emission to the local NDP registry.
- Sub-NID renewal (currently you revoke + reissue).

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
