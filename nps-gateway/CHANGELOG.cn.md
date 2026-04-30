[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— `nps-gateway`

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。Tag 跟随 NPS 套件统一 SemVer。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 新增

- 首次发布。NPS 套件的 Layer-2 公网 Internet ingress。
- Phase 1 骨架：`:8080` HTTP 监听 + `/health` 文档化里程碑，运维
  可以在 alpha.3 就把 nginx/Caddy/Traefik 接到它前面，alpha.4 → alpha.5
  只翻行为开关。
- 多阶段 Docker 镜像（非 root `npsgw` 用户，暴露 `:8080`）。

### 推迟到 alpha.4 / alpha.5

- TLS 卸载（alpha.3 仅 HTTP，TLS 在上游处理）。
- Rate limit（按 NID / 按客户 / 按路由）。
- NeuronHub 客户鉴权 + 按客户触发 NPT 计费。
- 路由前的 NPS-RFC-0004 reputation 查询。
- `LabAcacia.NPS.NWP.Anchor` Anchor Node 中间件接线（NPS-CR-0001）。
- DDoS 防护（慢连接超时、请求频率上限、fail2ban 钩子）。

---

[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
