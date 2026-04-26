[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— `npsd`

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。Tag 跟随 NPS 套件统一 SemVer。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 新增

- 首次发布。NPS 套件的 Layer-1 主机本地 daemon。
- 默认绑定 `127.0.0.1:17433`；可用 `NPSD_HOST` / `NPSD_PORT` 覆盖。
- 首次启动生成 root Ed25519 keypair，落盘到
  `${NPSD_DATA_DIR:-~/.local/share/npsd}/root.ed25519.pkcs8`，
  POSIX 文件权限 `0600` —— 满足 NPS-Node Profile L1 合规用例
  `TC-N1-NIP-01 — Root keypair generation and permission`。
- 暴露 `GET /health`（Docker `HEALTHCHECK` / systemd 活性探针目标）。
- 暴露 `GET /.nwm`，返回 daemon 自身的最小 Neural Web Manifest
  （memory-node 形态、anonymous-auth、无 action）。
- 多阶段 Docker 镜像（非 root `npsd` 用户、`/data` 卷）。

### 推迟到 alpha.4 / alpha.5

- NCP 原生模式 wire transport（alpha.3 仅 HTTP；NPS-RFC-0001 原生 preamble 在 alpha.4 落地）。
- 每 NID 的 inbox 队列持久化 + 向 resident 代理推送。
- 为本机代理签发 sub-NID。
- 向本地 NDP registry 广播 AnnounceFrame。

---

[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
