# C# 基础：delegate（委托）

> 排序：01 — 建议先掌握，后续理解 event、lambda、Func/Action 都依赖此概念。

---

## 速查卡片

```csharp
// 声明类型（定义签名）
delegate 返回值 类型名(参数列表);

// 声明变量
类型名 变量 = null;

// 赋值（指向方法）
变量 = (参数) => { 实现 };

// 调用
变量(实参);              // 语法糖
变量.Invoke(实参);       // 实际执行方式
```

---

## 一、它是什么

**delegate 是一个类型，用来描述"方法长什么样"——参数是什么、返回值是什么。**

- 它本身不包含实现，只定义签名
- 编译后生成一个类（继承 `System.MulticastDelegate`）
- 属于引用类型

---

## 二、四步使用流程

### 第 1 步：声明类型

```csharp
public delegate IExchange CreateClientFunc(string ip, int port);
//           ^返回类型  ^类型名            ^参数列表
```

这一步只做一件事：**告诉编译器"有这样一种函数类型"**。不分配内存，不创建实例。

类比：和声明 class 一样，只是定义了一个新类型的形状。

### 第 2 步：声明变量

```csharp
public static CreateClientFunc CreateClient = null;
//     ^static表示类级别  ^类型(第1步定义的)  ^变量名   ^初始无指向
```

分配一个引用，初始为 `null`（不指向任何方法）。此时调用会抛 `NullReferenceException`。

### 第 3 步：赋值（指向方法）

```csharp
CreateClient = new CreateClientFunc(某个已存在的方法);   // 写法A：显式构造
CreateClient = 某个已存在的方法;                         // 写法B：隐式转换
CreateClient = (ip, port) => { return new TLCPClient(); }; // 写法C：lambda
```

三种写法等价。lambda 是语法糖，编译时会自动生成一个私有方法。

### 第 4 步：调用

```csharp
IExchange conn = CreateClient("127.0.0.1", 9101);        // 写法A
IExchange conn = CreateClient.Invoke("127.0.0.1", 9101);  // 写法B：等价
```

---

## 三、关键特性

### 多播（一个 delegate 可以指向多个方法）

```csharp
CreateClient += (ip, port) => { Console.WriteLine("log"); return null; };
```

用 `+=` 可以链式挂多个方法，调用时**按顺序执行所有方法**，但只有最后一个方法的返回值会被返回。用 `-=` 移除。

### 闭包（lambda 可以捕获外部变量）

```csharp
string path = "d:/cert";
CreateClient = (ip, port) => {
    // 可以访问 path，编译器自动生成一个闭包类持有 path
    LoadCert(path);
};
```

---

## 四、内置泛型版本（不用自定义 delegate）

| 内置类型 | 等价于 | 用途 |
|----------|--------|------|
| `Action` | `delegate void XX();` | 无返回值 |
| `Action<T1, T2>` | `delegate void XX(T1, T2);` | 无返回值，有参数 |
| `Func<T1, T2, TResult>` | `delegate TResult XX(T1, T2);` | 有返回值，最后一个泛型是返回类型 |

```csharp
// 自定义 delegate（项目中用的方式）
public delegate IExchange CreateClientFunc(string ip, int port);

// 用内置 Func 等价替代
Func<string, int, IExchange> CreateClient = null;
```

项目用自定义 delegate 是为了**语义明确**——`CreateClientFunc` 比 `Func<string, int, IExchange>` 表意清晰。

---

## 五、与本项目的关系

在 `services/YCYWDataExchange/YCYWDataExchange/Helper/Net.cs` 中：

```csharp
// 第 111 行 — 声明类型
public delegate IExchange CreateClientFunc(string ip, int port);

// 第 113 行 — 声明静态变量，作为全进程的"连接工厂"入口
public static CreateClientFunc CreateClient = null;
```

这是一个**策略模式的实现**——运行时替换 `CreateClient` 的指向，就能切换全进程的连接方式（明文 TCP 或 TLCP 加密）：

```csharp
// 明文模式
CreateClient = TCPClient.RegisterCreateClient;

// 加密模式
CreateClient = TLCPClient.RegisterCreateClient;
```

---

## 六、常见混淆点

| 易混 | 区别 |
|------|------|
| delegate vs class | delegate 定义方法签名，class 定义对象结构 |
| delegate vs interface | interface 约束类的能力（多方法），delegate 约束单个方法签名 |
| delegate vs event | event 是 delegate 的包装，限制外部只能 `+=`/`-=`，不能直接赋值和调用 |
| `delegate` 关键字 vs `Func<>` | `delegate` 自定义命名类型，`Func<>` 是泛型快捷方式，本质一样 |
| 声明 vs 实例 | `delegate void F(int x)` 是声明类型；`F f = ...` 是创建实例 |
