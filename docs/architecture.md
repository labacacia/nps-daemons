English | [中文版](./architecture.cn.md)

# NPS Daemon Architecture

> Reference deployment topology for NPS in production. Six resident services
> spread across three layers; each layer is independently scalable and has
> distinct trust / failure boundaries.
>
> Status — v1.0-alpha.3: layer 1 (`npsd`) is the only daemon with a
> functional reference implementation; the remaining five ship as
> skeleton projects so the deployment surface and process names are
> stable while implementation fills in across alpha.4 → beta.

---

## The three layers

```
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 3 — Trust anchor       (NPS Cloud, 2027 Q1+)                   │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ⑤ nps-cloud-ca   │                  │ ⑥ nps-ledger     │           │
│ │ X.509 issuance   │                  │ K-of-N audit log │           │
│ │ CRL / OCSP       │                  │ tamper-evident   │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 2 — Network entry       (per ingress kind, on demand)          │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ③ nps-gateway    │                  │ ④ nps-registry   │           │
│ │ Public Internet  │                  │ Cross-machine    │           │
│ │ NPS-over-TLS     │                  │ NDP resolve+graph│           │
│ │ rate-limit + NPT │                  │ (L2 stage)       │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 1 — Host-local          (one of each per machine)              │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ① npsd           │ ◀── inbox ─────  │ ② nps-runner     │           │
│ │ NCP wire +       │                  │ FaaS scheduler   │           │
│ │ root keypair +   │ ── spawn-spec ─▶ │ ephemeral worker │           │
│ │ session reg +    │                  │ lifecycle (L3)   │           │
│ │ inbox            │                  │                  │           │
│ │ port :17433      │                  │                  │           │
│ └──────────────────┘                  └──────────────────┘           │
└──────────────────────────────────────────────────────────────────────┘
```

## The six daemons

### Layer 1 — Host-local (one of each per machine)

#### ① `npsd` — Protocol ingress + state host

- **Listens on** `127.0.0.1:17433` by default (unified port for the entire NPS suite).
- **Handles** NCP handshake (including the [NPS-RFC-0001](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.md) connection preamble), frame encode/decode, AnnounceFrame emission, root Ed25519 keypair management, sub-NID issuance for local agents, the per-host session registry, and the per-NID inbox queue used for `ephemeral` activation_mode delivery.
- **Why always-on**: identity and inbox cannot follow a session — persistent state needs a host. Every NPS client on the machine (MCP shim, resident agent, worker, gateway) connects through this daemon.
- **Why not merged into anything else**: protocol layer is business-agnostic and shared by all upper layers; folding it into a business process pollutes the trust domain.
- **Reference compliance**: target is `NPS-Node Profile L1` (see [`spec/services/NPS-Node-Profile.md`](https://github.com/labacacia/NPS-Release/blob/main/spec/services/NPS-Node-Profile.md) and the L1 conformance suite at [`spec/services/conformance/NPS-Node-L1.md`](https://github.com/labacacia/NPS-Release/blob/main/spec/services/conformance/NPS-Node-L1.md)).

#### ② `nps-runner` — Task scheduler / FaaS runtime *(L3 stage)*

- **Watches** the inbox for messages addressed to `ephemeral`-mode NIDs, consults the per-NID `spawn_spec_ref` (NDP §3.1), spawns the corresponding worker subprocess, and manages its lifecycle (timeout, concurrency cap, retry, result reclamation).
- **Why always-on**: messages arrive any time; the scheduler must be there to spawn. Workers are ephemeral but the scheduler is not.
- **Why separated from `npsd`**: resource profile is radically different — `npsd` is steady-state, `nps-runner` produces process-spawn bursts; failure isolation is critical (a worker crash must not take the NCP layer down); trust boundary differs (`nps-runner` runs user-supplied Agent SDK code, the protocol layer must not have that permission surface).

### Layer 2 — Network entry (per ingress kind, on demand)

#### ③ `nps-gateway` — Internet ingress

- **Translates** Internet-side NPS-over-TLS traffic into local frames. Performs TLS termination, rate limiting, NeuronHub-customer authentication, NPT debit triggering, reputation checks (against [NPS-RFC-0004](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md)), DDoS defense.
- **Why always-on**: the publicly bound port must always be listening.
- **Why separated from `npsd`**: `npsd` is bound to loopback by default; the gateway binds public addresses. Attack surface, security posture, and operational scope differ. `npsd` failure affects all local NPS traffic; gateway failure only affects external inbound — the two MUST be independently restartable.
- **Note**: This is a **process-level** name; the *spec*-level role of cluster control plane is now called **Anchor Node** in NWP, see [NPS-CR-0001](https://github.com/labacacia/NPS-Release/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md). The `nps-gateway` process MAY host an Anchor Node middleware but is not constrained to.

#### ④ `nps-registry` — Discovery registry *(L2 stage, optionally hosted)*

- **Centralised** NDP discovery service for cross-machine resolution. Responds to `Resolve` and `Graph` queries; aggregates registrations from multiple machines.
- **Per-host `npsd` only knows local sessions**; cross-machine queries go to a registry.
- **Why always-on**: discovery requests arrive arbitrarily; the registry cannot cold-start.
- **Why independent of `npsd`**: `npsd` is one-per-machine; registry is one-per-network (or HA cluster). Not needed at L1; appears at L2. NeuronHub will run one for its customers; LabAcacia will run a public-good open-source one.

### Layer 3 — Trust anchor *(NPS Cloud, 2027 Q1+)*

#### ⑤ `nps-cloud-ca` — Certificate authority

- **NIP trust root** that issues cross-organisation NID certificates and serves CRL / OCSP. LabAcacia operates an open-source instance; commercial users may operate private CAs.
- **Why always-on**: revocation queries must respond in real time; issuance requests arrive arbitrarily.
- **Why independent of `nps-registry`**: CA is the *source* of trust; registry is an *index* of information. CA failure prevents new NID onboarding and revocation responses; registry failure prevents discovery only. Trust level, operations process, audit requirements all differ. CA private keys MUST be HSM- or physical-security-protected; registry has no equivalent constraint.

#### ⑥ `nps-ledger` — Audit / compliance log collector

- **Append-only collection point** for the K-of-N audit consensus that NeuronHub NPT settlement ultimately depends on. Implements the Certificate-Transparency-style log defined by [NPS-RFC-0004](https://github.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md): submission, query, signed tree head, inclusion proofs.
- **Why always-on**: audit logs are a continuous append stream; the receiver cannot be down or there will be gaps.
- **Why independent**: audit isolation requirements are the strongest of all six daemons — `nps-ledger` MUST NOT trust the processes it audits, so it must run in its own trust domain. Even if `npsd` is compromised, `nps-ledger` should still produce independent evidence.

---

## Naming and binary layout

| Daemon | Folder | Project | Binary | NuGet package |
|--------|--------|---------|--------|----------------|
| ① | `tools/daemons/npsd/` | `Npsd.csproj` | `npsd` | `LabAcacia.NPS.Daemon.Npsd` |
| ② | `tools/daemons/nps-runner/` | `NpsRunner.csproj` | `nps-runner` | `LabAcacia.NPS.Daemon.Runner` |
| ③ | `tools/daemons/nps-gateway/` | `NpsGateway.csproj` | `nps-gateway` | `LabAcacia.NPS.Daemon.Gateway` |
| ④ | `tools/daemons/nps-registry/` | `NpsRegistry.csproj` | `nps-registry` | `LabAcacia.NPS.Daemon.Registry` |
| ⑤ | `tools/daemons/nps-cloud-ca/` | `NpsCloudCa.csproj` | `nps-cloud-ca` | `LabAcacia.NPS.Daemon.CloudCa` |
| ⑥ | `tools/daemons/nps-ledger/` | `NpsLedger.csproj` | `nps-ledger` | `LabAcacia.NPS.Daemon.Ledger` |

## Phasing across alpha + beta

| Daemon | alpha.3 (this release) | alpha.4 | alpha.5+ | beta / 1.0 |
|--------|------------------------|---------|----------|------------|
| `npsd` | L1 minimum: HTTP listener on `127.0.0.1:17433`, root keypair generation, `/.nwm`, baseline `/health`, AnnounceFrame on local NDP | NCP native-mode preamble runtime (NPS-RFC-0001 Phase 2 in .NET) | Inbox persistence, sub-NID issuance, push to resident agents | Conformance with full L1 + L2 |
| `nps-runner` | Skeleton + Generic-Host worker; documents the inbox-watch contract | Inbox poller wired through `npsd` | Spawn lifecycle, isolation | L3 conformance |
| `nps-gateway` | Skeleton + HTTP listener on `0.0.0.0:443` (TLS off); documents NeuronHub integration points | Anchor Node middleware (NPS-CR-0001 wiring) | Reputation lookup (NPS-RFC-0004 Phase 2) | DDoS, NPT debit |
| `nps-registry` | Skeleton + HTTP listener; placeholder `Resolve`/`Graph` endpoints returning `NDP-REGISTRY-UNAVAILABLE` | Real registry backed by SQLite | HA cluster mode | Federation |
| `nps-cloud-ca` | Skeleton; defers to `tools/nip-ca-server*` (six per-language OSS CAs) for actual issuance | NPS-RFC-0002 X.509 + ACME wiring | HSM integration | Cross-CA trust |
| `nps-ledger` | Skeleton + in-memory log honouring the NPS-RFC-0004 Phase 1 entry shape | Persistent + Merkle tree (NPS-RFC-0004 Phase 2) | STH gossip, public mirror | Public log accreditation |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
