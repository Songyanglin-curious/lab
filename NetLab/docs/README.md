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

**配套 C# 基础：** 如果对委托/异步/Stream 不熟，可查阅 [docs/csharp-concepts/](docs/csharp-concepts/)。

---

### Phase 1 — 帧协议 + ASDU/APDU 分离（已完成）

TCP 是流，没有消息边界。引入帧头 `0xEB 0x90` + 变长长度来解决粘包，再拆出 ASDU（消息体）/ APDU（传输帧）两层。

| 序号 | 内容 | 类型 | 路径 |
|------|------|------|------|
| 1.1 | 帧协议 — EB90 + 变长长度编码 | 文档 | [docs/tutorials/01-frame-protocol.md](docs/tutorials/01-frame-protocol.md) |
| 1.2 | ASDU/APDU 分离 — 发送侧消息封装入门 | 文档 | [docs/tutorials/02-asdu-apdu.md](docs/tutorials/02-asdu-apdu.md) |

**对应 RemoteOps：** `APDU.cs`、`BinaryASDU`、`JsonASDU`、`Helper/Net.LengthToBytes()`

---

### Phase 2 — IExchange 接口 + 委托注入（预计下一步）

APDU 不再直接依赖 `NetworkStream`，用 `IExchange` 接口抽象掉底层传输方式。通过委托注入实现明文/加密透明切换。

- IExchange 接口定义（Send / Read / Close / IsConnected）
- 委托注入作为策略替换（Net.CreateClient）
- TCPClient 实现 IExchange

**对应 RemoteOps：** `IExchange.cs`、`TCPClient.cs`、`Net.cs::CreateClient` 委托

---

### Phase 3 — 连接池（预计后续）

连接复用，不每次新建 TCP。

- ConcurrentDictionary 连接池
- Extend\<T\> 包装（IsUsing 标记）
- UseClient / InternalSend 调用模式

**对应 RemoteOps：** `Net.cs::UseClient/InternalSend`

---

### Phase 4 — 多业务分发（预计后续）

一个端口处理 Manage/File/Agent 三种业务。协商帧 + Business 枚举 + IChannel 接口 + Server 模板方法。

**对应 RemoteOps：** `Server.cs`、`EquipChannel.cs`、`ChannelManageProcess.cs`

---

### Phase 5 — TLCP 加密（预计后续）

站端之间走公网必须加密。IExchange 接口让 TLCP 加密替换不影响业务代码。

**对应 RemoteOps：** `TLCPDataExchange/`、`Ysh.Tlcp/`

---

### Phase 6 — 对照生产代码（预计最后）

逐模块对比 NetLab 实验代码和 RemoteOps 生产代码，标注差异点和已知缺陷。综合练习修改协议。

---

## 使用方式

1. **按 Phase 顺序学习**，每个 Phase 先读文档再动手写代码
2. 每个实验项目都有练习任务，**不要只看不写**
3. 遇到不懂的 C# 概念去 `docs/csharp-concepts/` 查
4. 每个 Phase 学完后对照 RemoteOps 生产代码看一遍（文件路径在各 Phase 说明中）

## 已有项目

| 项目 | 说明 |
|------|------|
| `Server/` | Echo 服务端 + 原始单连接服务端 |
| `Client/` | Echo 客户端 + 原始单连接客户端 |
| `异步/` | async/await 演示 |
| `NetLab/` | Hello World 空壳 |
