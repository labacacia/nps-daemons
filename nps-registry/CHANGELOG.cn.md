[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— `nps-registry`

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。Tag 跟随 NPS 套件统一 SemVer。

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

[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
