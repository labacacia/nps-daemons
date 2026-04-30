[English Version](./README.md) | 中文版

# `nps-runner` —— NPS Daemon（第一层，本机任务调度）

> 本机 FaaS runtime 的参考实现。监控 [`npsd`](../npsd/) inbox 中给
> `ephemeral` 模式 NIDs 的消息，根据 NID 的 `spawn_spec_ref`（NDP §3.1）
> 拉起对应 worker 子进程。完整六-daemon 拓扑见
> [`docs/daemons/architecture.cn.md`](../docs/architecture.cn.md)。

## 状态 —— alpha.4

**Phase 1 骨架。** Generic Host 脚手架已就绪；二进制可构建、启动，按 30
秒间隔打 heartbeat 日志。实际的 inbox 监视 + spawn-spec 解析 + worker
生命周期管理在 L3 阶段（alpha.5+）落地，详见
[daemon phasing 表](../docs/architecture.cn.md#alpha--beta-阶段化)。

骨架自 alpha.3 起存在，目的是让部署面（进程名、NuGet 包 id、Docker
image tag、systemd unit 名）从 daemon 生态的起点就稳定下来，而不是后期
再插入。

## 快速上手

```bash
dotnet run --project tools/daemons/nps-runner/NpsRunner.csproj
# → "nps-runner v1.0.0-alpha.4 starting (Phase 1 skeleton — ...)"
# → "nps-runner heartbeat — Phase 1 skeleton, no work to do yet"（每 30 秒）
```

### Docker

```bash
docker build -f tools/daemons/nps-runner/Dockerfile -t labacacia/nps-runner:1.0.0-alpha.4 .
docker run --rm labacacia/nps-runner:1.0.0-alpha.4
```

## 为什么独立 daemon（而不是 `npsd` 的一部分）

详见 [架构 §1](../docs/architecture.cn.md#-nps-runner--任务调度器--faas-runtime-l3-阶段)：
协议层与 worker 调度器在资源画像、故障隔离、信任边界三方面差异巨大 ——
worker crash 不该把 NCP 层带下去；调度器要执行用户提供的 Agent SDK 代码，
协议层不该有这个权限面。

## 许可证

Apache-2.0 —— 见仓库根 `LICENSE`。
