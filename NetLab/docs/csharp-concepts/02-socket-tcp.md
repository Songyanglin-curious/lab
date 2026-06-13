# C# 基础：Socket 与 TCP 通信

> 排序：02 — 理解项目中所有网络通信的底层基础。

---

## 速查卡片

```csharp
// 服务端
TcpListener listener = new TcpListener(IPAddress.Any, 9000);
listener.Start();                                // 创建 LISTEN socket，占端口
TcpClient client = listener.AcceptTcpClient();    // 阻塞，从 accept 队列取一个已连接的 socket
NetworkStream stream = client.GetStream();        // 拿流
stream.Read(buffer, 0, size);                     // 读数据
stream.Write(data, 0, data.Length);               // 写数据

// 客户端
TcpClient client = new TcpClient("127.0.0.1", 9000);  // Connect + 三次握手
NetworkStream stream = client.GetStream();
stream.Write(data, 0, data.Length);
stream.Read(buffer, 0, size);
```

---

## 一、是什么

TCP 是一个**面向连接、可靠、字节流**的传输协议。在 .NET 中通过 `TcpListener`（服务端）和 `TcpClient`（客户端）使用。底层 OS 维护 socket 数据结构，应用程序通过句柄操作。

---

## 二、关键概念

### 两种 socket

| | LISTEN socket | 已连接 socket |
|------|--------------|----------------|
| 谁创建 | `TcpListener.Start()` | OS 完成三次握手后自动生成 |
| 状态 | LISTEN | ESTABLISHED |
| 传数据 | 不传，只接受连接 | 传数据（Send/Read 全靠它） |
| 程序怎么拿到 | `TcpListener` 内部持有 | `AcceptTcpClient()` 返回 |
| 一个端口有几个 | 最多 1 个 | 有多少客户端就有多少 |

一个 `TcpListener` 绑定一个端口，但每个客户端连上来 OS 都会生成一个独立的已连接 socket。OS 靠四元组（ServerIP + ServerPort + ClientIP + ClientPort）区分不同的连接。

### 端口是 OS 全局资源

端口号 0~65535，OS 全局管理。同一端口在同一台机器上只能被一个进程占用，和内存不同——端口没有虚拟化。进程 A 占了 9000，进程 B 再占 9000 会抛异常。

### accept 队列

OS 完成三次握手后，把已连接的 socket 放入 accept 队列。`AcceptTcpClient()` 就是从队列里取——空就阻塞，有就拿走。三次握手由 OS 独立完成，不需要进程参与。

### 数据收发：流

`NetworkStream` 是原始字节流，**没有消息边界**。Client 分两次 `Write("Hello")` 和 `Write("World")`，Server 可能一次 `Read` 就收到 `HelloW` 或 `HelloWorld`。需要帧协议来区分消息边界。

---

## 三、与项目的关系

原项目中的通信底层：

- `TCPClient.cs` — 包装 `System.Net.Sockets.TcpClient`，实现 `IExchange` 接口
- `TLCPClient.cs` — 在原 TCP 之上加 TLCP 握手和加密
- `EquipTCPServer.cs` — 使用 `TcpListener` 异步 Accept 模式
- `TLCPServer.cs` — 使用 `TcpListener` + `Ysh.Tlcp.TlcpServer`
- `Net.cs` 的 `InternalSend`、`UseClient`、`CreateClient` — 都建立在 TCP 连接之上

---

## 四、常见混淆点

| 易混 | 区别 |
|------|------|
| LISTEN socket vs 已连接 socket | 前者只接受连接不传数据，后者传数据 |
| 端口 vs 内存 | 端口是 OS 全局的没有虚拟化，内存有虚拟地址空间 |
| `AcceptTcpClient()` vs 回调 | 是阻塞等待，不是注册回调 |
| `TcpListener` vs `TcpClient` | Listener 是服务端等待连接，Client 是主动发起连接 |
| TCP 三次握手 vs `AcceptTcpClient()` | 三次握手由 OS 完成，`AcceptTcpClient` 只是从队列取结果 |
