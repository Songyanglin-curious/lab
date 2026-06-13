# Phase 1.1 — 帧协议：EB90 + 变长长度

> 目标：亲手实现帧协议，让你发的每条消息都有明确的边界。这是从"裸 TCP"到"可用的通信框架"的第一步。

---

## 1. 回顾：粘包问题

在 Phase 0 你看到了：

```
客户端：Write("AAAA") Write("BBBB") Write("CCCC")   ← 发了 3 次
服务端：Read → "AAAABBBBCCCC"                        ← 收了 1 次，全粘在一起
```

TCP 只保证字节按顺序到达，不保留 Write 的次数边界。业务上需要拆分，但 TCP 不帮你做。

**问题：怎么让接收方知道"一条消息在哪结束"？**

---

## 2. 方案：帧协议

在每条消息前面贴标签，写清楚"这条消息多长"。

```
不加帧：
  "AAAA" "BBBB" "CCCC"           ← 混在一起，分不清

加帧：
  [EB 90] [04] AAAA              ← 帧头表示"新消息开始"，长度说"我是 4 字节"
  [EB 90] [04] BBBB
  [EB 90] [04] CCCC
```

接收方流程：
```
1. 读到 [EB 90] → 确认是新消息的开始
2. 读长度 → 4 字节
3. 读 4 字节载荷 → "AAAA"
4. 继续读下一帧 → [EB 90] ...
```

---

## 3. 帧结构设计细节

```
┌──────────┬──────────────┬──────────────────┐
│ 2 bytes  │  1~N bytes   │  0~N bytes       │
│  EB 90   │  变长长度     │  载荷 (payload)   │
└──────────┴──────────────┴──────────────────┘
```

### 3.1 为什么帧头是 2 字节（EB 90）？

EB90 不是用来单独解决粘包的——粘包边界靠**长度字段**解决。EB90 的作用是：

- **协议标识**：标识这是一帧 RemoteOps 协议数据，区别于其他协议
- **误读检测**：读错位或数据异常时尽早发现（非 EB90 直接报错）

- **不是 1 字节**：1 字节只有 256 种值，数据里偶尔出现 `0xEB` 的概率不低，容易误判
- **不是 4 字节**：4 字节（如 HTTP 的 `\r\n\r\n`）浪费带宽。2 字节 65536 种组合足够
- **EB 90 是任意选的**：只要收发双方约定好，用 `AA BB` 也可以。EB 90 来自 GB/T 18657 系列标准

### 3.2 为什么长度用变长编码？

固定 4 字节表示长度 → 每条消息浪费 4 字节。

RemoteOps 里大部分消息是短小的 JSON 字符串（几十到几百字节），偶尔有文件传输（几十 KB）。变长编码：小消息 1 字节，大消息自动扩展。

| 消息大小 | 固定 4 字节 | 变长编码 | 节省 |
|----------|-----------|---------|------|
| < 128 字节 | 4 字节 | 1 字节 | 75% |
| 128~16K | 4 字节 | 2 字节 | 50% |
| 16K~2M | 4 字节 | 3 字节 | 25% |

---

## 4. 变长编码算法详解

核心规则：**每字节低 7 位是数据位，最高位（MSB，第 8 位）是"后续还有字节"标志。**

- MSB = 1：后面还有长度字节
- MSB = 0：这是最后一个长度字节

### 4.1 编码（整数 → 字节）

以 length = 300 为例：

```
300 = 0x12C

Step 1: 300 & 0x7F = 0x2C = 44  → bytes[0] = 0x2C
        300 >>= 7 = 2

Step 2:   2 & 0x7F = 0x02 =  2  → bytes[1] = 0x02
          2 >>= 7 = 0            → 结束

结果：[0x2C, 0x02]  →  反转  →  [0x02, 0x2C]

高位在前，低位在后，符合网络字节序习惯。

Step 3: 非末字节 MSB 置 1
        0x02 | 0x80 = 0x82
        0x2C（末字节，MSB 保持 0）

最终编码：[0x82, 0x2C]
```

### 4.2 解码（字节 → 整数）

收到 `[0x82, 0x2C]`：

```
Step 1: 0x82 & 0x7F = 0x02  → n = 2
        MSB=1 → 还有下一字节，n <<= 7 → n = 256

Step 2: 0x2C & 0x7F = 0x2C  → n |= 44 → 300
        MSB=0 → 结束

结果：300 ✓
```

### 4.3 更多例子

| 十进制 | 编码过程 | 最终字节 |
|--------|---------|---------|
| 0 | 0x00 → MSB=0 | `[0x00]` |
| 127 | 0x7F → MSB=0 | `[0x7F]` |
| 128 | 0x80 & 0x7F=0x00, 1 >>=7 → 0x01 → 反转 `[0x01,0x00]` → MSB `[0x81,0x00]` | `[0x81, 0x00]` |
| 300 | 见上 | `[0x82, 0x2C]` |
| 16384 | 同上逻辑 | `[0x81, 0x80, 0x00]` |

---

## 5. 代码实现

### 5.1 FrameCodec.cs（公共工具类）

```csharp
public static class FrameCodec
{
    private static readonly byte[] Magic = { 0xEB, 0x90 };

    // ===== 发送 =====
    public static void SendFrame(NetworkStream stream, byte[] payload)
    {
        stream.Write(Magic, 0, 2);
        byte[] lenBytes = LengthToBytes(payload.LongLength);
        stream.Write(lenBytes, 0, lenBytes.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    // ===== 接收 =====
    public static byte[]? ReceiveFrame(NetworkStream stream)
    {
        // 1. 读帧头
        if (!ReadExactly(stream, 2, out byte[] magic)) return null;
        if (magic[0] != 0xEB || magic[1] != 0x90)
            throw new Exception($"帧头错误: {magic[0]:X2} {magic[1]:X2}");

        // 2. 读变长长度
        var lenList = new List<byte>();
        while (true)
        {
            if (!ReadExactly(stream, 1, out byte[] b)) return null;
            lenList.Add(b[0]);
            if ((b[0] & 0x80) == 0) break; // MSB=0 → 最后一字节
        }
        long length = BytesToLength(lenList.ToArray());

        // 3. 读载荷
        if (length == 0) return Array.Empty<byte>();
        if (length > int.MaxValue)
            throw new Exception($"载荷过大: {length}");
        byte[] payload = new byte[length];
        if (ReadExactly(stream, payload, (int)length) != length)
            return null;
        return payload;
    }

    // ===== 变长编码 =====
    public static byte[] LengthToBytes(long length)
    {
        if (length == 0) return new byte[] { 0x00 };

        var bytes = new List<byte>();
        while (length > 0)
        {
            bytes.Add((byte)(length & 0x7F));
            length >>= 7;
        }
        bytes.Reverse();
        // 非末字节 MSB 置 1
        for (int i = 0; i < bytes.Count - 1; i++)
            bytes[i] |= 0x80;
        return bytes.ToArray();
    }

    public static long BytesToLength(byte[] bytes)
    {
        long n = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            n <<= 7;
            n |= (long)(bytes[i] & 0x7F);
        }
        return n;
    }
}
```

完整的 `ReadExactly` 实现见 `Server/FrameCodec.cs`。

---

## 6. 集成到 EchoServer/EchoClient

### 改造前（原始字节流）

```csharp
// 服务端
int n = stream.Read(buffer, 0, buffer.Length);
stream.Write(buffer, 0, n);  // 回显

// 客户端
stream.Write(Encoding.UTF8.GetBytes("AAAA"));
stream.Write(Encoding.UTF8.GetBytes("BBBB"));
int n = stream.Read(buf, 0, buf.Length);  // 可能收到粘包
```

### 改造后（帧协议）

```csharp
// 服务端
byte[]? payload = FrameCodec.ReceiveFrame(stream);  // 读完整一帧
FrameCodec.SendFrame(stream, payload);              // 回显一帧

// 客户端
FrameCodec.SendFrame(stream, Encoding.UTF8.GetBytes("AAAA"));
FrameCodec.SendFrame(stream, Encoding.UTF8.GetBytes("BBBB"));
byte[]? response = FrameCodec.ReceiveFrame(stream); // 精确收到一帧
```

---

## 7. 动手练习

### 练习 1：跑起来

```powershell
# 终端 1
dotnet run --project Server

# 终端 2
dotnet run --project Client
```

观察服务端日志：
```
[127.0.0.1:xxxxx] 收到帧：4 字节 → AAAA
[127.0.0.1:xxxxx] 回显 4 字节
[127.0.0.1:xxxxx] 收到帧：4 字节 → BBBB
[127.0.0.1:xxxxx] 回显 4 字节
[127.0.0.1:xxxxx] 收到帧：4 字节 → CCCC
[127.0.0.1:xxxxx] 回显 4 字节
```

每帧独立、边界清晰。粘包问题解决 ✓。

### 练习 2：自己实现一遍 LengthToBytes

不看 `FrameCodec.cs`，用纸笔或代码实现 `LengthToBytes(300)`，输出 `0x82 0x2C`。

### 练习 3：极端测试

- 发一条 0 字节消息（空载荷） → 帧为 `EB 90 00`
- 发一条 50000 字节消息 → 长度占 3 字节，数据完整收到

---

## 8. 对照 RemoteOps

| NetLab | RemoteOps 对应 | 文件 |
|--------|---------------|------|
| `FrameCodec.SendFrame()` | `APDU.WriteTo()` | `services/YCYWDataExchange/YCYWDataExchange/Protocol/APDU.cs:19` |
| `FrameCodec.ReceiveFrame()` | `APDU.Read()` | `services/YCYWDataExchange/YCYWDataExchange/Protocol/APDU.cs:30` |
| `FrameCodec.LengthToBytes()` | `Helper.Net.LengthToBytes()` | `services/YCYWDataExchange/YCYWDataExchange/Helper/Net.cs:24` |
| `FrameCodec.BytesToLength()` | `Helper.Net.BytesToLength()` | `services/YCYWDataExchange/YCYWDataExchange/Helper/Net.cs:43` |
| `ReadExactly()` | `TCPClient.Read(long count)` | `services/YCYWDataExchange/YCYWDataExchange/Exchange/TCPClient.cs:50` |

生产代码中的 APDU.cs 和 NetLab 的 FrameCodec.cs 逻辑几乎一模一样——你现在写的代码直接对应生产代码。

---

## 自检清单

- [ ] 帧头 EB 90 被篡改了，接收方会怎样？
- [ ] 为什么长度字段要逐字节读，不能一次性 Read(4)？
- [ ] 如果客户端发 3 条消息，服务端现在收到几条？为什么？
