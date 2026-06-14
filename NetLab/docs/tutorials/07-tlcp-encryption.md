# Phase 5 — 加密通道透明替换

> 目标：在 IExchange 之上包一层加密，业务代码 / FrameCodec / Channel 一行不改。
>
> ⚠️ 本阶段学的是**加密通道替换模式**，不是真实 TLCP 密码细节（SM2/SM3/SM4/握手状态机）。
> SecureExchange 类比 TLCPClient 的**架构位置**，不等价于其加密实现。

---

## 1. 问题引入

Phase 4 的业务分发跑得很好，但数据走的是明文 TCP。站端之间走公网时，明文不可接受。

需要加密。但下面这些代码**一行都不该改**：

```csharp
// 业务端（Net.Connect / Net.Send）
FrameCodec.SendFrame(exchange, msg.GetContent());
byte[] resp = FrameCodec.ReceiveFrame(exchange);

// 服务端（IChannel.Run）
byte[] data = FrameCodec.ReceiveFrame(exchange);
FrameCodec.SendFrame(exchange, response);
```

---

## 2. 方案：IExchange 装饰器

Phase 2 的接口抽象在此兑现价值：

```
业务代码（Net.Send / Net.Connect / IChannel）
  ↓
FrameCodec.SendFrame / ReceiveFrame
  只调 exchange.Send(byte[]) / exchange.ReadExactly(int)
  ↓
SecureExchange           ← 加密/解密，IExchange 接口
  ↓
StreamExchange           ← 发/收字节，IExchange 接口
  ↓
TCP
```

关键：每一层只知道自己调用的是 `IExchange`，不知道上层/下层是谁。

---

## 3. SecureExchange 实现

```csharp
public class SecureExchange : IExchange
{
    private readonly IExchange _inner;

    public SecureExchange(IExchange inner)
    {
        _inner = inner;
        // 不做握手——避免客户端和服务端同时等对方先发而卡死
        // 真实 TLCP 有 13 步握手状态机，教学阶段只演示加密包装模式
    }

    public void Send(byte[] data)
    {
        byte[] encrypted = Xor(data);
        _inner.Send(encrypted);
    }

    public byte[]? ReadExactly(int count)
    {
        byte[]? encrypted = _inner.ReadExactly(count);
        if (encrypted == null) return null;
        return Xor(encrypted);   // 同 key 再 XOR 一次 = 解密
    }

    public bool IsConnected => _inner.IsConnected;
    public void Close() => _inner.Close();
    public void Dispose() => _inner.Dispose();

    private static byte[] Xor(byte[] data)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ 0x55);
        return result;
    }
}
```

设计决策：

| 决策 | 原因 |
|------|------|
| 不做握手 | 真实握手涉及客户端/服务端角色协商，教学绕过 |
| XOR 而非 SM4 | 可观察——加密后 EB90 变值，肉眼看出帧不再是明文 |
| 同 key 再 XOR = 解密 | XOR 对称性天然适合教学 |
| 构造不调阻塞方法 | 避免两端同时等对方先发 |

---

## 4. 两端同时切换

### 客户端（一行）

```csharp
// 明文
Net.CreateExchange = (ip, port) => {
    TcpClient c = new TcpClient(ip, port);
    return new StreamExchange(c.GetStream());
};

// 加密 —— 只改 return 下一行的包装
Net.CreateExchange = (ip, port) => {
    TcpClient c = new TcpClient(ip, port);
    return new SecureExchange(new StreamExchange(c.GetStream()));
};
```

### 服务端（同行）

```csharp
// 明文
BusinessServer server = new BusinessServer(9000);

// 加密
BusinessServer server = new BusinessServer(9000);
server.ExchangeWrapper = ex => new SecureExchange(ex);
```

关键：**两端必须同时切**，否则一端发加密帧、另一端按明文读 EB90，报帧头错误。

---

## 5. 验证加密生效

加密后服务端日志正常工作，说明密文被正确解密：

```
服务端日志：
  BusinessServer 已启动，监听 0.0.0.0:9000
  加密模式：开启
  新连接：127.0.0.1:xxxxx
  协商成功：Manage
  EchoChannel 收到：12 字节 → 加密消息
  EchoChannel 回显 12 字节
```

用 Wireshark 抓包对比——明文看到 `EB 90`，加密后看到 `BE C5`（`0xEB ^ 0x55 = 0xBE`，`0x90 ^ 0x55 = 0xC5`）。

---

## 6. 对照 RemoteOps

RemoteOps 真实的加密实现要复杂得多：

```
NetLab 教学模拟              RemoteOps 真实实现
─────────────────────        ─────────────────────
SecureExchange                TLCPDataExchange.TLCPClient  ← 位置相同
(StreamExchange)              (Ysh.Tlcp.TlcpClient)

XOR 异或                       SM4-CBC 分组加密 + SM3-HMAC     ← 算法不同
无握手                           13 步 SM2 签名/加密握手        ← 复杂度不同
self.Dispose()                 self.Dispose() + 发 CloseNotify  ← 安全关闭

一层包装(装饰器)                同上，模式一致
委托注入切换                     同上，机制一致
```

表中"位置相同"的意思是：两者都是 IExchange 装饰器，都包装底层连接，都对上层透明。

---

## 7. 动手练习

### 练习 1：观察加密效果

```powershell
dotnet run --project Server   # 加密已开启
dotnet run --project Client   # RunSecureTest
```

### 练习 2：注释掉加密

`Server/Program.cs` 注释掉 `server.ExchangeWrapper = ...` 行，不改客户端，观察"帧头错误"报错。

### 练习 3：恢复明文

两端都去掉 SecureExchange 包装，恢复明文通信。验证：只需改 CreateExchange 和 ExchangeWrapper 两处，业务代码不动。

### 练习 4：换 XOR key

把 `0x55` 换成 `0xAA`，两端同时改，验证依然通。

---

## Phase 1~5 全貌

```
Phase 1: FrameCodec          ← 帧协议，解决粘包
Phase 1: ASDU/APDU 分离      ← 消息体与传输帧解耦
Phase 2: IExchange 接口      ← 不再依赖 NetworkStream
Phase 2: 委托注入             ← 全局切换连接方式
Phase 3: 连接池              ← 连接复用（仅 Equip 内部通道）
Phase 4: 业务协商/Channel     ← 同端口多业务分发
Phase 5: SecureExchange      ← 加密通道透明替换
```

这 5 个 Phase 共同构成 RemoteOps `YCYWDataExchange + TLCPDataExchange` 的核心抽象教学。
