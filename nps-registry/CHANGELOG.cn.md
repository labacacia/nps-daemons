[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— `nps-registry`

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。Tag 跟随 NPS 套件统一 SemVer。

---

## [1.0.0-alpha.4] —— 2026-04-30

### 新增

- **SQLite 实仓 NDP registry** —— `SqliteNdpRegistry` 替换 alpha.3 stub。
  完整实现 NDP `Resolve` / `Graph` / `Announce` URL 接口，落到真实
  持久化存储 `${NPSREG_DATA_DIR:-/data}/registry.sqlite`：
  - `POST /v1/announce` —— 收 `AnnounceFrame`，持久化 NID → endpoint
    绑定 + TTL + 签名。
  - `GET /v1/resolve?nid=<nid>` —— NID 解析到 endpoint，读时按 TTL
    懒过期。
  - `GET /v1/graph?nid=<nid>&depth=<N>` —— 限深 BFS 遍历（默认上限 5，
    遵循 NDP 规范），带环路检测。
- 10 个集成测试位于 `NPS.Tests/Daemons/NpsRegistry/`，覆盖注册、解析、
  图谱遍历、TTL 淘汰、并发写、超大 announce 拒绝。

### 跟随套件的协议变更

- `LabAcacia.NPS.*` NuGet 依赖升至 `v1.0.0-alpha.4`。

### 推迟到 alpha.5+

- L2 HA-cluster 模式的跨机 federation / gossip。
- 可选 Postgres 后端，用于集群规模部署。
- BFS + 环路检测之外的图谱遍历优化（例如查询缓存、并行遍历）。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 新增

- 首次发布。NPS 套件的 Layer-2 跨机 NDP 发现 registry。
- Phase 1 骨架：在 NDP 可选专用端口 `17436` 监听；`Resolve` / `Graph` /
  `Announce` 全部返回 `NDP-REGISTRY-UNAVAILABLE`（HTTP 503），方便消费者
  预先接线 + 优雅降级。`/health` 在监听器拉起后返回 200。
- 多阶段 Docker 镜像（非 root `npsreg` 用户，暴露 `:17436`）。

### 推迟到 alpha.4

- SQLite 实仓注册表。
- Announce 签名验证 + 基于 TTL 的过期淘汰。
- 图谱遍历优化（BFS + 环路检测）。
- 可选 Postgres 后端，用于集群规模部署。

---

[1.0.0-alpha.4]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.4
[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
