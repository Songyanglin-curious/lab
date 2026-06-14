# 从零重写通信框架

## 目标

一步步重建 YCYWDataExchange + TLCPDataExchange 的核心抽象，理解每一步"为什么这样设计"。最终能看懂并修改 RemoteOps 的通信协议代码。

## 学习路线

### Phase 0 — 网络基础夯实

理解 TCP 通信的基本概念，亲手写出 echo 程序，**直观看到粘包现象**。

| 序号 | 内容 | 类型 | 路径 |
|------|------|------|------|
| 0.1 | 网络是什么 — IP、端口、客户端/服务端 | 文档 | [docs/network-basics/01-network-model.md](docs/network-basics/01-network-model.md) |
| 0.2 | TCP 协议 — 字节流、粘包/拆包 | 文档 | [docs/network-basics/02-tcp-protocol.md](docs/network-basics/02-tcp-protocol.md) |
| 0.3 | .NET 中的 TCP — TcpListener/TcpClient/NetworkStream | 文档 | [docs/network-basics/03-dotnet-tcp.md](docs/network-basics/03-dotnet-tcp.md) |
| 0.4 | 动手练习 — Echo 程序 & 粘包观察 | 文档 | [docs/network-basics/04-echo-lab.md](docs/network-basics/04-echo-lab.md) |
| Lab | Echo 服务端/客户端（粘包演示） | 项目 | `Server/` / `Client/` |

**配套 C# 基础：** [docs/csharp-concepts/](docs/csharp-concepts/)

---

### Phase 1 — 帧协议 + ASDU/APDU 分离（已完成）

| 序号 | 内容 | 文档 |
|------|------|------|
| 1.1 | 帧协议 — EB90 + 变长长度编码 | [docs/tutorials/01-frame-protocol.md](docs/tutorials/01-frame-protocol.md) |
| 1.2 | ASDU/APDU 分离 — 发送侧消息封装入门 | [docs/tutorials/02-asdu-apdu.md](docs/tutorials/02-asdu-apdu.md) |

**代码：** `Protocol/FrameCodec.cs`、`Protocol/Messages/`

---

### Phase 2 — IExchange 接口 + 委托注入（当前）

业务代码不再直接依赖 `NetworkStream`，用 `IExchange` 接口抽象掉底层传输。通过委托注入实现明文/加密切换。

| 序号 | 内容 | 文档 |
|------|------|------|
| 2.1 | IExchange 接口 — 传输层抽象 | [docs/tutorials/03-iexchange-interface.md](docs/tutorials/03-iexchange-interface.md) |
| 2.2 | 委托注入 — 明文/加密透明切换 | [docs/tutorials/04-delegate-injection.md](docs/tutorials/04-delegate-injection.md) |

**代码：** `Protocol/Exchange/`、`Client/Net.cs`

---

### Phase 3 — 连接池（当前）

`Net.Send` 不每次新建 TCP 连接，用连接池复用。

| 序号 | 内容 | 文档 |
|------|------|------|
| 3.1 | 连接池 — ExchangeLease + ConnectionPool | [docs/tutorials/05-connection-pool.md](docs/tutorials/05-connection-pool.md) |

**代码：** `Client/ConnectionPool.cs`、`Client/Net.cs`

**对应 RemoteOps：** `Net.cs::UseClient/InternalSend`、`Extend.cs`

---

### Phase 4 — 业务协商与 Channel 分发（当前）

同一个端口接受多种业务连接，首帧协商确定归属，分发给对应 Channel。

| 序号 | 内容 | 文档 |
|------|------|------|
| 4.1 | 业务协商与 Channel 分发 — 同端口多业务 | [docs/tutorials/06-business-dispatch.md](docs/tutorials/06-business-dispatch.md) |

**代码：** `Protocol/Business.cs`、`Protocol/IChannel.cs`、`Protocol/BusinessServer.cs`、`Protocol/Channels/`、`Client/Net.cs::Connect`

**对应 RemoteOps：** `Server.StartupChannel`、`Business` 枚举、`BaseMainServer.Connect`

---

### Phase 5 — TLCP 加密（当前）

IExchange 之上包一层加密，业务代码 / FrameCodec / Channel 一行不改。

| 序号 | 内容 | 文档 |
|------|------|------|
| 5.1 | 加密通道透明替换 — SecureExchange | [docs/tutorials/07-tlcp-encryption.md](docs/tutorials/07-tlcp-encryption.md) |

**代码：** `Protocol/Exchange/SecureExchange.cs`、`BusinessServer.ExchangeWrapper`

**对应 RemoteOps：** `TLCPDataExchange.TLCPClient`（位置类比，不等价加密实现）

---

### Phase 6 — 对照生产代码（当前）

NetLab 每个概念一一对应到 RemoteOps 源码，标注差异点和已知设计缺陷。

| 序号 | 内容 | 文档 |
|------|------|------|
| 6.1 | NetLab ↔ RemoteOps 全对照 | [docs/08-production-review.md](docs/08-production-review.md) |

---

## 项目结构

```
NetLab/
├── Protocol/                      共享库（帧协议 + 消息体 + 传输抽象）
│   ├── FrameCodec.cs
│   ├── Exchange/
│   │   ├── IExchange.cs
│   │   ├── StreamExchange.cs
│   │   ├── SecureExchange.cs       Phase 5
│   │   └── MockExchange.cs
│   ├── Channels/                    Phase 4
│   │   ├── IChannel.cs
│   │   ├── EchoChannel.cs
│   │   ├── FileChannel.cs
│   │   └── AgentChannel.cs
│   ├── Business.cs                  Phase 4
│   ├── BusinessServer.cs            Phase 4
│   └── Messages/
│       ├── IAsdu.cs
│       ├── BinaryAsdu.cs
│       ├── JsonAsdu.cs
│       └── HeartbeatAsdu.cs
├── Server/                        服务端
│   ├── EchoServer.cs              Phase 1~2 echo 服务端
│   └── SingleConnectionServer.cs  Step 0 原始版（保留）
├── Client/                        客户端
│   ├── EchoClient.cs              Phase 1~3 echo 客户端
│   ├── Net.cs                     Phase 2.2 委托 + Phase 3 池化 Send
│   ├── ConnectionPool.cs          Phase 3 连接池
│   └── SingleConnectionClient.cs  Step 0 原始版（保留）
├── 异步/                          async/await 演示
└── docs/                          所有教程文档
```

## 使用方式

1. **按 Phase 顺序学习**，先读文档再跑代码
2. 每个 Phase 都有动手练习，**不要只看不写**
3. C# 概念查 `docs/csharp-concepts/`
4. 学完对照 RemoteOps 生产代码（文件路径在各 Phase 说明中）
