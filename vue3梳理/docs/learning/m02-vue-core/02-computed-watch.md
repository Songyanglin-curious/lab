# 2.2 计算与侦听：computed / watch / watchEffect

## 参考文档

- [Vue3 官方 - 计算属性](https://cn.vuejs.org/guide/essentials/computed.html)
- [Vue3 官方 - 侦听器](https://cn.vuejs.org/guide/essentials/watchers.html)

---

## 你的原始理解

> 留空 — 在开始之前，先用自己的话写下你对 computed / watch / watchEffect 的现有认知。

```
computed 我的理解就是getter，它不是用来做其他的值的生成和业务的 他是用其他响应式来计算出自身的值的
watch 就是监听响应式标量是否被修改  或者说是触发 ，但是我已经忘记了它会不会防抖了
watchEffect 是watch的自动挡  会自动收集回调里面的响应式变量 进行监听，我就记得这点了。
```

---

## 讲解

### 1. computed：缓存机制是核心

很多人以为 `computed` 只是"把逻辑抽出来"。真正的区别在于：**method 每次访问都重新计算，computed 只有依赖变化才重新计算**。

```ts
const firstName = ref('John')
const lastName = ref('Doe')

// 方法 — 每次调用都执行
function fullNameMethod() {
  console.log('method 执行了')
  return firstName.value + ' ' + lastName.value
}

// 计算属性 — 缓存结果，只在依赖变化时重新计算
const fullNameComputed = computed(() => {
  console.log('computed 执行了')
  return firstName.value + ' ' + lastName.value
})
```

如果你在模板里多次引用 `fullNameMethod()`，每次渲染都会打印 `method 执行了`；而 `fullNameComputed` 只在 `firstName` 或 `lastName` 变化时才重新执行。

**computed 默认只读**，写入需要显式提供 setter：

```ts
const fullName = computed({
  get: () => firstName.value + ' ' + lastName.value,
  set: (val) => {
    const parts = val.split(' ')
    firstName.value = parts[0]
    lastName.value = parts[1]
  }
})
```

**禁区**：computed 里不要放副作用（网络请求、DOM 操作、日志），那是 watch 的活。

---

### 2. watch vs watchEffect：两个侦听器的决策表

|          | watch                                  | watchEffect                                  |
| -------- | -------------------------------------- | -------------------------------------------- |
| 追踪方式 | 手动指定侦听源                         | **自动追踪**回调里用到的所有响应式数据 |
| 立即执行 | 默认不执行，加 `{ immediate: true }` | **默认立即执行一次**                   |
| 获取旧值 | ✅`(newVal, oldVal)`                 | ❌ 回调里拿不到旧值                          |
| 适用场景 | 知道要侦听谁，需要比较新旧值           | 不知道依赖谁，让系统自动跟踪                 |

```ts
// watch — 明确告知侦听源
watch(x, (newVal, oldVal) => {
  console.log(`x 从 ${oldVal} 变成 ${newVal}`)
})

// watchEffect — 自动收集依赖
watchEffect(() => {
  // 只要用到的响应式数据变化，就重新执行
  console.log(`x: ${x.value}, y: ${y.value}`)
})
```

**watchEffect 自动跟踪的本质**：它会在第一次执行时"记录"所有被读取的响应式数据，之后这些数据任一变化都会触发回调。

---

### 2.1 用同一个业务场景对比

用一个用户信息编辑页做对比：三个 ref `name`、`email`、`phone`，任一变化就重新验证表单。

```ts
const name = ref('')
const email = ref('')
const phone = ref('')

// ===== watch 方案：手动列出三个源 =====
watch([name, email, phone], () => {
  console.log('watch: 验证表单...')
})

// ===== watchEffect 方案：自动读取，Vue 自己记依赖 =====
watchEffect(() => {
  console.log('watchEffect: 验证', name.value, email.value, phone.value)
})
```

表面上看都一样，区别在三个地方暴露：

**区别 1：拿不拿旧值**

```ts
// ✅ watch — 有 newVal/oldVal
watch(keyword, (newVal, oldVal) => {
  if (newVal === oldVal) return        // 防重复请求
  fetch(`/api/search?q=${newVal}`)
})

// ❌ watchEffect — 拿不到旧值
watchEffect(() => {
  fetch(`/api/search?q=${keyword.value}`)  // 相同关键词也会重复发
})
```

**区别 2：副作用时机控制（防抖）**

```ts
// ✅ watch — 回调里可以"等一等"
watch(keyword, (newVal) => {
  clearTimeout(timer)
  timer = setTimeout(() => api(newVal), 500)
})

// ❌ watchEffect — 变了就立刻跑，防抖逻辑无处安放
watchEffect(() => {
  api(keyword.value)  // 每次输入都立刻触发，没有"等"的空间
})
```

**区别 3：首次是否执行**

```ts
// watch — 默认不跑，你需要时加 immediate
watch(id, fetchDetail)                       // 等 id 变了再拉
watch(id, fetchDetail, { immediate: true })  // 一进页面就拉

// watchEffect — 立刻跑，少写 immediate，但要确认初始值有意义
watchEffect(() => fetchDetail(id.value))
```

**总结：不是"单变量 vs 多变量"**

| 你需要的 | 选 |
|----------|-----|
| 对比新旧值 | watch |
| 延迟执行（防抖） | watch |
| 灵活控制首次是否执行 | watch |
| 一堆变量变了都要做同一件事 | watchEffect（少写列表） |
| 进页面就要跑一次 | watchEffect（省 `immediate`） |

---

### 3. 常见误区

**误区 1：用 computed 发起请求**

```ts
// ❌ 错误 — computed 不应该有副作用
const userList = computed(async () => {
  const res = await fetch('/api/users')
  return res.json()
})
```

**误区 2：watchEffect 里修改正在侦听的变量导致无限循环**

```ts
// ❌ 危险 — 修改了自己侦听的变量
watchEffect(() => {
  count.value = count.value + 1
})
```

**误区 3：watch 侦听 reactive 对象属性时忘记用 getter 函数**

```ts
const state = reactive({ count: 0 })

// ❌ 错误 — 直接传 state.count，侦听的是值（0）
watch(state.count, (newVal) => { /* 不会触发 */ })

// ✅ 正确 — 用 getter 函数
watch(() => state.count, (newVal) => { /* 正常触发 */ })
```

**误区 4：以为 watchEffect 什么都不侦听**

```ts
// ❌ watchEffect 不会侦听普通变量
let x = 0
watchEffect(() => { console.log(x) })  // 只执行一次，x 变化不会触发
```

---

## 动手练习：在 Vue3Atlas 中创建试题

在 `src/components/` 下创建 `ComputedWatchExercises.vue`，**不要直接写成品代码，而是按照下面每道题的描述自己完成实现**。

每道题用一个独立的区域展示。

---

### 试题 1：computed 缓存 vs method

**要求**：

1. 声明一个 `ref` 数字 `base = ref(5)`
2. 写一个 method `doubleMethod()`，返回 `base * 2`，内部 `console.log('method called')`
3. 写一个 computed `doubleComputed`，返回 `base * 2`，内部 `console.log('computed called')`
4. 在模板里各引用 **3 次**
5. 再加一个按钮可以修改另一个无关的 `ref` 变量（比如 `other`），点击后触发重新渲染

**自检**：点击"修改无关变量"后，控制台分别打印几次 `method called` 和 `computed called`？为什么？

method 3次

computed 1次

触发渲染后 

method 3次

computed 0次

因为computed计算的依赖（base）没有变化,所以其本身没有变化，method 会每次都进行触发的。

---

### 试题 2：computed 可写

**要求**：

1. 声明 `firstName` 和 `lastName` 两个 ref
2. 写一个可写的 computed `fullName`，有 getter 和 setter
3. 模板中显示 `{{ fullName }}`，加一个输入框允许用户输入全名，@change 时写入 `fullName`
4. 当用户在输入框输入 `"Jane Smith"` 回车后，`firstName` 和 `lastName` 分别变成什么？
   我不知道你想问啥 但是这个getter  setter拦截器有什么问题吗水到渠成的拆分与赋值

---

### 试题 3：watch 基本用法 — 新旧值对比

**要求**：

1. 声明 `count = ref(0)`
2. 用 `watch` 侦听 `count`，回调打印 `"count: oldVal -> newVal"`
3. 加两个按钮：`count++` 和 `count--`
4. 加一个按钮 `reset` 把 `count` 设为 0，观察：reset 时打印的 oldVal 是什么？
   reset 时 依旧式上一个值

---

### 试题 4：watchEffect 自动收集依赖

**要求**：

1. 声明 `a = ref(1)`，`b = ref(2)`
2. 用 `watchEffect` 打印 `"a + b = ${a + b}"`
3. 加三个按钮分别修改 a、b、和一个无关的 `c = ref(0)`
4. 观察：点击修改 c 时 watchEffect 会触发吗？为什么？

`c又没有被收集响应依赖当然不会触发。`

---

### 试题 5：watch vs watchEffect 场景辨析

**要求**：不用写代码，在注释里回答以下问题：

```
场景 A：用户输入搜索关键词，需要发请求获取搜索结果。应该用 watch 还是 watchEffect？为什么？
watch 原因：1.单个值所以两者都可以，2.从业务角度来看 不应该立刻执行，所以选择watch
场景 B：一个页面上有三个独立的状态（a, b, c），
只要其中任何一个变化，就要把它们的值写入 localStorage。
应该用 watch 还是 watchEffect？为什么？
watchEffect 因为是多个值  watchEffect 更方便
```

---

### 试题 6：排查错误 — 侦听 reactive 属性

下面的代码有问题，请修复并说明原因：

```ts
// ❌ 有 bug — 改 state.count 时为什么 watch 不触发？
const state = reactive({ count: 0 })
watch(state.count, (newVal) => {
  console.log('count changed to', newVal)
})
```

watch 侦听 reactive 对象属性时忘记用 getter 函数；但是为啥我还真不知道

**解答**：

`watch(state.count, cb)` 不触发的原因是 **传进去之前就已经求值了**：

- `state.count` 在传给 `watch` 前被立即求值成 `0`
- `watch(0, cb)` — 拿到的只是一个数字 `0`，不是响应式对象，没有 Proxy 可拦截
- 依赖搜集为空，以后永远不会触发

`watch(() => state.count, cb)` 能触发的原因是 **传进去的是一段还没跑的代码**：

- Vue 拿到的是一个函数，不会立即求值
- `watch` 内部先用 `effect` 机制开启"依赖搜集模式"
- 在这个模式下调用 `() => state.count` → 函数执行 → 读到 `state.count` → Proxy 的 `get` 拦截 → 依赖被登记
- 以后 `state.count` 变化 → `Proxy` 的 `set` 触发 → 通知依赖 → 回调执行

结论：getter 角色不是函数本身决定的，而是由 **谁、在什么上下文里调用它** 决定的。`watch` 底层和 `watchEffect` 用的是同一个 effect 机制。`watch` 的第一个参数可以传 ref、reactive 对象、getter 函数、或以上三者的数组——底层都是把它们放进 effect 去读依赖。

---

### 试题 7（选做）：watchEffect 防抖

**要求**：

1. 声明 `keyword = ref('')`
2. 用 `watchEffect`（或 `watch`）实现：当 keyword 变化时，**防抖 500ms** 后再打印 `"搜索: ${keyword}"`
3. 快速连续输入时，只打印最后一次

提示：你可能需要同时用到 `watch` 和定时器，想一想 `watchEffect` 在这个场景是否合适。

---

## 验证题

做完以上试题后，回答以下三个问题（写注释即可）：

```
1. 什么情况下 computed 比 method 更合适？（不只说"性能"，要说出 cache 的触发条件）
大多数情况下computed都比method的合适 常用的逻辑就是根据其他响应式数据按照一定的逻辑计算得到一个新的值 并且无副作用如请求  操作dom等
2. watch 和 watchEffect 各自的"最佳适用场景"分别是什么？各举一个真实业务例子。
watch适合但个变量
watchEffect适合多个变量
最佳场景我这还真没有 多数情况下两个可以相互转变
3. Vue3 的 `watchEffect` 和 React 的 `useEffect` 在设计理念上有什么不同？
不了解React 的 `useEffect`

**解答**：

本质区别：**谁负责追踪依赖**。

- React：你手动列出 `[a, b, c]`，忘了 → 闭包过期，多写 → 不必要执行
- Vue：Proxy 自动拦截，回调第一次执行时自动记录所有被读取的响应式数据

更深一层：

| | useEffect | watchEffect |
|---|---|---|
| 触发 | 渲染后执行 | 依赖变化时立即执行 |
| 依赖追踪 | 手动声明数组 | 自动 Proxy 拦截 |
| 与渲染的关系 | 是渲染生命周期的一部分 | 和渲染无关，纯响应式 |
| 心智模型 | "渲染后同步到外部世界" | "数据变了就做这件事" |

React 的 effect 跑在渲染的"尾巴"上，Vue 的 watchEffect 跑在数据的"源头"上。所以 Vue 不需要 `useCallback`/`useMemo` 那套——响应式系统是细粒度的推送，不是粗粒度的重渲染后扩散。

**AI 时代视角**：框架越少让开发者（包括 AI）手工登记依赖，生成代码的正确率越高。React effect 的依赖数组是 Copilot/Cursor 最常见 bug 来源之一，Vue 没有这个入口，这个 bug 根本不存在。
```

做完后，把关键代码和控制台观察结果发给我。

---

## 备注

- 每个试题都是独立的代码块，互不干扰
- 重点不是一次性全做对，而是观察和理解行为差异
- 试题 7 不做也没关系，后面的业务场景会自然遇到
