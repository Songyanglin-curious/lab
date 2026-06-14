# Phase 2.2 — 委托注入：明文/加密透明切换

> 目标：通过一个静态委托，让全进程用一种统一的 IExchange 实现。为后续 TLCP 加密零改动替换打下基础。

---

## 1. 问题引入

现在有了 `IExchange`，业务代码用 `IExchange` 收发。但创建连接时还是硬编码：

```csharp
// 硬编码：创建 StreamExchange
TcpClient client = new TcpClient("127.0.0.1", 9000);
IExchange exchange = new StreamExchange(client.GetStream());
```

问题：如果将来有 20 处代码都创建连接，改加密时得改 20 处。而且每个调用方都要知道"加密是用 TLCPClient 还是 TCPClient"——这不是调用方该关心的。

---

## 2. 方案：静态委托作为全局工厂

```csharp
public static class Net
{
    // 声明委托类型
    public delegate IExchange CreateExchangeFunc(string ip, int port);

    // 全局唯一的连接工厂，启动时赋值一次
    public static CreateExchangeFunc? CreateExchange = null;

    // 业务层唯一入口：发一个 ASDU，等一个响应
    public static byte[]? Send(string ip, int port, IAsdu asdu)
    {
        using IExchange exchange = CreateExchange(ip, port);
        FrameCodec.SendFrame(exchange, asdu.GetContent());
        return FrameCodec.ReceiveFrame(exchange);
    }
}
```

### 启动时注册（一行切换）

```csharp
// 明文 TCP
Net.CreateExchange = (ip, port) =>
{
    TcpClient client = new TcpClient(ip, port);
    return new StreamExchange(client.GetStream());
};

// 将来：TLCP 加密（一行改掉，所有业务代码不动）
Net.CreateExchange = (ip, port) =>
{
    // 1. TCP 连接
    // 2. TLCP 握手
    // 3. 返回用 TlcpClient 包装的 IExchange
};
```

---

## 3. 委托注入的核心价值

```
┌──────────────────────┐
│  业务代码              │
│  Net.Send(ip,port,asdu)│  ← 只管"发消息收响应"
├──────────────────────┤
│  Net.CreateExchange   │  ← 委托，启动时赋值一次
│  (ip,port) => { ... } │
├──────────────────────┤
│  具体实现              │
│  StreamExchange /     │
│  将来: TLCPExchange   │
└──────────────────────┘
```

编译时：`Net.Send` 只依赖 `IExchange` 接口，不依赖任何具体实现。
运行时：通过 `Net.CreateExchange` 委托切换到具体实现。

RemoteOps 里正是用这套机制实现了全站透明加密。

---

## 4. 使用方式

### 方式一：长连接复用（多个请求-响应在同一连接上）

```csharp
Net.CreateExchange = (ip, port) => { ... };

using IExchange exchange = Net.CreateExchange("127.0.0.1", 9000);
for (int i = 0; i < 3; i++)
{
    FrameCodec.SendFrame(exchange, msg.GetContent());
    byte[] response = FrameCodec.ReceiveFrame(exchange);
}
```

### 方式二：短连接每次新建（一个请求-响应建一次连接）

```csharp
Net.CreateExchange = (ip, port) => { ... };

byte[]? response = Net.Send("127.0.0.1", 9000, asdu);
// 内部：CreateExchange → SendFrame → ReceiveFrame → Dispose
```

方式二的代价：每次请求都新建 TCP 连接（三次握手开销）。Phase 3（连接池）会解决这个问题。

---

## 5. 和 RemoteOps Net.cs 的对应关系

RemoteOps 的 `Helper/Net.cs` 同时包含了三件事：

```csharp
// RemoteOps Net.cs（真实代码简化）：
public static byte[] InternalSend(string ip, int port, IASDU asdu)
{
    using Extend<IExchange> ec = UseClient(ip, port);  // ← 连接池（Phase 3）
    //        ↑ UseClient 内部调 CreateClient(ip, port)  ← 委托注入（Phase 2.2）
    Net.Send(ec.Src, asdu);                             // ← 帧协议（Phase 1.1）
    byte[] rResponse = APDU.Read(ec.Src);
    return rResponse;
}
```

| NetLab 教学 | RemoteOps 生产 | 混在一起的问题 |
|------------|---------------|--------------|
| `Phase 1.1 FrameCodec` | `APDU.WriteTo/Read` | Net.cs 里还塞了变长编码 |
| `Phase 2.1 IExchange` | `IExchange.cs` | 干净 |
| `Phase 2.2 Client.Net.cs` 委托 | `Net.CreateClient 委托` | 和连接池写在同一文件同一方法里 |
| `Phase 3 连接池`（下一步） | `Net.UseClient/InternalSend` | 同上 |

---

## 6. 动手练习

### 练习 1：观察 IsConnected

在 `EchoClient.RunTest()` 里，发送消息前后打印 `exchange.IsConnected`。

### 练习 2：自己写一个 MockExchange

```csharp
public class MockExchange : IExchange
{
    private readonly byte[] _response;
    public MockExchange(byte[] response) { _response = response; }

    public void Send(byte[] data) { /* 不真发 */ }
    public byte[]? ReadExactly(int count) => _response;
    public void Close() { }
    public void Dispose() { }
    public bool IsConnected => true;
}
```

然后注册到 `Net.CreateExchange`，验证业务代码不用改就能跑。

### 练习 3：跟踪 ReadExactly 的调用链

从 `EchoServer.HandleClient` 开始 → `FrameCodec.ReceiveFrame(exchange)` → `exchange.ReadExactly(2)` → `StreamExchange.ReadExactly` 内部循环。用断点或 Console.WriteLine 跟踪每一步。

---

## 7. 对照 RemoteOps

| NetLab | RemoteOps | 备注 |
|--------|----------|------|
| `Client.Net.cs` | `Helper.Net.cs` | 教学只含委托 + Send；生产混杂了连接池 + 变长编码 |
| `Net.CreateExchange` | `Net.CreateClient` | 同名不同实现，机制一致 |
| `Net.CreateExchangeFunc` | `Net.CreateClientFunc` | 委托类型 |
| `Net.Send(ip, port, asdu)` | `Net.InternalSend(ip, port, asdu)` | 教学版无连接池 |

---

## Phase 2 总结

```
Phase 2.1: IExchange 接口
  业务代码不再直接依赖 NetworkStream
  → StreamExchange 包装 NetworkStream
  → FrameCodec 加 IExchange 版本重载

Phase 2.2: 委托注入
  创建连接的方式由 Net.CreateExchange 委托决定
  → 启动时注册一次，全进程统一
  → 明文/加密切换只需改一行
```

下一阶段（Phase 3）：连接池。解决 `Net.Send` 每次新建连接的开销。
