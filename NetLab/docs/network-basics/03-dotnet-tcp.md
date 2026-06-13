# 网络基础 03 — .NET 中的 TCP

> 目标：掌握 `TcpListener`、`TcpClient`、`NetworkStream` 的用法。能写出一个最基本的服务端和客户端。

---

## 1. 问题引入

刚才讲了 TCP 是字节流，那在 C# 里怎么用？RemoteOps 里到处是 `TcpClient` 和 `NetworkStream`（见于 `TCPClient.cs`、`EquipTCPServer.cs`、`Net.cs`），必须先会最基本的用法。

---

## 2. 三个核心类

### 2.1 TcpListener — 服务端监听器

```csharp
// 1. 创建 — 绑定到某个 IP 和端口
TcpListener listener = new TcpListener(IPAddress.Any, 9000);
// IPAddress.Any = 监听本机所有网卡（0.0.0.0）
// IPAddress.Parse("127.0.0.1") = 只监听回环（外部连不了）

// 2. 启动监听
listener.Start();
// 此时 OS 创建 LISTEN socket，端口被占用

// 3. 等待客户端连接（阻塞）
TcpClient client = listener.AcceptTcpClient();
// 有客户端连上来后，返回一个已连接的 TcpClient
// 如果没人连，就一直阻塞在这里

// 4. 用完关闭
listener.Stop();
```

**AcceptTcpClient() 的内部流程：**

```
1. OS 完成三次握手 → 已连接 socket 放入 accept 队列
2. AcceptTcpClient() 从队列取出一个
3. 队列空 → 阻塞等待（线程睡眠，不耗 CPU）
4. 取到了 → 返回 TcpClient（包装了已连接 socket）
```

### 2.2 TcpClient — 客户端连接

```csharp
// 客户端写法
TcpClient client = new TcpClient("127.0.0.1", 9000);
// 构造函数里做了：创建 socket → Connect → 三次握手
// 如果服务端没启动或端口不对 → 抛异常

// 服务端里也能用（AcceptTcpClient 返回的就是 TcpClient）
TcpClient client = listener.AcceptTcpClient();

// 用完关闭
client.Close();
```

### 2.3 NetworkStream — 双向字节流

```csharp
NetworkStream stream = client.GetStream();
// 拿到一个流对象，既能读又能写（全双工）

// 读数据（阻塞）
byte[] buffer = new byte[1024];
int bytesRead = stream.Read(buffer, 0, buffer.Length);
// Read 的三个参数：(存哪, 从哪开始存, 最多读多少)
// 返回实际读到的字节数
// 没有数据时阻塞等待

// 写数据
byte[] data = Encoding.UTF8.GetBytes("Hello");
stream.Write(data, 0, data.Length);
// Write 的三个参数：(发啥, 从哪开始发, 发多少)
// Write 是非阻塞的——数据丢给 OS 的发送缓冲区就返回

// 用完关闭
stream.Close();
```

### 2.4 Read 的"不保证"

```csharp
// NetworkStream.Read 的重要特性：
// 不保证一次返回你请求的字节数

byte[] buf = new byte[100];
int n = stream.Read(buf, 0, 100);
// 你可能期望 n == 100，但实际 n 可能是 30、50、100...

// 正确写法：循环读到够
public byte[] ReadExactly(NetworkStream stream, int count)
{
    byte[] data = new byte[count];
    int total = 0;
    while (total < count)
    {
        int n = stream.Read(data, total, count - total);
        if (n == 0) return null;  // 连接断开
        total += n;
    }
    return data;
}
```

RemoteOps 中的 `TCPClient.Read()`（`services/YCYWDataExchange/YCYWDataExchange/Exchange/TCPClient.cs`）就是这么写的。

---

## 3. 完整示例：一句话 Echo

结合已有 `Client/` 和 `Server/` 的代码来理解。

### 服务端（Server/Program.cs）

工作流程：

```
1. 监听 9000 端口
2. AcceptTcpClient() → 阻塞等连接
3. 有人连上来 → 拿到 NetworkStream
4. stream.Read() → 阻塞等数据
5. 读到了 → 判断是不是 "GetChannelStatus" → 回 "通道状态：已开启"
6. 关闭连接
```

### 客户端（Client/Program.cs）

工作流程：

```
1. new TcpClient("127.0.0.1", 9000) → 发起连接
2. 拿到 NetworkStream
3. stream.Write("GetChannelStatus") → 发请求
4. stream.Read() → 阻塞等回包
5. 读到了 → 打印
6. 关闭连接
```

---

## 4. 和 RemoteOps 的关系

生产代码中的对应位置：

| NetLab | RemoteOps 对应 | 文件 |
|--------|---------------|------|
| `new TcpListener` | `EquipTCPServer` 监听端口 | `Exchange/Equip/EquipTCPServer.cs` |
| `new TcpClient` | `Net.UseClient()` 新建连接 | `Helper/Net.cs` |
| `stream.Read()` | `TCPClient.Read()` 分块循环读 | `Exchange/TCPClient.cs` |
| `stream.Write()` | `TCPClient.Send()` 直接写 | `Exchange/TCPClient.cs` |

---

## 5. 常见坑

| 坑 | 解释 |
|----|------|
| `Read` 返回 0 | 对方已关闭连接（FIN），不是"没数据"。要判断并处理 |
| `Read` 不保证读满 | 见 2.4 的示例，必须循环读 |
| `Write` 后忘了 `Flush` | `NetworkStream.Write` 其实会自动 Flush，但显式 Flush 是安全习惯 |
| 一个 Server 只 Accept 一次 | 现有 Server/Program.cs 只 Accept 一个客户端就关。生产代码是循环 Accept |
| `client.Close()` vs `client.Dispose()` | Close 和 Dispose 差不多，都关连接。Dispose 还能配合 `using` |
