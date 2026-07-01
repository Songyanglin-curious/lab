<template>
  <div class="demo-container">
    <h2>响应式基础：ref / reactive 实验</h2>

    <!-- 实验 1：reactive 整体替换 -->
    <section class="experiment">
      <h3>实验 1：reactive 整体替换</h3>
      <p>姓名：{{ state.name }}</p>
      <button @click="changeName">单独改属性（有效）</button>
      <button @click="replaceState">整体替换（无效）</button>
      <p class="hint">💡 点击"整体替换"后页面不变，因为整体替换断掉了 Proxy 引用</p>
    </section>

    <!-- 实验 2：解构丢失 vs toRefs 拯救 -->
    <section class="experiment">
      <h3>实验 2：解构丢失 vs toRefs 拯救</h3>
      <p>直接解构的 name：{{ rawName }}</p>
      <p>toRefs 的 name：{{ refName }}</p>
      <button @click="changeRawName">改 rawName（无效）</button>
      <button @click="changeRefName">改 refName.value（有效）</button>
      <p class="hint">💡 rawName 只是普通 string，refName 是 Ref&lt;string&gt;，.value 走响应式</p>
    </section>

    <!-- 实验 3：ref 包对象 -->
    <section class="experiment">
      <h3>实验 3：ref 包对象</h3>
      <p>姓名：{{ user.name }}，年龄：{{ user.age }}</p>
      <button @click="updateUser">整体替换（有效）</button>
      <button @click="updateProperty">改属性（有效）</button>
      <p class="hint">💡 ref 包对象时，两种方式都触发更新，这就是 ref 方案的统一性</p>
    </section>

    <!-- 验证题 -->
    <section class="experiment">
      <h3>验证题：ref 方案实现</h3>
      <p>姓名：{{ person.name }}，年龄：{{ person.age }}</p>
      <button @click="replacePerson">整体替换为 Bob</button>
      <button @click="incrementAge">年龄 +1</button>
      <p class="hint">💡 用 ref 包对象，整体替换和属性修改都能正常工作</p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, toRefs, watch } from 'vue'

// ==================== 实验 1：reactive 整体替换 ====================
const state = reactive({ name: 'Alice' })

function changeName() {
  state.name = 'Bob'  // ✅ 属性改，有响应
}

function replaceState() {
  // 下面这行会报错：不能给 const 变量赋值
  // 即使绕过 const，整体替换也会丢失响应式
  // state = { name: 'Charlie' }  // ❌ 无效
  console.log('尝试整体替换：这行代码如果执行，state 还是 Alice，因为 Proxy 绑定在原对象上')
}

// ==================== 实验 2：解构丢失 vs toRefs 拯救 ====================
const state2 = reactive({ name: 'Alice', age: 20 })

// 方式 A：直接解构（丢失响应）
const { name: rawName } = state2

// 方式 B：toRefs（保持响应）
const { name: refName } = toRefs(state2)

function changeRawName() {
  // rawName 只是一个普通 string，修改它不会触发更新
  // rawName = 'Changed'  // 这行会报错，因为 rawName 是 const
  console.log('rawName 是普通 string，不是响应式')
}

function changeRefName() {
  refName.value = 'Changed'  // ✅ 有效，因为 refName 是 Ref<string>
}

// 监听验证
watch(
  () => rawName,
  (newVal) => {
    console.log('rawName 变化了:', newVal)  // 不会触发
  }
)

watch(refName, (newVal) => {
  console.log('refName 变化了:', newVal)  // 会触发
})

// ==================== 实验 3：ref 包对象 ====================
const user = ref({ name: 'Alice', age: 20 })

function updateUser() {
  user.value = { name: 'Bob', age: 25 }       // ✅ 整体替换有响应
}

function updateProperty() {
  user.value.name = 'Charlie'                   // ✅ 属性修改有响应（内部用了 reactive）
}

// ==================== 验证题：ref 方案实现 ====================
const person = ref({ name: 'Alice', age: 20 })

function replacePerson() {
  // 用 ref 包对象，整体替换完全没问题
  person.value = { name: 'Bob', age: 40 }
}

function incrementAge() {
  // 属性修改也没问题
  person.value.age++
}

/*
如果用 reactive 方案会遇到的问题：

1. 整体替换会丢失响应式：
   const personReactive = reactive({ name: 'Alice', age: 20 })
   personReactive = { name: 'Bob', age: 40 }  //  报错或丢失响应

2. 解构会丢失响应式：
   const { name } = personReactive  //  name 只是普通 string

3. 传递给子组件时，如果子组件接收 props，需要整个对象传过去
   而不是解构后分别传递，否则会丢失响应式

而用 ref 方案：
- person.value = newObj 
- person.value.name = 'xxx' 
- 解构需要用 toRefs，但 .value 让心智负担更低
*/
</script>

<style scoped>
.demo-container {
  max-width: 600px;
  margin: 20px auto;
  padding: 20px;
  font-family: sans-serif;
}

.experiment {
  margin-bottom: 30px;
  padding: 15px;
  border: 1px solid #eee;
  border-radius: 8px;
}

.experiment h3 {
  margin-top: 0;
  color: #42b883;
}

button {
  margin-right: 10px;
  padding: 8px 16px;
  background-color: #42b883;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

button:hover {
  background-color: #369970;
}

.hint {
  color: #666;
  font-size: 0.9em;
  margin-top: 10px;
}
</style>
