# Phase 3 — 连接池：连接复用

> 目标：`Net.Send` 不再每次新建 TCP 连接，用连接池复用。理解线程安全的租约模式。

---

## 1. 问题引入

Phase 2 的 `Net.Send` 每次调用都新建 TCP：

```csharp
public static byte[]? Send(string ip, int port, IAsdu asdu)
{
    using IExchange exchange = CreateExchange(ip, port);  // 三次握手
    FrameCodec.SendFrame(exchange, asdu.GetContent());
    return FrameCodec.ReceiveFrame(exchange);
}   // using 结束 → Dispose → Close → 四次挥手
```

每个请求一次握手 + 一次挥手。主站每秒几十个请求时，开销不可接受。

---

## 2. 方案：连接池 + 租约模式

核心思想：**用完不关，放回池里，下次复用。**

```
┌──────────────────────┐
│  Net.Send(ip,port,asdu)│
│    ↓                   │
│  _pool.UseClient(...)  │  ← 从池里借
│    ↓                   │
│  SendFrame / ReceiveFrame
│    ↓                   │
│  lease.Dispose()       │  ← IsUsing = false（归还）
└──────────────────────┘
```

### 数据结构

```
ConcurrentDictionary<string, PoolBucket>
     ↑                        ↑
  key = "127.0.0.1:9000"    每个 ip:port 一个桶
                             PoolBucket {
                                 Lock (锁),
                                 Items: List<ExchangeLease>
                             }

ExchangeLease {
    Exchange: IExchange,
    IsUsing: bool           ← true = 被借出, false = 空闲
}
```

### 线程安全分工

| 层级 | 用什么保护 | 保护什么 |
|------|-----------|---------|
| 外层 | `ConcurrentDictionary` | 多 ip:port 目标并发访问 |
| 内层 | `lock(bucket.Lock)` | 同一个 ip:port 的连接列表操作 |

只用 `ConcurrentDictionary` 不够——它只保证 key-value 操作原子，不保证 List 的并发安全。双层保护各管各的。

---

## 3. 租约模式（ExchangeLease）

```csharp
public class ExchangeLease : IDisposable
{
    public IExchange Exchange { get; }
    public bool IsUsing { get; internal set; }

    public ExchangeLease(IExchange exchange)
    {
        Exchange = exchange;
        IsUsing = true;
    }

    public void Dispose()
    {
        IsUsing = false;   // 归还池，不关连接！
    }
}
```

关键：`Dispose()` 不调用 `exchange.Close()`。连接保持 ESTABLISHED，下次可以复用。

配合 `using` 语法：

```csharp
using ExchangeLease lease = pool.UseClient(ip, port);
// 用 lease.Exchange 收发...
// using 结束 → lease.Dispose() → IsUsing = false → 归还
```

---

## 4. UseClient 核心逻辑

```csharp
public ExchangeLease? UseClient(string ip, int port)
{
    string key = $"{ip}:{port}";
    PoolBucket bucket = _buckets.GetOrAdd(key, _ => new PoolBucket());

    lock (bucket.Lock)
    {
        // 步骤 1：反向遍历，找空闲且存活的连接
        for (int i = bucket.Items.Count - 1; i >= 0; i--)
        {
            ExchangeLease lease = bucket.Items[i];
            if (lease.IsUsing) continue;         // 正被别的线程用着

            if (lease.Exchange.IsConnected)      // 连接还活着
            {
                lease.IsUsing = true;
                return lease;
            }

            lease.Exchange.Close();              // 连接已死，清理
            bucket.Items.RemoveAt(i);
        }

        // 步骤 2：没有可用连接，新建
        IExchange exchange = _factory(ip, port);
        if (exchange == null) return null;

        ExchangeLease newLease = new ExchangeLease(exchange);
        bucket.Items.Add(newLease);
        return newLease;
    }
}
```

细节说明：

| 细节 | 原因 |
|------|------|
| 反向遍历 | RemoveAt 时正序会导致索引错位 |
| `IsConnected` 检查 | TCP 连接可能被对端关闭（FIN），不能复用一个死连接 |
| 死连接先 `Close()` 再 `RemoveAt` | 确保 socket 资源释放 |
| `lock` 包住整个查找+新建 | 防止两个线程同时发现没可用连接，同时新建两个 |

---

## 5. Net.Send 改造

```csharp
// 改造前
public static byte[]? Send(string ip, int port, IAsdu asdu)
{
    using IExchange exchange = CreateExchange(ip, port);  // 每次新建
    FrameCodec.SendFrame(exchange, asdu.GetContent());
    return FrameCodec.ReceiveFrame(exchange);
}

// 改造后
private static ConnectionPool? _pool;

public static byte[]? Send(string ip, int port, IAsdu asdu)
{
    _pool ??= new ConnectionPool(CreateExchange!);
    using ExchangeLease lease = _pool.UseClient(ip, port);  // 池化
    if (lease == null) return null;
    FrameCodec.SendFrame(lease.Exchange, asdu.GetContent());
    return FrameCodec.ReceiveFrame(lease.Exchange);
}
```

---

## 6. 对照 RemoteOps

| NetLab | RemoteOps | 说明 |
|--------|----------|------|
| `ExchangeLease` | `Extend<IExchange>` | `Helper/Extend.cs` |
| `PoolBucket` | （无独立类，内联在 Net.cs） | 教学拆出来，RemoteOps 是内联 List |
| `ConnectionPool` | `Net.cs:115` ConcurrentDictionary | RemoteOps 没有单独池类，全在 Net 静态字段 |
| `UseClient` | `Net.UseClient` | 逻辑几乎一致 |
| `Net.Send` | `Net.InternalSend` | 教学版无协商帧（RemoteOps 有） |

---

## 7. 动手练习

### 练习 1：验证池复用

```csharp
// RunPooledTest() 已经写好
// 10 次 Net.Send → 只会创建 1~2 个 TCP 连接
dotnet run --project Client
```

用 `netstat -an | findstr 9000` 观察实际 TCP 连接数。

### 练习 2：并发压力测试

```csharp
var tasks = new List<Task>();
for (int i = 0; i < 100; i++)
{
    tasks.Add(Task.Run(() =>
    {
        Net.Send("127.0.0.1", 9000, new BinaryAsdu(...));
    }));
}
await Task.WhenAll(tasks);
// 观察：100 个并发请求，实际只建了几个连接
```

### 练习 3：模拟连接断开

服务端重启后，客户端池里的旧连接 `IsConnected` 变 false。下一次 `UseClient` 会自动清理并新建。验证这一点。

### 练习 4：改用 Net.Send 替代直接 IExchange

把 `EchoClient.RunTest()` 里的直接 IExchange 方式改为用 `Net.Send` 逐条发送，对比区别。

---

## Phase 3 总结

```
Phase 2.1: IExchange 接口           → 不再依赖 NetworkStream
Phase 2.2: 委托注入                 → 全局切换连接方式
Phase 3:   连接池                   → 连接复用，不每次新建
```

三个 Phase 共同构成了 RemoteOps `Helper/Net.cs` 的完整教学拆解：

```
RemoteOps Net.cs:
  CreateClient 委托  ← Phase 2.2
  + UseClient 池     ← Phase 3
  + InternalSend     ← Phase 2.2 + Phase 3 合并
  + LengthToBytes    ← Phase 1.1（已拆到 FrameCodec）
```
