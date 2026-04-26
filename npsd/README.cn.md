[English Version](./README.md) | 中文版

# `npsd` —— NPS Daemon（第一层，本机）

> 本机 NPS daemon 的参考实现。监听协议族统一端口 `17433`，持有本机 root
> Ed25519 keypair，是本机所有 NPS 客户端（MCP shim、resident agent、worker、
> gateway）的本机入口。完整六-daemon 拓扑见
> [`docs/daemons/architecture.cn.md`](../docs/architecture.cn.md)。

## 这个二进制做什么（alpha.3 范围）

- 默认 bind `127.0.0.1:17433`（仅 loopback —— 公网入口由 `nps-gateway` 负责）。
- 首次启动生成 root Ed25519 keypair，持久化到 `~/.local/share/npsd/root.ed25519.pkcs8`，权限 `0600`（满足 NPS-Node Profile L1 合规测试 `TC-N1-NIP-01 — Root keypair generation and permission`）。
- 提供 `GET /health`，给 Docker `HEALTHCHECK` 与 systemd liveness 用。
- 提供 `GET /.nwm`，返回 daemon 自身的最小 Neural Web Manifest（memory-node 形态、anonymous-auth、无 actions）。应用级 `/.nwm` 由 operator 在自己应用的 NWP stack 上对外暴露 —— 不由 `npsd` 提供。

## alpha.3 阶段尚未实现的功能

阶段化跟踪见 `docs/daemons/architecture.cn.md` 的 per-daemon phasing 表：

- NCP 原生模式 wire 传输（这里仅 HTTP；原生模式前导 runtime 见 [NPS-RFC-0001](https://gitee.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.cn.md)，alpha.4 落地）。
- 按 NID 维护 inbox 队列持久化 + 推送到 `resident` agent。
- 为本机 agent 签发 sub-NID。
- 向本机 NDP registry 上发 AnnounceFrame。

## 快速上手

### 本机

```bash
dotnet run --project tools/daemons/npsd/Npsd.csproj
# → Now listening on: http://127.0.0.1:17433
# → 日志："npsd starting; root NID host fingerprint = <8-hex>"

curl -s http://127.0.0.1:17433/health | jq
curl -s http://127.0.0.1:17433/.nwm   | jq
```

### Docker

```bash
docker build -f tools/daemons/npsd/Dockerfile -t labacacia/npsd:1.0.0-alpha.3 .
docker run --rm -p 17433:17433 \
  -v npsd-data:/data \
  labacacia/npsd:1.0.0-alpha.3
```

## 配置（环境变量）

| 变量 | 默认值 | 用途 |
|-----|------|-----|
| `NPSD_PORT` | `17433` | bind TCP 端口。|
| `NPSD_HOST` | `127.0.0.1` | bind 地址。`0.0.0.0` 仅在隔离网络 namespace 中使用 —— 永远不要把 `npsd` 直接暴露到公网（用 `nps-gateway`）。|
| `NPSD_DATA_DIR` | `~/.local/share/npsd` | 持久化状态（root keypair、未来的 inbox 存储）。|

## 规范引用

- [NPS-Node Profile](https://gitee.com/labacacia/NPS-Release/blob/main/spec/services/NPS-Node-Profile.cn.md) —— 本 daemon 目标合规规范。
- [NPS-Node-L1 合规测试套件](https://gitee.com/labacacia/NPS-Release/blob/main/spec/services/conformance/NPS-Node-L1.cn.md) —— 本 daemon 力争通过的 21 个 `TC-N1-*` 用例。
- [Daemon 架构](../docs/architecture.cn.md) —— 六-daemon、三层参考部署。
- [NPS-1 NCP](https://gitee.com/labacacia/NPS-Release/blob/main/spec/NPS-1-NCP.cn.md) —— wire 层。
- [NPS-3 NIP](https://gitee.com/labacacia/NPS-Release/blob/main/spec/NPS-3-NIP.cn.md) —— root keypair / IdentFrame 语义。

## 许可证

Apache-2.0 —— 见仓库根 `LICENSE`。
