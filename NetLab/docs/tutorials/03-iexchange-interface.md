# Phase 2.1 — IExchange 接口：传输层抽象

> 目标：不再直接依赖 `NetworkStream`，用接口抽象掉底层读写方式。为后续加密切换打下基础。

---

## 1. 问题引入

当前 EchoServer / EchoClient 直接操作 `NetworkStream`：

```csharp
// 问题：代码直接依赖 NetworkStream
using NetworkStream stream = client.GetStream();
byte[] payload = FrameCodec.ReceiveFrame(stream);   // stream 字面量
FrameCodec.SendFrame(stream, payload);              // stream 字面量
```

三个问题：

1. **无法替换**：如果将来换成 TLCP 加密（不走 `NetworkStream` 而走 `Ysh.Tlcp.TlcpClient`），所有调用的地方都得改
2. **无法测试**：单元测试没法 mock 一个 `NetworkStream`（它依赖真实的 socket）
3. **职责混合**：业务代码关心的是"收一条消息/发一条消息"，不应该知道底层是 `NetworkStream` 还是 `TlcpClient`

---

## 2. 方案：IExchange 接口

定义一套方法签名，描述"能收发字节、能关连接"的抽象能力：

```csharp
public interface IExchange : IDisposable
{
    void Send(byte[] data);              // 发字节
    byte[]? ReadExactly(int count);      // 精确读满 count 字节，断开返回 null
    void Close();                        // 关连接（Dispose 内部调 Close）
    bool IsConnected { get; }            // 连接是否存活
}
```

关键设计决策：

| 决策                                                        | 原因                                               |
| ----------------------------------------------------------- | -------------------------------------------------- |
| `ReadExactly(count)` 不是 `Read(buffer, offset, count)` | 协议层要"读满指定字节"，不要半包语义               |
| 返回 `byte[]?`，null = 断开                               | 调用方只需判空，不需要检查实际读了多少             |
| 继承 `IDisposable` + 保留 `Close()`                     | `using` 能自动清理；`Close()` 可重复调用不报错 |

---

## 3. 实现：StreamExchange

```csharp
public class StreamExchange : IExchange
{
    private readonly NetworkStream _stream;

    public StreamExchange(NetworkStream stream)
    {
        _stream = stream;
    }

    public void Send(byte[] data)
    {
        _stream.Write(data, 0, data.Length);
        _stream.Flush();
    }
    //基于 TCP 已经保证的“有序字节流”自己循环 Read直到凑够 count 个字节
    public byte[]? ReadExactly(int count)
    {
        byte[] buffer = new byte[count];
        int total = 0;
        while (total < count)
        {
            int n = _stream.Read(buffer, total, count - total);
            if (n == 0) return null;   // 对端断开
            total += n;
        }
        return buffer;
    }

    public bool IsConnected
    {
        get
        {
            try { return !(_socket.Poll(0, SelectMode.SelectRead) && _socket.Available == 0); }
            catch { return false; }
        }
    }

    public void Close() { try { _stream.Close(); } catch { } }
    public void Dispose() => Close();
}
```

注意：`ReadExactly` 内部做循环读，调用方（`FrameCodec`）不再自己处理半包问题。这是和原来 `FrameCodec.ReadExactly(NetworkStream stream, ...)` 的本质区别——原来循环读逻辑在 `FrameCodec` 里，现在移到 `IExchange` 实现里。

---

## 4. FrameCodec 加 IExchange 版本

```csharp
// ==== 新增 IExchange 版本 ====

public static void SendFrame(IExchange exchange, byte[] payload)
{
    byte[] lenBytes = LengthToBytes(payload.LongLength);
    byte[] frame = new byte[2 + lenBytes.Length + payload.Length];
    Array.Copy(Magic, 0, frame, 0, 2);
    Array.Copy(lenBytes, 0, frame, 2, lenBytes.Length);
    Array.Copy(payload, 0, frame, 2 + lenBytes.Length, payload.Length);
    exchange.Send(frame);   // 一次 Send，发完整帧
}

public static byte[]? ReceiveFrame(IExchange exchange)
{
    byte[]? magic = exchange.ReadExactly(2);
    if (magic == null) return null;
    // ... 逐字节读长度 → 精确读载荷 → return payload
}
```

与 NetworkStream 版本的区别：

| 维度   | NetworkStream 版本                      | IExchange 版本                           |
| ------ | --------------------------------------- | ---------------------------------------- |
| 发送   | 三次 `stream.Write`（帧头/长度/载荷） | 拼好整帧，一次 `exchange.Send`         |
| 接收   | 手动 `ReadExactly(stream, ...)` 循环  | `exchange.ReadExactly(count)` 一步到位 |
| 用哪个 | 向后兼容旧代码                          | **Phase 2 起推荐**                 |

---

## 5. 改造效果

### 改造前（直接依赖 NetworkStream）

```csharp
// EchoServer
using NetworkStream stream = client.GetStream();
byte[] payload = FrameCodec.ReceiveFrame(stream);
FrameCodec.SendFrame(stream, payload);

// EchoClient
using NetworkStream stream = client.GetStream();
FrameCodec.SendFrame(stream, Encoding.UTF8.GetBytes("AAAA"));
byte[] response = FrameCodec.ReceiveFrame(stream);
```

### 改造后（通过 IExchange）

```csharp
// EchoServer
using IExchange exchange = new StreamExchange(client.GetStream());
byte[] payload = FrameCodec.ReceiveFrame(exchange);
IAsdu asdu = new BinaryAsdu(payload);
FrameCodec.SendFrame(exchange, asdu.GetContent());

// EchoClient — 方式一：直接持 IExchange
using IExchange exchange = ...;
FrameCodec.SendFrame(exchange, msg.GetContent());
byte[] response = FrameCodec.ReceiveFrame(exchange);

// EchoClient — 方式二：通过 Net.Send（下一步学）
byte[] response = Net.Send("127.0.0.1", 9000, asdu);
```

关键变化：EchoServer 和 EchoClient 不再 `using System.Net.Sockets;` 不需要知道底层是 TCP。只依赖 `IExchange`。

---

## 6. 对照 RemoteOps

| NetLab             | RemoteOps      | 文件                                                                   |
| ------------------ | -------------- | ---------------------------------------------------------------------- |
| `IExchange`      | `IExchange`  | `services/YCYWDataExchange/.../Exchange/IExchange.cs`                |
| `StreamExchange` | `TCPClient`  | `services/YCYWDataExchange/.../Exchange/TCPClient.cs`                |
| —                 | `TLCPClient` | `services/TLCPDataExchange/TLCPClient.cs`（下一版本 IExchange 实现） |

> RemoteOps 里 `TCPClient` 这个名字容易误解——它不是 TcpClient 本身，而是包装了 TcpClient 的 IExchange 实现。NetLab 命名为 `StreamExchange` 更准确。

---

## 自检清单

- [X] `IExchange.ReadExactly(100)` 和 `NetworkStream.Read(buf, 0, 100)` 有什么区别？

从代码角度来说NetworkStream.Read(buf, 0, 100)
= 尝试最多读 100 个字节，可能只返回 1~100 个字节
IExchange.ReadExactly(100)
= 必须凑够 100 个字节才返回 byte[]，否则断开返回 null，
但是从抽象层级来看差别就是 `IExchange.ReadExactly(100)`描述的是读100个字节 只是表述这一抽象不关注底层，而 `NetworkStream.Read(buf, 0, 100)`表述的是从 `NetworkStream`流中读取0-100字节到buf中，使用效果或许在目前一致但是未来 `NetworkStream.Read(buf, 0, 100)`抽象层级不够会在未来演进时丢失这个隐藏的抽象而变得混乱。

- [X] 为什么 `FrameCodec` 同时保留 NetworkStream 和 IExchange 两套接口？
  目前为了兼容和教学，我认为是可以不保留的；还有就是服务端没有使用这一套抽象为了和服务端配套而已
- [X] 如果将来要加 MockExchange（测试用），需要改 EchoServer 的代码吗？
  不知道这个描述的是啥？果如果EchoServer / EchoClient 只依赖 `IExchange`，理论上加 `MockExchange` 不需要改业务代码，只需要换 `Net.CreateExchange` 的注册实现。
