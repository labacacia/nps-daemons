English | [中文版](./CHANGELOG.cn.md)

# Changelog — NPS Daemons (bundle)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Until NPS reaches v1.0 stable, every repository in the suite is synchronized to the same pre-release version tag.

---

## [1.0.0-alpha.3] — 2026-04-26

### Added

- First independent release as a standalone repository at
  [`labacacia/nps-daemons`](https://github.com/labacacia/nps-daemons)
  ([Gitee mirror](https://gitee.com/labacacia/nps-daemons)). Up to and
  including v1.0.0-alpha.2 these daemons did not exist yet; they were
  introduced as a group in dev as the alpha.3 deployment surface and
  have been promoted to their own repo on first release.
- **`npsd`** (Layer 1, host-local NCP wire + state host) — L1 minimum:
  binds `127.0.0.1:17433`, generates a root Ed25519 keypair on first
  start (PKCS#8, file mode `0600` — satisfies NPS-Node Profile L1
  conformance test `TC-N1-NIP-01`), serves `GET /health` and a
  daemon-self `GET /.nwm`.
- **`nps-runner`** (Layer 1, task scheduler / FaaS runtime) — Phase 1
  skeleton: Generic Host scaffolding with a 30-second heartbeat. Inbox
  watcher + `spawn_spec_ref` resolver + worker lifecycle land at L3
  stage (alpha.5+).
- **`nps-gateway`** (Layer 2, public Internet ingress) — Phase 1
  skeleton: HTTP listener on `:8080` + `/health` documenting planned
  milestones. TLS termination, rate-limit, NeuronHub auth, NPT debit,
  NPS-RFC-0004 reputation lookup, and NPS-CR-0001 Anchor Node
  middleware wiring land alpha.4 → alpha.5.
- **`nps-registry`** (Layer 2, cross-machine NDP discovery) — Phase 1
  skeleton: HTTP listener on the NDP optional-dedicated port `17436`;
  `Resolve` / `Graph` / `Announce` URL surface returns
  `NDP-REGISTRY-UNAVAILABLE` (HTTP 503) so consumers can wire and
  gracefully fall back. SQLite-backed real registration lands alpha.4.
- Bundle README (EN + CN), top-level `docker-compose.yml` to spin up
  all four daemons at once, repo-level `LICENSE` + `NOTICE`, and
  `docs/architecture.{md,cn.md}` carrying the full three-layer
  topology (including the two private trust-anchor daemons that live
  in `innolotus/nps-cloud-ca` and `innolotus/nps-ledger`).

### Changed

- All four daemon `*.csproj` files switched from in-tree
  `<ProjectReference>` to published
  [`LabAcacia.NPS.*`](https://www.nuget.org/profiles/LabAcacia/) NuGet
  packages at v1.0.0-alpha.3. The publish repo is now self-contained
  and builds without the development monorepo.
- Dockerfile context changed from monorepo-relative (`../../..`) to
  repo-root, so `docker build .` works directly inside each daemon
  subdirectory of the publish repo.

### Tracking the suite

This release rolls up suite-wide protocol changes that landed in NPS
`v1.0.0-alpha.3`:

- **RFC-0001** — NCP connection preamble (preamble runtime in `npsd`
  itself lands at alpha.4 — Phase 1 here is library-only).
- **RFC-0003** — Agent identity assurance levels (touches NIP and the
  `npsd` self-`/.nwm` `min_assurance_level` field).
- **RFC-0004** — NID reputation log (Phase 1; entry shape only —
  `nps-ledger`'s in-memory honouring lives in `innolotus/nps-ledger`,
  not this repo).
- **CR-0001** — Anchor + Bridge Node split (`nps-gateway` is wired
  against the new `LabAcacia.NPS.NWP.Anchor` package).

See [`NPS-Release/CHANGELOG.md`](https://github.com/labacacia/NPS-Release/blob/main/CHANGELOG.md)
for the full suite-level rollup.

---

[1.0.0-alpha.3]: https://github.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
