[English Version](./README.md) | 中文版

# NPS Daemons

[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](./LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/labacacia/nps-daemons?include_prereleases)](https://github.com/labacacia/nps-daemons/releases)
[![Architecture](https://img.shields.io/badge/architecture-3--layer-success)](./docs/architecture.cn.md)

**Neural Protocol Suite（NPS）** 的参考部署二进制 ——
覆盖 NPS 标准三层部署拓扑中**主机层 + 网络入口层** 4 个 OSS daemon。

> 源仓库：[gitee.com/labacacia/nps-daemons](https://gitee.com/labacacia/nps-daemons) ·
> GitHub 镜像：[github.com/labacacia/nps-daemons](https://github.com/labacacia/nps-daemons) ·
> 套件：[NPS-Release](https://gitee.com/labacacia/NPS-Release) ·
> 架构：[docs/architecture.cn.md](./docs/architecture.cn.md)

---

## 仓库内容

| 层 | Daemon | 默认端口 | `v1.0.0-alpha.3` 状态 |
|----|--------|----------|------------------------|
| 1 | [`npsd`](./npsd/) | `127.0.0.1:17433` | L1 最小集：HTTP 监听、root keypair 生成（POSIX `0600`）、`/.nwm`、`/health`。|
| 1 | [`nps-runner`](./nps-runner/) | —（worker）| Phase 1 骨架 —— Generic Host 脚手架 + 30 秒心跳。Inbox 监听 + spawn-spec 解析在 alpha.5+。|
| 2 | [`nps-gateway`](./nps-gateway/) | `:8080` | Phase 1 骨架 —— 公网 HTTP 监听 + `/health`。TLS 卸载 + rate limit + auth + NPT 计费 + reputation 查询在 alpha.4 → alpha.5。|
| 2 | [`nps-registry`](./nps-registry/) | `:17436` | Phase 1 骨架 —— NDP `Resolve` / `Graph` / `Announce` 全部返回 `NDP-REGISTRY-UNAVAILABLE`，方便消费者预先接线 + 优雅降级。SQLite 实仓在 alpha.4。|

每个 daemon 在自己的子目录里有独立的 `Dockerfile` / `docker-compose.yml` /
README —— 共享发布节奏、共享基础镜像，但独立构建独立发版。

### 不在本仓的部分

NPS 三层中的**信任锚 / 云**层在 GitHub `innolotus` 组织下两个私有仓里，
跟 NPS Cloud 一起发（2027 Q1+）：

- `innolotus/nps-cloud-ca` —— 跨组织 NID 证书颁发机构 + CRL/OCSP。
- `innolotus/nps-ledger` —— 实现 [NPS-RFC-0004](https://gitee.com/labacacia/NPS-Release/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md)
  的 Certificate-Transparency 风格 NID 声誉日志。

**今天**就要自托管 CA 的话用 [`labacacia/nip-ca-server`](https://gitee.com/labacacia/nip-ca-server)
—— 单组织 OSS CA。

---

## 快速开始（4 个 daemon 一起拉起）

```bash
git clone https://gitee.com/labacacia/nps-daemons.git
cd nps-daemons
docker compose up -d

# 各 daemon 的 /health 在各自端口
curl -s http://localhost:17433/health   # npsd
curl -s http://localhost:8080/health    # nps-gateway
curl -s http://localhost:17436/health   # nps-registry

docker compose logs -f nps-runner       # nps-runner 无 HTTP 接口
```

或者只拉一个：

```bash
docker compose up -d npsd
```

## 快速开始（单 daemon，不用 compose）

每个子目录都有自包含的 Dockerfile：

```bash
cd npsd
docker build -t labacacia/npsd:1.0.0-alpha.3 .
docker run --rm -p 17433:17433 -v npsd-data:/data labacacia/npsd:1.0.0-alpha.3
```

源码构建也行（需要 .NET 10 SDK）：

```bash
cd npsd
dotnet restore
dotnet run
```

所有 daemon 依赖 nuget.org 上发布的 `LabAcacia.NPS.*` NuGet 包
（`Core`、`NIP`、`NDP`、`NWP`、`NWP.Anchor`、`NOP`），不依赖 monorepo。

## 架构

完整的三层参考拓扑（含 2 个私有的信任锚 daemon）见
[`docs/architecture.cn.md`](./docs/architecture.cn.md)。简版：

```
┌─────────────────────────────────────────────────────────┐
│ Layer 3（私有 —— innolotus org，NPS Cloud 2027 Q1+）    │
│   nps-cloud-ca · nps-ledger                             │
├─────────────────────────────────────────────────────────┤
│ Layer 2（本仓库）—— 网络入口                            │
│   nps-gateway（公网 ingress）· nps-registry（NDP）       │
├─────────────────────────────────────────────────────────┤
│ Layer 1（本仓库）—— 主机本地                            │
│   npsd（状态宿主，:17433）· nps-runner（FaaS）           │
└─────────────────────────────────────────────────────────┘
```

## 规范 & SDK 参考

- [NPS-Release](https://gitee.com/labacacia/NPS-Release) —— 协议规范。
- [NPS-Node Profile](https://gitee.com/labacacia/NPS-Release/blob/main/spec/services/NPS-Node-Profile.cn.md) —— `npsd` 对照构建的合规规范。
- [NPS-Node-L1 合规](https://gitee.com/labacacia/NPS-Release/blob/main/spec/services/conformance/NPS-Node-L1.cn.md) —— 21 个 `TC-N1-*` 用例。
- [NPS-sdk-dotnet](https://gitee.com/labacacia/NPS-sdk-dotnet) —— daemon 依赖的 .NET SDK。
- [labacacia/nip-ca-server](https://gitee.com/labacacia/nip-ca-server) —— 当下用于真签发的单组织 OSS CA。

## 版本

跟随 NPS 套件统一 SemVer。NPS 1.0 之前所有组件用同一 `1.0.0-alpha.x` tag。
逐 daemon 版本说明见各子目录的 `CHANGELOG.cn.md`；汇总版本说明在仓库
根目录 [`CHANGELOG.cn.md`](./CHANGELOG.cn.md)。

## 许可证

Apache License 2.0，见 [`LICENSE`](./LICENSE) 和 [`NOTICE`](./NOTICE)。

版权所有 © 2026 LabAcacia（INNO LOTUS PTY LTD）。
