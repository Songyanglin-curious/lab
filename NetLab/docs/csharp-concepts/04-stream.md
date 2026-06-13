# C# 基础：Stream（流）

> 排序：04 — 理解为什么 `Write("Hello")` 两次，对方 `Read` 可能一次收到 `HelloW`。

---

## 速查卡片

```csharp
// 流 = 字节水管，只管字节不管消息边界
NetworkStream stream = tcpClient.GetStream();

stream.Write(bytes, 0, len);          // 往水管里倒字节
int read = stream.Read(buffer, 0, size);  // 从水管接字节，可能接不满

// TCP 不保证发的次数 = 收的次数
// 左端: Write("Hello"), Write("World")
// 右端: Read 可能收到 "HelloW"、"HelloWorld"、分三次收到...
```

---

## 一、是什么

`Stream` 是 .NET 对"字节序列"的抽象基类。不管背后的实体是文件、网络连接、还是内存数组，读写操作都一样：`Read` 和 `Write`。

关键限制：Stream **没有消息边界**。TCP Stream 只保证字节的先后顺序，不保证"发几次就收几次"。

---

## 二、水管比喻

```
发送方                                    接收方
[Hello] → 拧开水龙头 ──── 水管 ────→ 杯子接水 → 可能接到 "HelloW"、"Hel" 等
[World] → 再倒一杯
```

你倒进去的是两杯水，接收方用杯子接的时候，接到的一杯可能是第一杯的全量加第二杯的一半。水管只负责把水送过去，不负责"你倒的时候停的那一下"。

## 三、为什么会有粘包/拆包

TCP 是一个字节流协议，底层有发送缓冲区和接收缓冲区。发送方的两次 `Write` 可能被合并在发送缓冲区里一起发出去（Nagle 算法），接收方的 `Read` 缓冲可能一次读到多帧数据。

所以帧协议（如原项目的 `0xEB 0x90` + 变长长度）是必需的——用帧头标记"一条消息从哪开始到哪结束"。

## 四、常用 Stream 类型

| 类型 | 背后是什么 | 用在哪 |
|------|-----------|--------|
| `NetworkStream` | TCP 连接 | `TcpClient.GetStream()` — 网络通信 |
| `FileStream` | 磁盘文件 | 文件读写 |
| `MemoryStream` | 内存中的 byte 数组 | 临时数据暂存 |
| `CryptoStream` | 套在另一个 Stream 上，加解密 | TLCP 内部 |

## 五、与项目的关系

- `TCPClient.cs:20` — `stream = new NetworkStream(socket, true)` 包装 TCP 连接为流
- `TLCPClient.cs:62` — `stream = c.GetStream()` 拿到原始 TCP 流 → 交给 `Ysh.Tlcp.TlcpClient` 加密
- `APDU.cs:19-28` — `WriteTo(IExchange)` 靠帧头解决流没有消息边界的问题
- `EquipChannel.cs:22` — `byte[] data = APDU.Read(exchange)` 底层 `exchange.Read()` 就是读流
- `FileOperate.cs` — `FileStream` 分块写文件

## 六、常见混淆点

| 易混 | 区别 |
|------|------|
| Stream vs 消息 | Stream 是字节序列，消息是"有头有尾的完整数据块" |
| `Read` 返回的字节数 | 不一定等于请求的字节数，可能更少（TCP 切片） |
| Stream vs socket | socket 是 OS 层的，Stream 是 .NET 对它的封装 |
| `Flush` | Write 后数据可能还在缓冲区，Flush 强制立即发送 |
