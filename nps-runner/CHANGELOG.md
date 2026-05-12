English | [中文版](./CHANGELOG.cn.md)

# Changelog — `nps-runner`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

---

## [1.0.0-alpha.6] — 2026-05-12

### Tracking the suite

- Tracks NPS suite `v1.0.0-alpha.6`. Project version, publish-overlay
  version, and all `LabAcacia.NPS.*` PackageReferences are aligned to the
  alpha.6 release train.

---

## [1.0.0-alpha.5] — 2026-05-01

### Added

- **Inbox watcher + worker spawn** — full L3 FaaS runtime replacing the
  alpha.3/alpha.4 heartbeat skeleton:
  - Self-registers with local `npsd` on startup (`POST /v1/agents`,
    idempotent — 409 returns existing NID); retries with exponential
    backoff up to 20 attempts.
  - Long-polls the runner's inbox (`GET /v1/inbox/{nid}?wait=N&batch=B`)
    at a configurable interval (`NPS_RUNNER_POLL_INTERVAL_MS`, default 1 s).
  - Deserialises JSON spawn-spec messages; see README for full field list.
  - Spawns worker subprocesses with the given `command` / `args` /
    `env` / `work_dir`.
  - Captures `stdout` + `stderr` to `NPS_RUNNER_LOG_DIR/{task_id}.log`
    with `[stdout]`/`[stderr]` prefixes.
  - Monitor loop (5 s tick) enforces `idle_timeout_seconds` (silence
    since last output) and `max_runtime_seconds` (hard wall-clock limit,
    default 4 h).
  - On worker exit: acks the inbox message; if `reply_to` is set, POSTs
    a JSON completion notification to that NID.
  - Concurrency cap (`NPS_RUNNER_MAX_CONCURRENT_WORKERS`, default 8) —
    messages arriving when at capacity stay unacked and reappear next poll.

---

## [1.0.0-alpha.4] — 2026-04-30

### Tracking the suite

- Bumps `LabAcacia.NPS.*` NuGet dependencies to `v1.0.0-alpha.4`.
  No functional changes since alpha.3 — `nps-runner` remains the
  Generic Host + 30-second heartbeat skeleton.
- Inbox watcher, `spawn_spec_ref` resolver, and worker subprocess
  lifecycle remain deferred to the L3 stage (alpha.5+).

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

[1.0.0-alpha.5]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.5
[1.0.0-alpha.4]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.4
[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
