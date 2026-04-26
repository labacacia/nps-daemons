[English Version](./README.md) | 中文版

# `nps-registry` —— NPS Daemon（第二层，跨机 NDP 发现）

> 跨机 NDP 发现注册中心的参考实现。中心节点，响应 NDP `Resolve` / `Graph`
> 查询，汇总多机注册信息。每机 [`npsd`](../npsd/) 只知道本机 session；
> 跨机查询走这里。完整六-daemon 拓扑见
> [`docs/daemons/architecture.cn.md`](../docs/architecture.cn.md)。

## 状态 —— alpha.3

**Phase 1 骨架。** 监听 NDP 可选独立端口 `17436`（详见 [NPS-4 §1](https://gitee.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.cn.md)）
+ `/health` 端点。`Resolve`、`Graph`、`Announce` URL 已就位但返回
`NDP-REGISTRY-UNAVAILABLE`（HTTP 503），让消费方可以提前对该 daemon
做集成 + graceful fallback。SQLite 后端真实注册存储 + 跨机 federation
在 alpha.4 → alpha.5 落地。

## 快速上手

```bash
NPSREGISTRY_PORT=17436 dotnet run --project tools/daemons/nps-registry/NpsRegistry.csproj
curl -s http://localhost:17436/health        | jq
curl -s -i http://localhost:17436/v1/resolve  # → 503 NDP-REGISTRY-UNAVAILABLE
```

### Docker

```bash
docker build -f tools/daemons/nps-registry/Dockerfile -t labacacia/nps-registry:1.0.0-alpha.3 .
docker run --rm -p 17436:17436 labacacia/nps-registry:1.0.0-alpha.3
```

## 配置（环境变量）

| 变量 | 默认值 | 用途 |
|-----|------|-----|
| `NPSREGISTRY_PORT` | `17436` | bind TCP 端口。NDP 可选独立端口，见 [NPS-4](https://gitee.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.cn.md)。|
| `NPSREGISTRY_HOST` | `0.0.0.0` | bind 地址。registry 故意面向网络（与 `npsd` 默认 loopback 不同）。|

## 规范引用

- [NPS-4 NDP](https://gitee.com/labacacia/NPS-Release/blob/main/spec/NPS-4-NDP.cn.md) —— 本 daemon 实现的注册中心面所属的发现协议。
- [Daemon 架构 §④](../docs/architecture.cn.md#-nps-registry--discovery-注册中心-l2-阶段可选托管) —— 为什么 registry 独立 daemon 而不是 `npsd` 的一部分。

## 许可证

Apache-2.0 —— 见仓库根 `LICENSE`。
