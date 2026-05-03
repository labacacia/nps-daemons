English | [中文版](./README.cn.md)

# `nps-runner` — NPS Daemon (Layer 1, host-local task scheduler)

> Reference implementation of the host-local FaaS runtime. Watches the
> [`npsd`](../npsd/) inbox for messages addressed to `ephemeral`-mode
> NIDs and spawns the corresponding worker subprocess per the NID's
> `spawn_spec_ref` (NDP §3.1). See
> [`docs/daemons/architecture.md`](../docs/architecture.md)
> for the broader six-daemon topology.

## Status — alpha.4

**Phase 1 skeleton.** The Generic Host scaffolding is in place; the
binary builds, starts, and emits a 30-second heartbeat log. The actual
inbox watch + spawn-spec resolver + worker lifecycle management land
at the L3 stage (alpha.5+) per the
[daemon phasing table](../docs/architecture.md#phasing-across-alpha--beta).

This skeleton has been in place since alpha.3 so that the deployment surface (process
name, NuGet package id, Docker image tag, systemd unit name) is stable
from the start of the daemon ecosystem rather than appearing late.

## Quick start

```bash
dotnet run --project tools/daemons/nps-runner/NpsRunner.csproj
# → "nps-runner v1.0.0-alpha.4 starting (Phase 1 skeleton — ...)"
# → "nps-runner heartbeat — Phase 1 skeleton, no work to do yet"  (every 30 s)
```

### Docker

```bash
docker build -f tools/daemons/nps-runner/Dockerfile -t labacacia/nps-runner:1.0.0-alpha.4 .
docker run --rm labacacia/nps-runner:1.0.0-alpha.4
```

## Why a separate daemon (and not part of `npsd`)

See [architecture §1](../docs/architecture.md#-nps-runner--task-scheduler--faas-runtime-l3-stage):
resource profile, failure isolation, and trust boundary all differ
significantly between the protocol layer and the worker scheduler — a
worker crash must not take the NCP layer down, and the scheduler runs
user-supplied Agent SDK code which the protocol layer must not have a
permission surface for.

## License

Apache-2.0 — see `LICENSE` at the repository root.
