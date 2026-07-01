# 1.2 Vite 配置与环境变量

## 参考文档

- [Vite 官方 - 配置参考](https://cn.vitejs.dev/config/)
- [Vite 官方 - 环境变量与模式](https://cn.vitejs.dev/guide/env-and-mode.html)
- [Vite 官方 - server.proxy](https://cn.vitejs.dev/config/server-options.html#server-proxy)

---

## 讲解：三个核心配置项

### 1. `resolve.alias` — 路径别名

不用再写 `../../../components/Button.vue`，用 `@/components/Button.vue`。

脚手架已帮你写好：

```ts
// vite.config.ts
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  }
})
```

拆解：

- `import.meta.url` → 当前文件（vite.config.ts）的 file:// URL
- `new URL('./src', import.meta.url)` → 拼接出 `file:///.../src` 路径
- `fileURLToPath(...)` → 把 file:// URL 转成系统路径 `D:\...\src`
- 最终效果：代码里 `import xxx from '@/utils/api'` → Vite 解析为 `src/utils/api.ts`

**注意**：光 Vite 配了还不行，TS 也要认识 `@`。检查你的 `tsconfig.app.json` 里是否有：

```json
"compilerOptions": {
  "paths": {
    "@/*": ["./src/*"]
  }
}
```

两者必须同时配，否则代码能跑但 VSCode 红色波浪线伴你终身。

---

路径别名在使用ts时 vite和ts都得配置

---

### 2. `server.proxy` — 开发环境接口代理

**问题**：前端跑在 `localhost:5173`，后端跑在 `localhost:8080`。浏览器发 `/api/user` 请求会拼成 `http://localhost:5173/api/user` → 404。

**解决**：Vite dev server 做代理转发：

```ts
// vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      }
    }
  }
})
```

效果：

- 前端代码写 `fetch('/api/user')`
- Vite dev server 拦截 `/api` 开头的请求
- 转发到 `http://localhost:8080/api/user`
- 浏览器以为请求的是同源，不触发跨域

三个关键点：

| 字段             | 作用                                                        |
| ---------------- | ----------------------------------------------------------- |
| `'/api'`       | 匹配规则：以 `/api` 开头的请求走代理                      |
| `target`       | 转发的目标地址                                              |
| `changeOrigin` | 把请求头里的 `Host` 改成 target 的 host，避免后端校验拒绝 |

**重要**：代理只在开发环境（`pnpm dev`）生效。生产环境是打包出静态文件，没有 Vite dev server，代理通常由 nginx 做。

---

此处的代理配置本质上就是服务器的代理配置将各种服务器代理思想一套 就能明白，只需要掌握各种配置的写法即可

---

### 3. `.env` 文件 — 环境变量

**上一轮的纠正**：`env.d.ts` 是 TS 类型声明，不是环境变量文件。真正的环境变量文件是这些：

```
.env                  # 所有环境都加载
.env.development      # pnpm dev 时加载
.env.production       # pnpm build 时加载
```

**命名字规则**：只有 `VITE_` 前缀的变量才会暴露给前端代码。这是 Vite 的安全设计——防止把服务器密钥泄露到浏览器。

```bash
# .env.development
VITE_API_BASE=http://localhost:3000
VITE_APP_TITLE=本地开发环境
```

```bash
# .env.production
VITE_API_BASE=https://api.example.com
VITE_APP_TITLE=生产环境
```

在代码中读取：

```ts
console.log(import.meta.env.VITE_API_BASE)   // 'http://localhost:3000'
console.log(import.meta.env.VITE_APP_TITLE)  // '本地开发环境'
console.log(import.meta.env.MODE)            // 'development'（内置变量）
console.log(import.meta.env.DEV)             // true（内置变量）
console.log(import.meta.env.PROD)            // false（内置变量）
```

**生命周期**：`.env` 文件中的变量在**构建时**被静态替换进代码，不是运行时读取。这意味着改了 `.env` 需要重启 `pnpm dev`。

---

## 三个文件的职责对照（关键！）

| 文件               | 管什么                                                              | 谁读                    |
| ------------------ | ------------------------------------------------------------------- | ----------------------- |
| `env.d.ts`       | TS 类型声明：`declare module '*.vue'`、`import.meta.env` 的类型 | TypeScript 编译器       |
| `.env.*`         | 运行时配置数据：API 地址、标题、密钥等                              | Vite 构建               |
| `vite.config.ts` | Vite 本身的行为：代理、别名、插件、端口                             | Vite dev server / build |

三件事无关，不要混。

---

## 你的任务

### 任务 1：验证别名

在 `src/App.vue` 的 `<script setup>` 里：

```vue
<script setup lang="ts">
import { RouterView } from 'vue-router'
// 用 @ 别名引用 store
import { useCounterStore } from '@/stores/counter'

const counter = useCounterStore()
console.log(counter.count)
</script>
```

验证 `@/stores/counter` 能正确跳转且不红。

### 任务 2：配代理

在 `vite.config.ts` 里加 proxy 配置，把 `/api` 请求转发到 `http://localhost:3000`。

### 任务 3：建环境变量文件

创建 `.env.development`，写入：

```
VITE_API_BASE=http://localhost:3000
VITE_APP_TITLE=Vue3 学习项目
```

然后在 `App.vue` 里打印这两个值，看 `pnpm dev` 控制台输出。

---

## 验证题（做完任务后回答）

用一句话分别说明以下三个文件是干什么的，要求体现它们的区别：

1. `env.d.ts`
   供ts编译器使用 提供自定义文件类型被识别的；“d.ts”文件用于为 TypeScript 提供有关用 JavaScript 编写的 API 的类型信息。
2. `.env.development`
   开发环境的环境变量
3. `vite.config.ts` 里的 `server.proxy`
   请求代理，就是个代理服务器仅此而已。
