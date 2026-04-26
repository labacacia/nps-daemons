English | [中文版](./CHANGELOG.cn.md)

# Changelog — `nps-gateway`

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Tags follow the umbrella SemVer of the NPS suite.

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
- NeuronHub-customer authentication and per-customer NPT debit triggering.
- NPS-RFC-0004 reputation lookup before routing.
- `LabAcacia.NPS.NWP.Anchor` Anchor Node middleware wiring (NPS-CR-0001).
- DDoS defence (slow-loris timeout, request-rate caps, fail2ban hooks).

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
