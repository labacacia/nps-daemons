English | [中文版](./CHANGELOG.cn.md)

# Changelog — `nps-runner`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

---

## [1.0.0-alpha.3] — 2026-04-26

### Added

- First release. Layer-1 task scheduler / FaaS runtime for the NPS suite.
- Phase 1 skeleton: Generic Host scaffolding with a 30-second heartbeat,
  so the deployment surface is stable and operators can wire it into
  systemd / docker compose without waiting for the full implementation.
- Multi-stage Docker image (non-root `npsrunner` user, no exposed ports — pulls work from the colocated `npsd`).

### Deferred to alpha.5+ (L3 stage)

- Inbox watcher polling local `npsd` for messages addressed to
  ephemeral-mode NIDs.
- `spawn_spec_ref` resolver — given an ephemeral NID, fetch the
  spawn spec from the originating Anchor / Memory Node.
- Worker subprocess lifecycle: launch, monitor, kill on completion or
  on idle timeout per the spawn spec.

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
