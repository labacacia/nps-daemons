[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— `nps-runner`

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。Tag 跟随 NPS 套件统一 SemVer。

---

## [1.0.0-alpha.4] —— 2026-04-30

### 跟随套件的协议变更

- `LabAcacia.NPS.*` NuGet 依赖升至 `v1.0.0-alpha.4`。自 alpha.3 无功能
  变更 —— `nps-runner` 仍是 Generic Host + 30 秒心跳的骨架。
- Inbox 监听、`spawn_spec_ref` 解析、worker 子进程生命周期仍推迟到
  L3 阶段（alpha.5+）。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 新增

- 首次发布。NPS 套件的 Layer-1 任务调度 / FaaS runtime。
- Phase 1 骨架：Generic Host 脚手架 + 30 秒心跳，让部署面先稳定，
  运维可以把它接进 systemd / docker compose 而不必等完整实现。
- 多阶段 Docker 镜像（非 root `npsrunner` 用户，不暴露端口 ——
  从同主机的 `npsd` 拉任务）。

### 推迟到 alpha.5+（L3 阶段）

- Inbox 监听：轮询本机 `npsd` 找发往 ephemeral 模式 NID 的消息。
- `spawn_spec_ref` 解析：拿到 ephemeral NID 后，从源 Anchor / Memory
  Node 取回 spawn spec。
- worker 子进程生命周期：按 spawn spec 拉起、监控、完成或空闲超时后回收。

---

[1.0.0-alpha.4]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.4
[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
