<script setup lang="ts">
import { useCounterStore } from '@/stores/counter'
import ComputedWatchExercises from '@/components/ComputedWatchExercises.vue'

const counter = useCounterStore()
console.log(counter.count);
</script>

<template>
  <h1>Vue3 响应式基础 Demo</h1>
  <ComputedWatchExercises />
</template>

<style scoped></style>
