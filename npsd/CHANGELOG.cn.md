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
  （memory-node 形态、anonymous-auth、含路由表）。
- **Sub-NID 签发** —— `POST/GET /v1/agents`、
  `GET/POST /v1/agents/{nid}`、`POST /v1/agents/{nid}/revoke`。
  IdentFrame 由主机 root key 签名（`LabAcacia.NPS.NIP` 的
  `NipSigner` 对 canonical JSON 做 Ed25519 签名）。SQLite 存
  `${NPSD_DATA_DIR}/sub-nids.sqlite`。调用方可以 BYO `agent_pub_key`
  （npsd 看不到私钥），或不传（npsd 生成一对 Ed25519，仅一次返回私钥）。
- **Per-NID inbox** —— `POST /v1/inbox/{nid}`、
  `GET /v1/inbox/{nid}?wait=N&batch=B`（long-poll）、
  `DELETE /v1/inbox/{nid}/{message_id}`（ack）、
  `GET /v1/inbox/{nid}/depth`。内存队列；支持优先级、TTL、
  per-NID 深度上限、payload 大小上限，以及 404/403/413/429
  的友好错误映射。
- 多阶段 Docker 镜像（非 root `npsd` 用户、`/data` 卷）。
- 通过 `NPSD_HOST_NID_PREFIX`、`NPSD_SUB_NID_VALIDITY_DAYS`、
  `NPSD_MAX_INBOX_DEPTH_PER_NID`、`NPSD_MAX_INBOX_MESSAGE_BYTES`、
  `NPSD_MAX_INBOX_WAIT_SECONDS` 调节运行参数。
- 17 个集成测试位于 `NPS.Tests/Daemons/Npsd/`，覆盖 issue、list、get、
  revoke、inbox 投递、long-poll、ack、depth、priority 排序、
  oversize-payload 拒绝、已 revoke NID 投递拒绝。

### 推迟到 alpha.4 / alpha.5

- NCP 原生模式 wire transport（alpha.3 仅 HTTP；NPS-RFC-0001 原生
  preamble 在 alpha.4 落地）。
- Inbox 持久化（LMDB / SQLite）—— alpha.4，跟 NCP 原生 runtime 一起，
  两者共享同一条投递流水线。
- 向本地 NDP registry 广播 AnnounceFrame。
- Sub-NID 续签（现在只能 revoke + reissue）。

---

[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
