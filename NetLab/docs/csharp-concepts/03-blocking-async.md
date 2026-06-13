# C# 基础：阻塞与异步

> 排序：03 — 理解 `AcceptTcpClient()` 为什么卡住、`await` 为什么不停。

---

## 速查卡片

```csharp
// 阻塞 — 线程睡在那行等结果
byte[] data = stream.Read(buffer, 0, 1024);           // 卡住，数据到了才返回

// 异步 — 线程先行返回，有结果了 CLR 再安排线程继续
byte[] data = await stream.ReadAsync(buffer, 0, 1024); // 不卡线程，数据到了往下走

// async Task Main — 运行时等返回的 Task 完成才退出进程
static async Task Main() { await Task.Delay(5000); }   // 5秒后才退出
static void Main() { Task.Delay(5000); }               // 立刻退出
```

---

## 一、是什么

**阻塞** — 调一个函数，条件不满足函数不返回，当前线程被 OS 挂起（WAITING 状态），不消耗 CPU。条件满足后线程被唤醒，函数返回。

**异步** — 调一个函数，线程立即返回干别的事。底层 OS + CLR 在后台等结果，结果到了安排线程从断点继续执行。

---

## 二、阻塞的内部过程

以 `stream.Read(buffer, 0, 1024)` 为例：

1. 调用进入 C# 库 → C# 库调用 OS 的 `recv()` 函数
2. OS 查该 socket 的接收缓冲区，发现是空的
3. OS 把当前线程标记为 WAITING，CPU 调度器不再给它分配时间片
4. 线程挂起，**CPU 去执行其他线程，不是空转轮询**
5. 数据到达 → 网卡中断 → OS 拷贝数据到缓冲区 → 标记线程 READY
6. CPU 下次调度时，线程从 `Read` 返回，拿到数据

**关键：程序"卡住"是 OS 故意让线程睡着的，不是死循环。**

## 三、异步的内部过程（编译器做了什么）

你写的：

```csharp
byte[] data = await stream.ReadAsync(buffer, 0, 1024);
```

编译器大致生成：

```csharp
var task = stream.ReadAsync(buffer, 0, 1024);
// 当前方法剩余代码 → 打包成回调，注册到 task 上
task.ContinueWith(t => { /* 从这行继续执行 */ });
return task;
```

`await` = "把后面的代码变成回调 + 把 Task 抛给调用方"。运行时拿到 Task，检查是否完成。没完成就等（不阻塞调用方的线程）。

## 四、为什么 `async Task Main` 进程不退出

```csharp
static async Task Main()
{
    await Task.Delay(5000);  // 5秒后进程才退出
}
```

运行时调用 `Main()`，拿到的不是 `void` 而是 `Task`。运行时检查这个 Task：

- Task 已完成 → 退出进程
- Task 未完成 → 等着，进程不退出

而 `static void Main()` + 不 await 的写法，`Main` 立刻返回 → 进程退出 → 异步操作被中止。

## 五、对比

| | 阻塞 | 异步 |
|------|------|------|
| 线程去哪了 | 睡在那一行（WAITING） | 立即返回，不睡觉 |
| 谁在等 | 线程在等 | OS 在等；CLR 管调度 |
| 结果到了 | 线程醒来继续 | CLR 找线程从断点跑 |
| CPU 消耗 | 0（真睡觉） | 0（也不空转） |
| 代码风格 | 顺序阅读，直观 | 需要 async/await，但底层是回调 |
| 一个线程处理多个连接 | ❌ 一个线程只能等一件事 | ✅ 一个线程可以管很多连接 |

## 六、与项目的关系

原项目大部分使用**同步（阻塞）**写法：

- `listener.AcceptTcpClient()` — Server.cs / EquipTCPServer.cs
- `stream.Read()` — TCPClient.cs:59、TLCPClient.cs:160
- `APDU.Read()` — 所有接收处
- `Net.UseClient()` → `CreateClient()` — Net.cs:140

唯一的异步用法是 `EquipChannel.Run()` 第 28 行：

```csharp
Task.Run(() => { DataReceived(data); });
```

收到消息后，**业务处理**异步执行（不阻塞下一帧的接收），但 **收帧本身**是同步阻塞的。

---

## 七、常见混淆点

| 易混 | 区别 |
|------|------|
| 阻塞 = 卡死 | 阻塞线程被 OS 挂起，CPU 跑别的线程，不卡死 |
| `await` = 线程在等 | `await` 后线程返回了，没在等；CLR 在等 |
| `async` = 多线程 | async 启动的是异步操作，不保证新线程；很多异步是单线程 IO 完成 |
| `task` vs `thread` | Task 是"一件要做的事"，Thread 是"做事的工人" |
| 异步一定能提高性能 | 对 IO 密集有帮助（少占线程），对 CPU 密集没用 |
