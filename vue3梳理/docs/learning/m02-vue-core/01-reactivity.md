# 2.1 响应式基础：ref / reactive

## 参考文档

- [Vue3 官方 - 响应式基础](https://cn.vuejs.org/guide/essentials/reactivity-fundamentals.html)

---

## 你的原始理解

> 1. 对对象使用响应式包装，使用 .value 获取值。任何情况都可以用 ref。
> 2. proxy 对象，一般对对象和表单使用。但很容易把响应式写丢，所以统一用 ref。
> 3. 统一用 ref。reactive 容易写丢，接收多层数据很麻烦，ref 能实现 reactive 所有能力。
> 4. 模板不需要 .value。
> 5. ref 可以包对象，reactive 不可以包基本类型。
> 6. 响应丢失，尤其是 props 传递时。

---

## 讲解

### 1. ref 的真实原理

你以为 ref 只负责一个"响应式包装壳"。实际上有两层：

**包基本类型（string/number/boolean）**：

```ts
// ref('hello') 等价于：
{ value: 'hello' }  // value 挂 getter/setter，值变时触发更新
```

**包对象时**：

```ts
const user = ref({ name: 'Alice' })
// ref 内部对 { name: 'Alice' } 调用了 reactive() 做深层响应
// user.value.name = 'Bob' → 触发更新
```

结论：**ref 不是替代 reactive，而是包对象时内部就用了 reactive**。区别只在外壳。

---

### 2. 模板自动解包（你的记忆是对的）

```vue
<template>
  {{ count }}     <!-- 不用 .value，编译器自动做 -->
</template>

<script setup>
const count = ref(0)
console.log(count.value)  // script 里必须 .value
</script>
```

规则：**模板中顶层 ref 自动解包 `.value`，script 中必须手动写**。

但如果 ref 包在对象里就不是顶层了，不会自动解包：

```vue
<template>
  {{ state.count }}  <!-- 对象.属性 不是顶层 ref，不自动解 -->
</template>
```

---

### 3. reactive 为什么“丢响应式”（重点）

**坑 1：直接替换整个对象**

```ts
const state = reactive({ name: 'Alice' })

state.name = 'Bob'       // ✅ 属性改，有响应
state = { name: 'Bob' }  // ❌ 整体替换，断掉 Proxy 引用
```

原因：`reactive()` 返回的 Proxy 绑定在**最初传入的那个对象**上。你把变量指向了新对象，Proxy 还在旧对象上，新对象没有响应。

**坑 2：解构**

```ts
const state = reactive({ name: 'Alice', age: 20 })

const { name } = state       // ❌ name 只是一个普通 string，不是响应式
const name = state.name      // ❌ 同上
```

原因：解构等于取出原始值，脱离了 Proxy。

**修复方式**：用 `toRefs()` 把每个属性转成 ref：

```ts
const { name } = toRefs(state)  // ✅ name 是 Ref<string>，.value 走响应式
```

**坑 3：展开传给函数**

```ts
const state = reactive({ name: 'Alice' })

function doSomething(data: { name: string }) {
  data.name = 'Bob'  // ❌ 不触发更新，data 是普通对象
}

doSomething({ ...state })  // 展开操作丢失 Proxy
```

这三条加在一起，就是你说的"容易把响应式写丢"——根本原因都是**脱离了 Proxy 引用链路**。

---

### 4. 为什么推荐统一用 ref

| 场景 | reactive | ref |
|------|----------|-----|
| 基本类型 | ❌ 不支持 | ✅ |
| 对象整体替换 | ❌ 丢失响应 | ✅ `xxx.value = newObj` |
| 解构 | ❌ 丢失响应 | ❌ 也一样，但 `toRefs()` 两边都能救 |
| 函数传参 | ❌ 容易掉 | `.value` 明确标记，不容易忘 |
| `watch` 侦听 | ✅ 直接 `watch(state, ...)` | 需要**深层 watch**：`watch(refObj, ..., { deep: true })` |

ref 的唯一额外成本：script 里多写 `.value`。换来的是心智负担大幅降低——不用担心解构、替换等所有 Proxy 链路断裂问题。

---

### 5. reactive 还有用吗？

极少场景，但不是零：

- **表单对象**：表单字段固定、不会整体替换，解构场景少
- **不需要整体替换的对象**：比如一个全局 store 里的状态树

Vue 官方文档的倾向：不禁止 reactive，但推荐 ref 为主。社区主流也是这个方向。

---

## 动手练习

在 `Vue3Atlas` 项目中创建 `src/components/ReactivityDemo.vue`，完成以下实验。

### 实验 1：reactive 整体替换

```vue
<template>
  <div>
    <p>姓名：{{ state.name }}</p>
    <button @click="changeName">单独改属性（有效）</button>
    <button @click="replaceState">整体替换（无效）</button>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue'

const state = reactive({ name: 'Alice' })

function changeName() {
  state.name = 'Bob'  // ✅
}

function replaceState() {
  state = { name: 'Charlie' }  // ❌ 无效。看控制台，这里应该直接报错：不能赋值给 const
}
</script>
```

**预期结果**："整体替换" 按钮点击后，页面不变。然后思考：为什么？

### 实验 2：解构丢失 vs toRefs 拯救

```vue
<script setup lang="ts">
import { reactive, toRefs } from 'vue'

const state = reactive({ name: 'Alice', age: 20 })

// 方式 A：直接解构
const { name: rawName } = state

// 方式 B：toRefs
const { name: refName } = toRefs(state)

// 手动修改 rawName 和 refName，或用 watch 验证
</script>
```

在浏览器里用 Vue DevTools 或 `watch` 验证：改 `refName.value` 页面有反应，改 `rawName` 没有。

### 实验 3：ref 包对象

```vue
<script setup lang="ts">
import { ref } from 'vue'

const user = ref({ name: 'Alice', age: 20 })

function updateUser() {
  user.value = { name: 'Bob', age: 25 }       // ✅ 整体替换有响应
}
function updateProperty() {
  user.value.name = 'Charlie'                   // ✅ 属性修改有响应（内部用了 reactive）
}
</script>
```

**预期结果**：两种方式都触发更新。这就是 `ref` 方案的统一性。

---

## 验证题

在你的 `ReactivityDemo.vue` 里，完成下面这个场景并回答：

```
有一个对象 { name: 'Alice', age: 20 }，需要：
1. 在模板上显示 name 和 age
2. 有一个按钮可以整体替换这个人（改成 { name: 'Bob', age: 40 }）
3. 把 name 单独传给一个子组件展示

实现一份用 ref 方案，再想想用 reactive 方案会遇到什么问题。（只写 ref 方案也 OK，但要在注释里写清楚如果用 reactive 哪里会踩坑）
```

做完把 `ReactivityDemo.vue` 的代码发给我。
