# Phase 6 — NetLab ↔ RemoteOps 全对照

> 目标：把 NetLab 每个概念一一对应到 RemoteOps 生产代码中，标注差异点和已知设计缺陷。

---

## 0. 网络基础

| NetLab 教学 | RemoteOps 生产 | 位置 |
|-------------|---------------|------|
| `new TcpListener(IP, port)` | `new TcpListener(port)` | `EquipTCPServer.cs` |
| `new TcpClient("127.0.0.1", 9000)` | `Net.CreateClient(ip, port)` 委托内部 | `Net.cs:111` |
| `AcceptTcpClient()` 循环 | `AcceptCallback` 异步 Accept | `EquipTCPServer.cs` |
| `NetworkStream` 直接操作 | `TCPClient` 包装 NetworkStream | `TCPClient.cs:50` |

RemoteOps 端口分配（`SystemConfig.cs`）：

| 端口 | 服务 |
|------|------|
| 9101 | ChannelServer |
| 9102 | ManageServer |
| 9103 | AgentServer |
| 9104 | OperateServer |
| 9105 | MonitorServer |

---

## 1. 帧协议

### 帧结构

```
NetLab:  [0xEB 0x90] [变长长度] [载荷]
RemoteOps: 同上，完全一致
```

### 逐行对照

| NetLab | RemoteOps | 文件:行号 | 差异 |
|--------|----------|----------|------|
| `FrameCodec.SendFrame(stream, bytes)` | `APDU.WriteTo(IExchange sender)` | `APDU.cs:19-28` | 教学拼整帧一次 Send；生产分 3 次 Send（帧头/长度/载荷），用 IExchange 接口 |
| `FrameCodec.ReceiveFrame(stream)` | `APDU.Read(IExchange receiver)` | `APDU.cs:30-78` | 核心逻辑一致：读 2 字节 → 逐字节读变长长度 → 读载荷 |
| `FrameCodec.LengthToBytes()` | `Helper.Net.LengthToBytes()` | `Net.cs:24-42` | 完全一致 |
| `FrameCodec.BytesToLength()` | `Helper.Net.BytesToLength()` | `Net.cs:43-59` | 完全一致 |
| `FrameCodec.ReadExactly` 循环读 | `TCPClient.Read(long count)` 内部循环 | `TCPClient.cs:44-65` | 教学在 FrameCodec 里循环；生产在 TCPClient 包装层循环 |

### 差异

RemoteOps `APDU.Read` 读变长长度时用 `receiver.Read(1)` 逐字节读，每次 Read 返回 `byte[]`。教学版用 `exchange.ReadExactly(1)` 一步到位。

---

## 2. ASDU/APDU 分离

| NetLab | RemoteOps | 文件:行号 | 差异 |
|--------|----------|----------|------|
| `IAsdu` | `IASDU` | `ASDU.cs:9` | 命名风格不同（IAsdu vs IASDU） |
| `BinaryAsdu` | `BinaryASDU` | `ASDU.cs` | 教学持有 byte[]；生产构造时接收 byte 并做 length 检查 |
| `JsonAsdu` | `JsonASDU` (abstract) | `ASDU.cs` | 教学只有基础 JSON；生产有完整的 JSON 序列化引擎 |
| — | `ManageRequestASDU` | `ManageRequestASDU.cs` | 生产有 10+ 种派生 ASDU（Manage/Response/HeartBeat/Negotiate...） |

### 差异

RemoteOps 的 ASDU 继承树比教学版丰富得多：

```
RemoteOps IASDU 继承树：
├── BinaryASDU       ← 协商帧（1字节）
├── JsonASDU (抽象)   ← JSON 序列化基类
│   ├── JsonDataASDU
│   ├── HeartBeatASDU
│   ├── ManageRequestASDU   ← {"Head":{"FunName":"...","Mid":"..."},"Data":{...}}
│   ├── ManageResponseASDU
│   ├── Negotiate 系列
│   └── Response 系列
└── ...
```

NetLab 教学只保留了最核心的三层：接口 → Binary → JSON。

---

## 3. IExchange + 委托注入

| NetLab | RemoteOps | 文件:行号 | 差异 |
|--------|----------|----------|------|
| `IExchange` 接口 | `IExchange` | `IExchange.cs:9` | 教学返回 `byte[]?`，生产返回 `byte[]`（null 表示断开） |
| `StreamExchange` | `TCPClient` | `TCPClient.cs` | **命名差异**：教学叫 StreamExchange（强调流包装），生产叫 TCPClient（容易误解） |
| `Net.CreateExchangeFunc` 委托 | `Net.CreateClientFunc` | `Net.cs:111` | 同名模式 |
| `Net.CreateExchange` 字段 | `Net.CreateClient` | `Net.cs:113` | 教学叫 CreateExchange，生产叫 CreateClient |
| `Net.Send` | `Net.InternalSend` | `Net.cs:164` | 教学 Send 内部调池→收发→归还 |

### 差异

RemoteOps `TCPClient` 名字容易误解——它不是 TcpClient 本身，而是包装了 TcpClient 的 IExchange 实现。NetLab 命名为 `StreamExchange` 更准确。

RemoteOps `Net.InternalSend` 同时包含连接池逻辑（`UseClient`），教学拆成两步：

```
RemoteOps Net.InternalSend:
  UseClient(ip, port)           ← 连接池（Phase 3 独立讲）
  → CreateClient 委托创建        ← 委托注入（Phase 2.2 独立讲）
  → Net.Send(client, asdu)      ← 帧协议（Phase 1 已覆盖）
  → APDU.Read(client)           ← 帧协议

NetLab 教学:
  Net.Send → _pool.UseClient → 委托创建 → FrameCodec.SendFrame → FrameCodec.ReceiveFrame
  各层独立，各有各的文件。
```

---

## 4. 连接池

| NetLab | RemoteOps | 文件:行号 | 差异 |
|--------|----------|----------|------|
| `ExchangeLease` | `Extend<IExchange>` | `Extend.cs:9-36` | 命名不同；教学 public，生产 internal |
| `PoolBucket` | （无独立类） | — | 教学拆出 PoolBucket 明确锁粒度；生产内联 List 在 Net.cs |
| `ConnectionPool` | `dictClient` 静态字段 | `Net.cs:115` | 教学独立类；生产静态 ConcurrentDictionary 直写在 Net.cs |
| `UseClient` 逻辑 | `UseClient` | `Net.cs:117-162` | 逐行对应：反向遍历→找空闲→IsConnected 检查→新建→发协商帧→入池 |
| `ExchangeLease.Dispose` | `Extend<T>.Dispose` | `Extend.cs:32-35` | 都是 `isUsing = false`，不关连接 |

### 差异

RemoteOps `UseClient` 里硬编码发送 `Business.Equip` 协商帧（第 144 行），教学版 ConnectionPool 不对协商帧做假设，协商由 `Net.Send` 外层处理。

RemoteOps 连接池无上限、无空闲超时、无连接数限制——已知设计缺陷。

---

## 5. 业务协商与 Channel 分发

| NetLab | RemoteOps | 文件:行号 | 差异 |
|--------|----------|----------|------|
| `Business` 枚举 | `Business` | `State.cs:12-18` | 值完全一致：Manage=1 / File=2 / Agent=3 / Equip=4 |
| `IChannel` | `IChannel` | `IChannel.cs` | 接口方法不同：教学 `Run` + `ResponseNegotiate`；生产多一个 `DataReceived` |
| `BusinessServer` | `Server` (abstract) | `Server.cs:22-41` | 教学一个类搞定；生产 Server 抽象 + EquipTCPServer 子类 |
| `StartupChannel` | `StartupChannel` | `Server.cs:22-41` | 逻辑逐行对应：读协商→GetChannel→ResponseNegotiate→Run |
| `RegisterProcess` | `RegisterProcess` | Server 基类 | 完全一致 |
| `Net.Connect(ip, port, biz)` | `BaseMainServer.Connect()` | `BaseMainServer.cs:163-186` | 教学更简洁；生产有额外的日志/心跳逻辑 |

### 差异

RemoteOps `EquipChannel.Run()` 里用 `Task.Run(() => DataReceived(data))` 异步处理每帧，教学版 `Run` 里同步处理。

RemoteOps `UseClient` 里的协商帧硬编码 `Business.Equip`——所有走池的连接统一协商为"内部服务通信"，不如按目标端口区分灵活。

---

## 6. 加密

| NetLab 教学模拟 | RemoteOps 真实实现 | 层 |
|-----------------|-------------------|-----|
| `SecureExchange` | `TLCPDataExchange.TLCPClient` | IExchange 装饰器 |
| XOR 异或 | SM4-CBC 分组加密 + SM3-HMAC 完整性校验 | 加解密算法 |
| 无握手 | 13 步 SM2 签名/加密握手 | 密钥协商 |
| `SecureExchange.Dispose()` | `TLCPClient.Dispose()` + 发送 CloseNotify | 安全关闭 |
| 一行委托切换 | 一行委托切换 | 机制完全一致 |

### RemoteOps TLCP 真实层次

```
Web / MainServer
  ↓
Net.CreateClient 委托
  ↓
TLCPDataExchange.TLCPClient  ← IExchange 实现
  ↓
Ysh.Tlcp.TlcpClient.Send / Receive
  ↓
P/Invoke → gmssl.dll
  ↓
TCP (TcpClient + NetworkStream)
```

### 差异

| 维度 | 教学 SecureExchange | 生产 TLCPClient |
|------|-------------------|-----------------|
| 加密 | XOR（无安全性） | SM4-CBC（128位分组加密） |
| 完整性 | 无 | SM3-HMAC（消息认证） |
| 握手 | 无 | SM2 双证书体系（签名+加密） |
| 库 | 纯 C# 3 行 | gmssl.dll 原生库 + P/Invoke |
| 线程安全 | 无 | ConcurrentPtrCounter 序列号管理 |

---

## 7. RemoteOps 已知设计缺陷

### 7.1 `Net.UseClient` 硬编码 `Business.Equip`

```csharp
// Net.cs:144 — 所有走池的连接统一协商 Equip
Helper.Net.Send(client, new BinaryASDU((byte)Business.Equip));
```

不同目标端口可能需要不同 Business，但此处写死了。NetLab 的 `Net.Send` 对此不做假设。

### 7.2 `Net.cs` 一个文件混了三层

```
Helper.Net.cs 包含：
  - LengthToBytes / BytesToLength   ← 变长编码（应在 APDU/FrameCodec 层）
  - CreateClientFunc / CreateClient ← 委托注入（纯粹的策略模式）
  - dictClient + UseClient         ← 连接池（连接生命周期管理）
  - InternalSend                    ← 业务入口（使用上面三层）
```

NetLab 把它们拆成 `FrameCodec` / `Net` / `ConnectionPool` 三个文件。

### 7.3 `TCPClient` 命名误导

```
TCPClient = IExchange 实现，包装 NetworkStream
TLCPClient = IExchange 实现，包装 Ysh.Tlcp.TlcpClient
```

`TCPClient` 名中有 TCP，暗示它是 TCP 客户端——但它只是一个 IExchange 流包装器。NetLab 命名为 `StreamExchange`，名如其意。

### 7.4 `SafeExchange.Connect()` 空壳

```csharp
// SafeExchange.cs — Connect() 空方法，注释说"计划中的断线重连"
// 当前用它只会无意义代理调用
```

文件 `interfaces.cs` 不在 csproj 编译列表中，是废弃文件。

### 7.5 连接池无上限

```csharp
ConcurrentDictionary<string, List<Extend<IExchange>>> dictClient
// 无 MaxSize、无空闲超时、无连接数限制
// 高并发时可能无限创建连接
```

---

## 8. RemoteOps 中"混在一起"的概念及 NetLab 拆解

| RemoteOps 一处 | 混了什么 | NetLab 拆成 |
|---------------|---------|------------|
| `Net.cs` 一个文件 | 委托 + 连接池 + 变长编码 + 业务入口 | `Net.cs` / `ConnectionPool.cs` / `FrameCodec.cs` |
| `APDU.cs` 一个方法 `Read` | 帧头校验 + 变长解码 + 循环读载荷 | `FrameCodec.ReceiveFrame` 内部两步 |
| `TCPClient` 一个类 | IExchange 实现 + Stream 包装 + 连接状态检测 | `StreamExchange`（单一直白） |
| `Server.StartupChannel` 一个方法 | 读协商 + 查注册表 + 协商回应 + 启动 Run | 教学保持此结构，是正向案例 |

---

## 9. 动手练习

### 练习 1：文件定位

在 RemoteOps 源码中找到以下文件并读出对应行：

| 概念 | 目标文件 | 目标行 |
|------|---------|--------|
| 变长编码 `LengthToBytes` | `Helper/Net.cs` | ~24 |
| 连接池 `UseClient` | `Helper/Net.cs` | ~117 |
| 帧发送 `APDU.WriteTo` | `Protocol/APDU.cs` | ~19 |
| 帧接收 `APDU.Read` | `Protocol/APDU.cs` | ~30 |
| 服务端分发 `Server.StartupChannel` | `Exchange/Server.cs` | ~22 |
| 外部业务连接 `BaseMainServer.Connect` | `MainDataExchange/BaseMainServer.cs` | ~163 |

### 练习 2：拆解练习

假设你要给 RemoteOps 加一个新的 Business 类型（比如 `Business.Config`），在 NetLab 里需要改几个文件？在 RemoteOps 里需要改几个文件？为什么 NetLab 改得更少？

### 练习 3：自己修一个缺陷

给 `ConnectionPool` 加上 `MaxConnections` 上限（在 `UseClient` 里如果池满了就复用阻塞等待的那个，或抛异常）。这是 RemoteOps 缺失的功能。

---

## Phase 1~6 学习路径总览

```
Phase 0: TCP 网络基础            → 知道 TCP 无消息边界
Phase 1: 帧协议 + ASDU/APDU   → 有了消息边界，业务与传输解耦
Phase 2: IExchange + 委托注入  → 不依赖具体传输方式，明文/加密可切换
Phase 3: 连接池                → 连接复用（仅 Equip 内部通道）
Phase 4: 业务协商与 Channel    → 同端口多业务分发
Phase 5: 加密通道透明替换        → IExchange 装饰器模式兑现
Phase 6: 对照生产代码            ← 当前
```

从零到能看懂并修改 RemoteOps 的通信协议——六个阶段，每个概念都有代码可跑、有生产代码可比。
