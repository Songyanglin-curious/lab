# 1.1 脚手架与项目结构

## 参考文档

- [Vue3 官方 - 快速上手](https://cn.vuejs.org/guide/quick-start.html)
- [Vite 官方 - 入门](https://cn.vitejs.dev/guide/)

## 你的原始理解 ✅

| 文件/目录          | 你的理解                                                       |
| ------------------ | -------------------------------------------------------------- |
| `package.json`   | nodejs的版本依赖还有各种包管理还有项目启动命令的管理           |
| `vite.config.ts` | vite配置的文件，配置映射符还有插件等                           |
| `tsconfig.json`  | ts的配置文件                                                   |
| `index.html`     | 网页的挂载点，单页面最后编译出来的是js，需要一个入口的html页面 |
| `env.d.ts`       | 开发环境变量就是有的变量开发环境和生产环境使用的变量是不一样的 |
| `src/main.ts`    | vue的逻辑初始化对象的入口                                      |
| `src/App.vue`    | 单页面的入口                                                   |
| `public/`        | 静态资源存放点                                                 |
| `node_modules/`  | 依赖包的资源存放文件夹                                         |

---

## 逐文件讲解

### `package.json` — 项目的身份证

你的理解方向对，但少了一层：它不只是"管依赖和命令"，而是 **npm/pnpm 读取的唯一入口**。

```json
{
  "name": "my-project",       // 项目名（发布到 npm 时用）
  "private": true,             // 防止意外发布
  "version": "0.0.0",         // 版本
  "scripts": {                 // 快捷命令（pnpm dev → vite）
    "dev": "vite",
    "build": "vue-tsc && vite build",
    "preview": "vite preview"
  },
  "dependencies": {            // 运行时依赖（打包后会进产物）
    "vue": "^3.4.0"
  },
  "devDependencies": {         // 仅开发时用的依赖（不会打进产物）
    "vite": "^5.0.0",
    "typescript": "^5.0.0"
  }
}
```

关键区分：

- `dependencies` —— 代码 `import` 了且最终用户浏览器要跑的（vue、vue-router、pinia、axios、element-plus）
- `devDependencies` —— 只在开发/构建环节用的（vite、typescript、eslint、prettier）

如果现在随手添加一个包，你能判断该放哪里吗？后面的练习会考。

---

### `vite.config.ts` — Vite 的总控开关

Vite 读这个文件来决定：

| 配置项            | 作用                 | 示例                                                        |
| ----------------- | -------------------- | ----------------------------------------------------------- |
| `plugins`       | 加载 Vite 插件       | `vue()` 让 Vite 认识 `.vue` 文件                        |
| `resolve.alias` | 路径别名             | `@` → `./src`，写 `import @/utils` 而不用 `../../` |
| `server.port`   | 开发服务器端口       | 默认 5173                                                   |
| `server.proxy`  | 接口代理（解决跨域） | `/api` → `http://localhost:3000`                       |
| `build.outDir`  | 打包输出目录         | 默认 `dist`                                               |

脚手架生成的默认配置通常很简单，后面 1.2 轮会专门展开。

---

### `tsconfig.json` — TypeScript 编译规则

> 你说的"ts的配置文件"——对，但具体管什么？

它告诉 TypeScript 编译器：

- **编译到哪个 JS 版本**：`"target": "ES2020"`
- **模块系统**：`"module": "ESNext"`
- **严格程度**：`"strict": true`（开启后不能 `any` 偷懒）
- **路径别名**：`"paths": { "@/*": ["./src/*"] }`（和 vite.config 里的 alias 要对应）
- **哪些文件要检查**：`"include": ["src/**/*.ts", "src/**/*.vue"]`

本质一句话：**TS 编译器拿到 tsconfig 才知道怎么把 `.ts` / `.vue` 变成 JS**。

---

### `index.html` — Vite 构建的真正入口

你的理解完全正确：SPA 需要一个 HTML 挂载点。

但有一个**反直觉**的点：

> Vite 的构建入口是 `index.html`，不是 `src/main.ts`

```html
<!DOCTYPE html>
<html lang="en">
  <head>...</head>
  <body>
    <div id="app"></div>                      <!-- Vue 挂载点 -->
    <script type="module" src="/src/main.ts"></script>  <!-- Vite 从这里找到 main.ts -->
  </body>
</html>
```

Webpack 时代入口是 `main.js`，Vite 时代入口是这个 HTML。这是设计上的一个重要区别。

---

### `env.d.ts` ❗核心纠正

> 你说的"环境变量"理解是错的。这个文件跟 `.env` 毫无关系。

它实际只干一件事：**给 TypeScript 打补丁，让它认识一些它原本不认识的模块类型**。

```ts
/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}
```

拆解：

1. `/// <reference types="vite/client" />` — 让 TS 认识 `import.meta.env` 的类型（Vite 注入的环境变量 API）
2. `declare module '*.vue'` — 告诉 TS："所有 `.vue` 文件导出的都是一个 Vue 组件类型，别报错"

如果没有这个文件，你在 `main.ts` 里写 `import App from './App.vue'` 时 TS 会红：

> `Cannot find module './App.vue' or its corresponding type declarations.`

命名混乱是历史原因：因为里面引用了 `vite/client` 类型（其中包含 `import.meta.env` 的类型定义），所以叫 `env.d.ts`。但它本质是类型声明文件，不是环境变量文件。

**真正的环境变量文件**是：

- `.env`（所有环境通用）
- `.env.development`（`pnpm dev` 时加载）
- `.env.production`（`pnpm build` 时加载）

这个在 1.2 轮展开。

---

### `src/main.ts` — Vue 应用的启动入口

```ts
import { createApp } from 'vue'     // 进口：createApp 工厂函数
import App from './App.vue'          // 进口：根组件
import router from './router'        // 进口：路由实例（如果你选了 Router）

const app = createApp(App)           // 核心：创建 Vue 应用实例
app.use(router)                      // 装插件
app.mount('#app')                    // 挂载到 index.html 里的 <div id="app">
```

流程：`index.html` → 加载 `src/main.ts` → 创建 Vue 实例 → 渲染 `App.vue` → 插入 `<div id="app">`。

这就是整个 Vue3 应用的**启动链路**。后面学路由、状态管理、Element Plus，都是在这条链路上 `app.use(xxx)` 装插件。

---

### `src/App.vue` — 根组件

它是整个 Vue 组件树的根。典型结构：

```vue
<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <RouterView />   <!-- 路由出口，根据 URL 渲染对应的页面组件 -->
</template>
```

如果没选 Router，这里就是直接写页面内容。选了 Router 后，`App.vue` 通常只放 `<RouterView />`，页面切换由路由接管。

---

### `public/` — 纯静态资源

跟你理解一致。但有一个重要区别：

|                    | `public/`                         | `src/assets/`             |
| ------------------ | ----------------------------------- | --------------------------- |
| 是否经过 Vite 编译 | ❌ 原样复制                         | ✅ 会被处理（hash、压缩）   |
| 引用方式           | `/favicon.ico`（绝对路径）        | `import` 或 `@/assets/` |
| 适合放什么         | favicon、robots.txt、完全不改的文件 | 组件里引用的图片、样式      |

---

### `node_modules/` — 依赖的地基

你理解正确。补充两点：

1. **不要手动改**，只通过 `pnpm add` / `pnpm remove` 操作
2. **不要提交到 git**（脚手架已帮你写好 `.gitignore`）
3. 用 pnpm 时这个目录是**硬链接**，多个项目共享磁盘空间——这就是 pnpm 比 npm 省空间的原理

---

## 验证题

下面这道题检验你对 `env.d.ts` 和 `package.json` 两个最易混点的掌握：

### 题目

假设你新起了一个 Vue3 项目，现在要做以下三件事。请判断每件事应该改**哪个文件（或文件组）**，并解释为什么：

1. 安装 element-plus，并在页面上使用它的按钮组件
2. 让 TypeScript 不再对 `import App from './App.vue'` 报错
3. 让开发环境 `pnpm dev` 时访问 `/api` 请求转发到 `http://localhost:8080`，但生产环境不走代理

把你的答案发过来，我评价后再进入 1.2 轮（Vite 配置与环境变量）。

1. package.json  dependencies下管理，，因为这块逻辑是要被打包的
2. 要在 `env.d.ts` 配置但是怎么配置我忘记了
3. 代理需要在vite.config.ts配置因为 vite在开发的时候是起到了服务器的作用 而代理是需要网络服务才能够生效的。
