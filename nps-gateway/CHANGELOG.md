English | [中文版](./CHANGELOG.cn.md)

# Changelog — `nps-gateway`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

---

## [1.0.0-alpha.5.2] — 2026-05-03

### Tracking the suite

Tracks NPS suite `v1.0.0-alpha.5.2`. No daemon-specific code changes — the
`estimated_npt → cgn_est` wire field rename (NPS-Dev#17) lands in the protocol
layer; all downstream components must upgrade together per suite-wide versioning
policy.

---

## [1.0.0-alpha.5] — 2026-05-01

### Changed

- Bumps `LabAcacia.NPS.NWP.Anchor` dependency to `1.0.0-alpha.5.2`.
  The hotfix renames the wire field `estimated_npt` → `cgn_est` in
  topology events to match the Cognon Budget spec
  (NPS-5 §4.3 / NPS-AaaS §2.3).
- No changes to the gateway's own code; the API surface and routing
  behaviour are identical to alpha.4.

---

## [1.0.0-alpha.4] — 2026-04-30

### Tracking the suite

- Bumps `LabAcacia.NPS.*` NuGet dependencies to `v1.0.0-alpha.4`,
  including the new `LabAcacia.NPS.NWP.Anchor` package which now ships
  **NPS-CR-0002** topology query types (`topology.snapshot` /
  `topology.stream`). The gateway daemon does **not** wire these yet —
  Anchor middleware integration remains the alpha.4 → alpha.5 work.
- No functional changes in the gateway itself since alpha.3 — still the
  `:8080` HTTP listener + `/health` skeleton documenting the planned
  TLS / rate-limit / auth / CGN-debit / reputation-lookup path.

---

## [1.0.0-alpha.3] — 2026-04-26

### Added

- First release. Layer-2 public Internet ingress for the NPS suite.
- Phase 1 skeleton: HTTP listener on `:8080` and `/health` documenting
  the planned milestones, so operators can put nginx/Caddy/Traefik in
  front of it during alpha.3 and only flip behavior at alpha.4 → alpha.5.
- Multi-stage Docker image (non-root `npsgw` user, exposes `:8080`).

### Deferred to alpha.4 / alpha.5

- TLS termination (alpha.3 ships plain HTTP — terminate upstream).
- Rate limiting (per-NID + per-customer + per-route).
- NeuronHub-customer authentication and per-customer CGN debit triggering.
- NPS-RFC-0004 reputation lookup before routing.
- `LabAcacia.NPS.NWP.Anchor` Anchor Node middleware wiring (NPS-CR-0001).
- DDoS defence (slow-loris timeout, request-rate caps, fail2ban hooks).

---

[1.0.0-alpha.5.2]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.5.2
[1.0.0-alpha.5]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.5
[1.0.0-alpha.4]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.4
[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
