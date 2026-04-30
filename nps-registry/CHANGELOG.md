English | [中文版](./CHANGELOG.cn.md)

# Changelog — `nps-registry`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

---

## [1.0.0-alpha.3] — 2026-04-26

### Added

- First release. Layer-2 cross-machine NDP discovery registry for the NPS suite.
- Phase 1 skeleton: HTTP listener on the NDP optional-dedicated port `17436`;
  `Resolve` / `Graph` / `Announce` URLs return `NDP-REGISTRY-UNAVAILABLE`
  (HTTP 503) so consumers can wire and gracefully fall back. `/health`
  returns 200 once the listener is up.
- Multi-stage Docker image (non-root `npsreg` user, exposes `:17436`).

### Deferred to alpha.4

- SQLite-backed real registration table.
- Announce signature verification + TTL-based eviction.
- Graph-traversal optimisation (BFS + cycle detection).
- Optional Postgres backend for cluster-grade deployments.

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
