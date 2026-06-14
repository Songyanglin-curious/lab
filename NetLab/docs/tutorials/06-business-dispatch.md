# Phase 4 — 业务协商与 Channel 分发

> 目标：同端口接收不同业务连接，通过首帧协商分发到对应 Channel。
>
> ⚠️ 关键前提：**连接池只服务于 Net.Send（内部 Equip 通道），Net.Connect 不走池。**

---

## 0. 重要前提：连接池 ≠ 全局通用

在 RemoteOps 和 NetLab 中，连接按用途分为两条路径：

| | 路径 A：内部服务通道 | 路径 B：外部业务通道 |
|---|---|---|
| NetLab | `Net.Send` → `ConnectionPool` | `Net.Connect` → 不走池 |
| RemoteOps | `InternalSend` → `UseClient` | `BaseMainServer.Connect()` |
| 协商字节 | `Business.Equip`（固定 0x04） | 调用方传入（Manage/File/Agent） |
| 是否走池 | **走池** | **不走池** |
| 连接模式 | 短请求-响应，用完归还 | 长连接/按需创建，专线专用 |
| 示例场景 | ChannelServer ↔ AgentServer 内部通信 | MainServer → ChannelServer 管理/文件/代理 |

本章讲的是路径 B——同端口多业务连接分发。

---

## 1. 问题引入

当前服务端一条连接只做一件事（echo）。但 RemoteOps 里 ChannelServer 的同一端口（9904）注册了三种业务处理器：

```
同一个端口 9904
  ├── Business.Manage → ChannelManageProcess（通道状态查询、启停控制）
  ├── Business.File   → EquipFileProcess（文件下发）
  └── Business.Agent  → EquipBaseAgentProcess（代理隧道）
```

问题：怎么让服务端知道"这个连接是来干嘛的"？

---

## 2. 方案：首帧协商

连接建立后，客户端第一帧就是一个协商帧——一个字节，表明业务身份。

```
客户端                               服务端
  │                                    │
  │── TCP 连接 ──────────────────────→│
  │                                    │
  │── 协商帧 [0x01] ────────────────→│  读到 0x01 → Business.Manage
  │                                    │  GetChannel(Manage) → EchoChannel
  │←─ 协商确认 [0x00] ────────────────│  ResponseNegotiate()
  │                                    │
  │══ 之后整条连接都由 EchoChannel 处理 ══│
```

关键特性：

```
一条连接 = 一个 Business = 一个 Channel
连接建立时确定，之后不可更改。
```

---

## 3. 核心类

### Business 枚举

```csharp
public enum Business : byte
{
    Manage = 1,   // 管理业务
    File = 2,     // 文件下发
    Agent = 3,    // 远程代理
    Equip = 4     // 装置内部通信（走池）
}
```

### IChannel 接口

```csharp
public interface IChannel : IDisposable
{
    void ResponseNegotiate(IExchange exchange);  // 回协商确认
    void Run(IExchange exchange);                 // 业务主循环
}
```

### BusinessServer — 服务端分发

```csharp
public class BusinessServer
{
    private Dictionary<Business, Func<IChannel>> _factories = new();

    public void RegisterProcess(Business biz, Func<IChannel> factory)
    {
        _factories[biz] = factory;
    }

    public void Start()
    {
        // 监听 → Accept → 包成 StreamExchange → StartupChannel
    }

    private void StartupChannel(IExchange exchange)
    {
        byte[]? data = FrameCodec.ReceiveFrame(exchange);  // 协商帧
        Business business = (Business)data[0];               // 取业务类型

        if (!_factories.TryGetValue(business, out var factory))
        {
            FrameCodec.SendFrame(exchange, new byte[] { 0xFF });  // 未知 → 拒绝
            exchange.Close();
            return;
        }

        IChannel channel = factory();
        channel.ResponseNegotiate(exchange);   // 回 [0x00]
        channel.Run(exchange);                 // 进入业务循环
    }
}
```

---

## 4. 客户端两种连接方式

### 方式一：Net.Connect（外部业务，不走池）

```csharp
// 每条连接指定一种 Business，专属专用
using IExchange? conn = Net.Connect("127.0.0.1", 9000, Business.Manage);
FrameCodec.SendFrame(conn, msg.GetContent());
byte[]? resp = FrameCodec.ReceiveFrame(conn);
```

`Net.Connect` 内部：
- 创建连接 → 发协商帧 `[business_byte]` → 收协商回应 `[0x00]`
- **不走连接池**，每次调用都建新连接
- 对应 RemoteOps `BaseMainServer.Connect()`

### 方式二：Net.Send（内部 Equip，走池）

```csharp
// 走连接池，协商固定 Equip
byte[]? resp = Net.Send("127.0.0.1", 9000, asdu);
```

`Net.Send` 内部：
- `ConnectionPool.UseClient` → 协商 `Business.Equip`
- 对应 RemoteOps `InternalSend → UseClient`

---

## 5. 协商协议

```
协商帧：
  [EB90][01][business_byte]     ← APDU 包装的单字节

协商响应：
  [EB90][01][0x00]              ← 成功
  [EB90][01][0xFF]              ← 未知业务 → 服务端关连接
```

---

## 6. 三种 Channel 实现

| Channel | 注册的 Business | 行为 | 收发模式 |
|---------|----------------|------|---------|
| `EchoChannel` | `Manage` | 原 EchoServer 回显 | 持续收发 |
| `FileChannel` | `File` | 分块传输模拟（3块→完→关） | 持续收发直到传输完成 |
| `AgentChannel` | `Agent` | 持续收消息直到断开 | 持续收发 |

**FileChannel 为什么是"持续收发直到传输完成"？**

Real RemoteOps 文件传输是一个分块请求-应答循环：

```
客户端：发 GetFileASDU（请求第 N 块）
服务端：回 BinaryContentResponseASDU（含 N 字节文件数据）
客户端：写磁盘 → 剩余 > 0 → 发下一块请求
        → 剩余 = 0 → 发 Finished → 服务端关连接
```

核心特征：
- **收发模式**：和 Manage/Agent 一样是 `while(true)` 持续循环
- **关闭时机**：文件传完主动关闭（不像 Manage 和 Agent 一直保持长连接）
- **一个连接一个文件**：每次文件传输新建一次连接，和池化的 Equip 完全不同

这解释了 RemoteOps 为什么 `BaseMainServer.Connect(Business.File)` 不走连接池——每传一个文件建一次连接，传完即关，没有复用价值。

---

## 7. 动手练习

### 练习 1：跑分发演示

```powershell
dotnet run --project Server   # BusinessServer 启动，注册 3 种 Channel
dotnet run --project Client   # EchoClient.RunDispatchTest() 建 3 条不同连接
```

观察服务端日志：每条连接被分发给不同的 Channel。

### 练习 2：发错误的协商字节

修改客户端协商字节为 `0x05`（未注册的 Business），观察服务端回 `[0xFF]` 并关闭连接。

### 练习 3：同时跑 Net.Send 和 Net.Connect

```csharp
// Net.Connect → 外部业务，不回收到回声（因为服务端没有注册 Equip Channel）
// Net.Send → 内部 Equip，连接池创建，协商 Equip
```

观察：`Net.Connect(Business.Manage)` 协商成功，`Net.Send` 发的 Equip 协商因服务端未注册 Equip Channel 被拒绝。

### 练习 4：给 FileChannel 加真正的文件传输逻辑

当前 FileChannel 是桩，试着自己实现：收文件名 → 读本地文件 → 回传文件内容。

---

## 8. 对照 RemoteOps

| NetLab | RemoteOps | 位置 |
|--------|----------|------|
| `Business` | `Business` | `Protocol/State.cs:12-18` |
| `IChannel` | `IChannel` | `Exchange/IChannel.cs` |
| `BusinessServer.StartupChannel` | `Server.StartupChannel` | `Exchange/Server.cs:22-41` |
| `RegisterProcess` | `RegisterProcess` | `Exchange/Server.cs` |
| `EchoChannel` | `ChannelManageProcess` / `EquipManagerProcess` | 各种 Process 类 |
| `Net.Connect` | `BaseMainServer.Connect()` | `BaseMainServer.cs:163-186` |
| `Net.Send` | `Net.InternalSend` | `Net.cs:164-212` |

---

## Phase 4 总结

```
同端口(BusinessServer) → 首帧协商(Business枚举) → Channel分发(IChannel)

两条路径：
  Net.Connect(ip, port, business) → 外部业务 → 不走池 → 各 Channel
  Net.Send(ip, port, asdu)        → 内部 Equip → 走池   → ConnectionPool
```
