[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— NPS Daemons（套装）

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

在 NPS 达到 v1.0 稳定版之前，套件内所有仓库同步使用同一个预发布版本号。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 新增

- 首次作为独立仓库
  [`labacacia/nps-daemons`](https://github.com/labacacia/nps-daemons)
  ([Gitee 镜像](https://gitee.com/labacacia/nps-daemons)) 发布。截至
  v1.0.0-alpha.2，这些 daemon 还不存在；alpha.3 在 dev 仓库引入作为
  套件部署面，本次首发即拆出独立仓。
- **`npsd`**（Layer 1，主机本地 NCP 接入 + 状态宿主）—— L1 最小集：
  绑定 `127.0.0.1:17433`，首次启动生成 root Ed25519 keypair（PKCS#8，
  POSIX 文件权限 `0600` —— 满足 NPS-Node Profile L1 合规用例
  `TC-N1-NIP-01`），暴露 `GET /health` 和 daemon 自身的 `GET /.nwm`。
- **`nps-runner`**（Layer 1，任务调度 / FaaS runtime）—— Phase 1
  骨架：Generic Host 脚手架 + 30 秒心跳。Inbox 监听 + `spawn_spec_ref`
  解析 + worker 生命周期在 L3 阶段（alpha.5+）。
- **`nps-gateway`**（Layer 2，公网 Internet ingress）—— Phase 1 骨架：
  `:8080` HTTP 监听 + `/health`，文档化里程碑。TLS 卸载、rate-limit、
  NeuronHub 鉴权、NPT 计费、NPS-RFC-0004 reputation 查询、
  NPS-CR-0001 Anchor Node 中间件接线在 alpha.4 → alpha.5。
- **`nps-registry`**（Layer 2，跨机 NDP 发现）—— Phase 1 骨架：
  在 NDP 可选专用端口 `17436` 监听；`Resolve` / `Graph` / `Announce`
  端点全部返回 `NDP-REGISTRY-UNAVAILABLE`（HTTP 503），方便消费者
  预先接线 + 优雅降级。SQLite 实仓在 alpha.4。
- 仓库 README（EN + CN）、顶层 `docker-compose.yml` 一键拉起 4 个
  daemon、仓库级 `LICENSE` + `NOTICE`、`docs/architecture.{md,cn.md}`
  完整三层拓扑文档（含 `innolotus/nps-cloud-ca` 和
  `innolotus/nps-ledger` 两个私有信任锚 daemon）。

### 变更

- 4 个 daemon 的 `*.csproj` 全部从仓内 `<ProjectReference>` 切换到
  nuget.org 上发布的
  [`LabAcacia.NPS.*`](https://www.nuget.org/profiles/LabAcacia/) NuGet
  包（v1.0.0-alpha.3）。发布仓现在脱离 monorepo 也能独立构建。
- Dockerfile 构建上下文从 monorepo 相对路径（`../../..`）改为仓库根，
  从发布仓直接进 daemon 子目录 `docker build .` 即可。

### 跟随套件的协议变更

本次随套件 `v1.0.0-alpha.3` 同步：

- **RFC-0001** —— NCP 连接前导（`npsd` 自身的 preamble runtime 在
  alpha.4 —— Phase 1 仅库层）。
- **RFC-0003** —— Agent 身份保证等级（涉及 NIP，以及 `npsd` 自身
  `/.nwm` 的 `min_assurance_level` 字段）。
- **RFC-0004** —— NID 声誉日志（Phase 1，仅 entry shape；in-memory
  实现在 `innolotus/nps-ledger` 而非本仓）。
- **CR-0001** —— Anchor / Bridge Node 拆分（`nps-gateway` 改用新的
  `LabAcacia.NPS.NWP.Anchor` 包）。

完整套件级汇总见
[`NPS-Release/CHANGELOG.cn.md`](https://gitee.com/labacacia/NPS-Release/blob/main/CHANGELOG.cn.md)。

---

[1.0.0-alpha.3]: https://gitee.com/labacacia/nps-daemons/releases/tag/v1.0.0-alpha.3
