# Phase 1.2 — ASDU/APDU 分离：发送侧消息封装入门

> 目标：把"业务要说什么"和"怎么发出去"拆成两层。业务代码不再碰到 `0xEB 0x90` 这些字节。

---

## 1. 问题引入

当前帧协议代码虽然解决了粘包，但业务代码里到处是 `0xEB`、`0x90`、变长编码：

```csharp
// EchoClient.cs — 业务代码里混杂了帧协议细节
FrameCodec.SendFrame(stream, Encoding.UTF8.GetBytes("AAAA"));
FrameCodec.SendFrame(stream, Encoding.UTF8.GetBytes("BBBB"));
```

这里有两个问题：

1. **业务不关心帧**：发 "AAAA" 的业务逻辑不应该知道帧头是 `EB 90` 还是 `AA BB`
2. **消息格式混在业务里**：如果将来要改成发 JSON，需要改所有调 `GetBytes` 的地方

---

## 2. 分层思想

```
┌──────────────────────┐
│  业务层               │  "我要查通道状态"（不关心怎么传）
│  ASDU (消息体)        │  只管数据是什么
├──────────────────────┤
│  传输层               │  EB90 + 变长长度 + 载荷
│  APDU (传输帧)        │  只管怎么发出去
├──────────────────────┤
│  TCP 层               │  字节流
└──────────────────────┘
```

类比：寄快递

```
ASDU = 你要寄的东西（一本书）
APDU = 快递包装盒（纸箱 + 胶带 + 面单）
TCP  = 快递卡车

你（业务层）只关心书有没有寄到。
快递公司（传输层）关心纸箱够不够大、面单贴没贴好。
```

在 RemoteOps 里：

```
ASDU（Application Service Data Unit）= 应用服务数据单元 = 消息体
APDU（Application Protocol Data Unit）= 应用协议数据单元 = 传输帧
```

---

## 3. 接口定义

```csharp
public interface IAsdu
{
    byte[] GetContent();  // 返回消息体的字节表示
}
```

接口极简：只有一个方法，返回 payload 字节。具体怎么序列化由实现类决定。

### 两种实现

```csharp
// 原始字节 — 用于协商、简单确认
public class BinaryAsdu : IAsdu
{
    public byte[] Data { get; }
    public BinaryAsdu(byte[] data) { Data = data; }
    public byte[] GetContent() => Data;
}

// JSON 序列化 — 用于复杂业务数据
public class JsonAsdu : IAsdu
{
    public string Json { get; }
    public JsonAsdu(string json) { Json = json; }
    public byte[] GetContent() => Encoding.UTF8.GetBytes(Json);
}
```

---

## 4. 集成改造

`FrameCodec` 只负责收发 `byte[]`，业务代码自己完成 ASDU → byte[] 的转换：

```csharp
// FrameCodec 只需要 byte[] 这一种签名
FrameCodec.SendFrame(NetworkStream stream, byte[] payload)

// 业务代码：
IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes("AAAA"));
byte[] payload = msg.GetContent();           // ASDU 转字节
FrameCodec.SendFrame(stream, payload);       // 交给传输层
```

关键点：`GetContent()` 是 ASDU 的事，组帧拆帧是 FrameCodec 的事。两层各司其职。

### 客户端示例

```csharp
IAsdu msg1 = new BinaryAsdu(Encoding.UTF8.GetBytes("AAAA"));
FrameCodec.SendFrame(stream, msg1.GetContent());

IAsdu msg2 = new JsonAsdu("{\"cmd\":\"hello\",\"id\":1}");
FrameCodec.SendFrame(stream, msg2.GetContent());

IAsdu msg3 = new BinaryAsdu(new byte[] { 0x01 });
FrameCodec.SendFrame(stream, msg3.GetContent());
```

### 服务端示例

```csharp
byte[]? payload = FrameCodec.ReceiveFrame(stream);
// 根据首字节判断类型（简化版——生产代码用 Business 枚举）
IAsdu asdu = new BinaryAsdu(payload);
FrameCodec.SendFrame(stream, asdu.GetContent());
```

### 4.4 当前局限

以上只完成了**发送侧**的 ASDU/APDU 分离。接收侧仍是：

```csharp
byte[]? payload = FrameCodec.ReceiveFrame(stream);
// 手动判断消息类型...
```

完整的接收侧解耦需要 `AsduFactory.Parse(byte[])` 或在 APDU 层面做类型分发。这在后续引入 IChannel + Business 枚举时自然补齐。

---

## 5. 对照 RemoteOps 的完整层级

RemoteOps 的 ASDU 体系比 NetLab 丰富得多：

```
IAsdu (接口)
├── BinaryAsdu           ← 协商帧（1 字节 Business 枚举）
├── JsonAsdu (抽象)
│   ├── JsonDataASDU     ← 包装已有 JsonObject
│   ├── HeartBeatASDU    ← 心跳
│   ├── ManageRequestASDU ← 管理请求 {"Head":{...}, "Data":{...}}
│   ├── ManageResponseASDU
│   └── Negotiate 系列
└── Response 系列
```

但核心思想完全一样：**IAsdu.GetContent() → byte[] → APDU.WriteTo() → TCP**。

---

## 6. 动手练习

### 练习 1：跑起来

```powershell
dotnet run --project Server
dotnet run --project Client
```

观察服务端日志：每条消息独立成帧，且打印了消息类型。

### 练习 2：加一种新的 ASDU

自己写一个 `HeartbeatAsdu` 类：

```csharp
public class HeartbeatAsdu : IAsdu
{
    public byte[] GetContent()
    {
        return new byte[] { 0xFF };  // 心跳标记
    }
}
```

然后在客户端发送，在服务端识别它。

### 练习 3：在服务端按类型分派

```csharp
byte[]? payload = FrameCodec.ReceiveFrame(stream);
if (payload != null && payload.Length > 0 && payload[0] == 0xFF)
{
    Console.WriteLine("收到心跳");
    // 不回复或回复特定内容
}
else
{
    FrameCodec.SendFrame(stream, new BinaryAsdu(payload));
}
```

---

## 7. 对照 RemoteOps

| NetLab | RemoteOps | 文件 |
|--------|----------|------|
| `IAsdu` | `IASDU` | `Protocol/Base/ASDU.cs` |
| `BinaryAsdu` | `BinaryASDU` | 同上 |
| `JsonAsdu` | `JsonASDU` (abstract) | 同上 |
| `FrameCodec.SendFrame(IAsdu)` | `APDU.WriteTo(IExchange)` | `Protocol/APDU.cs:19` |
| — | `ManageRequestASDU` / `ManageResponseASDU` | 各类业务消息子类 |

---

## 8. 阶段总结

你现在拥有了等同于 RemoteOps 核心通信框架的简化版：

```
Phase 0:  TCP 字节流                → 知道 TCP 没有消息边界
Phase 1.1: 帧协议 (EB90+变长长度)   → 有消息边界了
Phase 1.2: ASDU/APDU 分离           → 业务和传输解耦了

对应 RemoteOps：
  Phase 0    → TCPClient.cs（原始 TCP）
  Phase 1.1  → APDU.cs（帧编码）
  Phase 1.2  → IAsdu / BinaryASDU / JsonASDU（消息体）
```

下一阶段（Phase 2）要做的是：连接池 + IExchange 接口 + 委托注入——让你能在一行代码不改的情况下切换明文/加密。
