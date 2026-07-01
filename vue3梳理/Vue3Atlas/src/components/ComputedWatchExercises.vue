<template>
  <div class="demo-container">
    <h2>响应式基础：ref / reactive 实验</h2>

    <!-- computed 缓存 -->
    <!-- 试题 1：computed 缓存 vs method -->
    <section class="experiment">
        <h3>试题 1：computed 缓存 vs method</h3>
        <p>other: {{ other }}</p>

        <p>doubleMethod: {{ doubleMethod() }}</p>
        <p>doubleMethod: {{ doubleMethod() }}</p>
        <p>doubleMethod: {{ doubleMethod() }}</p>

        <p>doubleComputed: {{ doubleComputed }}</p>
        <p>doubleComputed: {{ doubleComputed }}</p>
        <p>doubleComputed: {{ doubleComputed }}</p>
        <button @click="changeOther">触发重新渲染</button>
    </section>

    <!-- 试题 2：computed 可写 -->
    <section class="experiment">
      <h3>试题 2：computed 可写</h3>
      <p>firstName: {{ firstName }}</p>
      <p>lastName: {{ lastName }}</p>
      <p>fullName: {{ fullName }}</p>
      <input
        :value="fullName"
        @change="onFullNameChange"
        placeholder="输入全名，如 Jane Smith"
      />
      <!-- 
        问题：输入 "Jane Smith" 并回车后，
        firstName 和 lastName 分别变成什么？
      -->
    </section>

    <!-- 试题 3：watch 基本用法 — 新旧值对比 -->
    <section class="experiment">
      <h3>试题 3：watch 新旧值对比</h3>
      <p>count: {{ count }}</p>
      <button @click="increment">count++</button>
      <button @click="decrement">count--</button>
      <button @click="reset">reset</button>
      <!-- 
        问题：点击 reset 时，console 打印的 oldVal 是什么？
      -->
    </section>

    <!-- 试题 4：watchEffect 自动收集依赖 -->
    <section class="experiment">
      <h3>试题 4：watchEffect 自动收集依赖</h3>
      <p>a: {{ a }}, b: {{ b }}, c: {{ c }}</p>
      <button @click="changeA">修改 a</button>
      <button @click="changeB">修改 b</button>
      <button @click="changeC">修改 c</button>
      <!-- 
        问题：点"修改 c"时 watchEffect 会触发吗？为什么？
      -->
    </section>


      <section class="experiment">
      <h3>keyword</h3>

      <input
        :value="keyword"
        @change="onKeyWordChange"
        placeholder="输入全名，如 Jane Smith"
      />
      <!-- 
        问题：输入 "Jane Smith" 并回车后，
        firstName 和 lastName 分别变成什么？
      -->
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, watchEffect } from 'vue'

const base = ref(5);
const other = ref(5);
function changeOther(){
    other.value = 6;
}

// 方法 — 每次调用都执行
function doubleMethod() {
    console.log('method 执行了')
   return base.value*2;
}

// 计算属性 — 缓存结果，只在依赖变化时重新计算
const doubleComputed = computed(() => {
  console.log('computed 执行了')
  return base.value *2;
})

// ==================== 试题 2：computed 可写 ====================
const firstName = ref('John')
const lastName = ref('Doe')

// TODO: 把下面这个 computed 改成可写的（加上 setter）
// 理解computed 本质 get  set 操作而已，但是常规用法还是 get 多个值聚合
const fullName = computed({
    get:() => {
        return firstName.value + ' ' + lastName.value
    },
    set:(v)=>{
        if(!v)return;
        const splitArr = v.split(" ");
        firstName.value = splitArr[0] || "";
        lastName.value= splitArr[1] || "";
    }
})

function onFullNameChange(e: Event) {
  const target = e.target as HTMLInputElement
  fullName.value = target.value;
  // TODO: 把 target.value 写入 fullName
}
// 问题：输入 "Jane Smith" 并回车后，firstName 和 lastName 分别变成什么？

// ==================== 试题 3：watch 基本用法 ====================
const count = ref(0)

// TODO: 用 watch 侦听 count，回调里打印 "count: {oldVal} -> {newVal}"
watch(count,(nv,ov)=>{
    console.log(`watch,nv:${nv},ov:${ov}`);
})
function increment() {
  count.value++
}

function decrement() {
  count.value--
}

function reset() {
  count.value = 0
}
// 问题：多次 ++ 后再点 reset，打印的 oldVal 是什么？

// ==================== 试题 4：watchEffect 自动收集依赖 ====================
const a = ref(1)
const b = ref(2)
const c = ref(0)

// TODO: 用 watchEffect 打印 "a + b = xxx"
watchEffect(()=>{
    const sum = a.value +b.value;
    console.log(`a+b=${sum}`)
})

function changeA() {
  a.value++
}

function changeB() {
  b.value++
}

function changeC() {
  c.value++
}
// 问题：点"修改 c"时 watchEffect 会触发吗？为什么？
// c又没有被收集响应依赖当然不会触发。

const keyword = ref('');
function onKeyWordChange(){

}

let t: number | null | undefined= null;
watch(keyword,(nv,ov)=>{
    if(t)
        clearTimeout(t);
    t = setTimeout(()=>{
    console.log(`watch防抖,nv:${nv},ov:${ov}`);
    },500)
})

</script>

<style scoped>

</style>
