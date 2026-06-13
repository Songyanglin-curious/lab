# 动手练习 — Echo 程序 & 粘包观察

> 目标：把前 3 篇的概念变成实战。先写 echo 程序，再**亲手制造并观察粘包现象**——这是理解帧协议为什么存在的唯一方法。

---

## 热身：跑起 Step 0 原始代码

确保基础 TCP 能跑通：

```powershell
# 终端 1：启动服务端
dotnet run --project Server

# 终端 2：启动客户端
dotnet run --project Client
```

---

## 练习 1：写一个多连接的 Echo 服务端

### 要求

在 `Server/EchoServer.cs` 中实现：

1. 不处理具体的业务，只是**原样返回**客户端发的任何内容（echo）
2. **支持多个客户端**——用 `while(true)` + `AcceptTcpClient()`，每来一个客户端在新线程/Task 里处理
3. 每个客户端连接后，**循环收发**——客户端发什么就回什么，直到客户端断开

### 提示

```csharp
// 关键结构（放在 EchoServer.cs 的 Start() 方法里）：
TcpListener listener = new TcpListener(IPAddress.Any, 9000);
listener.Start();

while (true)
{
    TcpClient client = listener.AcceptTcpClient();
    Task.Run(() => HandleClient(client));
}

void HandleClient(TcpClient client)
{
    using NetworkStream stream = client.GetStream();
    byte[] buffer = new byte[1024];
    while (true)
    {
        int n = stream.Read(buffer, 0, buffer.Length);
        if (n == 0) break;
        stream.Write(buffer, 0, n);
    }
}
```

### 调用入口

`Server/Program.cs` 只保留一行：

```csharp
using NetLab.Server;
new EchoServer(9000).Start();
```

---

## 练习 2：写一个连发多条消息的客户端

### 要求

在 `Client/EchoClient.cs` 中实现：

1. 连接 echo 服务端
2. **连续发送 3 条不同的消息**（中间不做任何等待）
3. 再读回响应，打印收到了什么

```csharp
// 客户端关键代码（放在 EchoClient.cs 的 RunTest() 方法里）：
stream.Write(Encoding.UTF8.GetBytes("AAAA"));
stream.Write(Encoding.UTF8.GetBytes("BBBB"));
stream.Write(Encoding.UTF8.GetBytes("CCCC"));

// 然后读回
byte[] buf = new byte[4096];
int n = stream.Read(buf, 0, buf.Length);
Console.WriteLine($"服务端回：{Encoding.UTF8.GetString(buf, 0, n)}");
```

### 调用入口

`Client/Program.cs` 只保留一行：

```csharp
using NetLab.Client;
new EchoClient("127.0.0.1", 9000).RunTest();
```

---

## 练习 3：观察粘包 — 这是最关键的一步

### 现象

服务端输出（可能）是：

```
收到客户端连接：127.0.0.1:xxxxx
收到 12 字节：AAAABBBBCCCC
回显 12 字节
```

注意：客户端发了 3 次 `Write`，但服务端 **1 次 Read** 就把三条消息全部收到了。

### 如果没粘包怎么办？

localhost 有时因为回环接口优化不粘包。试以下方法：

1. **让客户端发完不立刻读**，加 `Thread.Sleep(5000)` 再读——给服务端时间收齐
2. **服务端先把 buffer 改小**（如 2 字节）再 Read——强制拆包
3. 把服务端的 buffer 改大（4096），确保一次 Read 能装下所有消息
4. 增大消息量（每条 1KB），让发送缓冲区累积

### 核心发现

```
你写了 3 次 Write，对方 1 次 Read 就收了 3 条。
说明：TCP 不保留 Write 的"次数"边界。

消息拼在一起了，怎么拆开？
→ 你需要帧协议（下一阶段学）
→ 这就是 RemoteOps 中 APDU.cs 存在的根本原因
```

---

## 对照 RemoteOps

| 练习中的概念 | 生产代码对应 |
|-------------|------------|
| 服务端循环 accept | `EquipTCPServer.cs` 的 `AcceptCallback` |
| 收到多少就回多少 | `EquipAgentProcess.DataReceived`（透传模式） |
| 粘包现象 | 导致必须引入 `APDU.Read()` 和 `APDU.WriteTo()` |
| 循环读直到读够 | `TCPClient.Read(count)` 的分块循环 |
| 多客户端并发 | `Net.cs` 的 `ConcurrentDictionary<string, List<Extend<IExchange>>>` |

---

## 自检清单

完成以上练习后，确保能回答：

- [ ] 为什么服务端要用 `while(true)` + `AcceptTcpClient()` —— 而不是只 Accept 一次？
- [ ] 为什么 `HandleClient` 里也是 `while(true)` 循环读 —— 而不是只读一次？
- [ ] 客户端发了 3 次，服务端收到 1 次——这暴露了 TCP 的什么特性？
- [ ] 如果客户端发了 `"GetChannelStatus"` + `"GetFileList"`，服务端怎么知道第一个在哪结束？
