# 1.3 打包与部署

## 参考文档

- [Vite 官方 - 构建生产版本](https://cn.vitejs.dev/guide/build.html)
- [Vite 官方 - 部署静态站点](https://cn.vitejs.dev/guide/static-deploy.html)
- [Vite 官方 - 公共基础路径](https://cn.vitejs.dev/config/shared-options.html#base)

---

## 讲解

### 1. `pnpm build` 产物解读

执行 `pnpm build` 后生成 `dist/` 目录：

```
dist/
  index.html              # 入口 HTML
  assets/
    index-CNQwonAQ.js     # JS 产物，带 content hash
    index-xx.css          # CSS 产物（如果有样式）
    HomeView-xx.js        # 路由懒加载的页面（配了 Router 懒加载后会出现）
  favicon.ico             # public/ 下原样复制
```

关键概念：

| 现象 | 原因 |
|------|------|
| 文件名带 hash（`CNQwonAQ`） | 内容哈希指纹。文件内容经哈希算法生成，内容变 → hash 变 → 浏览器重新请求 |
| HTML 没被压缩 | Vite 默认不压缩 HTML，只压缩 JS/CSS。HTML 保留可读结构 |
| 多 chunk 分割 | 路由懒加载时 Vite 自动拆分，当前项目只有首页所以只有一个 JS |

**hash 命名规则**：Vite 底层用 Rolldown 打包，默认输出模式为 `assets/[name]-[hash].[ext]`。hash 是文件内容的 SHA-256 截断。

---

### 2. `base` — 部署路径

**默认情况**（`base: '/'`）：

- `index.html` 里的资源引用 → `/assets/index-Df9kL3x8.js`
- 部署到 `https://example.com/` → 浏览器请求 `https://example.com/assets/index-Df9kL3x8.js` → ✅

**子路径部署**（比如 `https://example.com/my-app/`）：

- 默认 `base: '/'` → 资源路径仍是 `/assets/...` → 浏览器请求 `https://example.com/assets/...` → ❌ 404
- 需要改成 `base: '/my-app/'` → 资源路径变成 `/my-app/assets/...` → ✅

```ts
// vite.config.ts
export default defineConfig({
  base: '/my-app/'  // 部署路径
})
```

**本质**：`base` 的值被拼接到所有资源引用路径前面，仅此而已——就是资源路径的前缀管理。

---

### 3. `pnpm preview` — 本地验证构建产物

```bash
pnpm build
pnpm preview
```

它启动一个静态服务器，把 `dist/` 当根目录，模拟生产环境。用来在本地验证打包结果是否正确。

**和 `pnpm dev` 的区别**：

| | `pnpm dev` | `pnpm preview` |
|--|-----------|---------------|
| 服务器 | Vite dev server（有 HMR、代理、转换） | 静态文件服务器（纯输出） |
| 来源 | 源码实时编译 | `dist/` 构建产物 |
| 用途 | 开发调试 | 验证打包结果 |
| 代理 | `server.proxy` 生效 | 不生效（需要 nginx 或后端处理） |

---

### 4. 常见部署问题

| 现象 | 原因 | 解决 |
|------|------|------|
| 部署后白屏 | 资源路径错误，JS/CSS 404 | 检查 `base` 配置 |
| 刷新页面 404 | SPA 路由由前端 JS 管理，服务器没有对应 HTML 文件 | nginx 配 `try_files $uri /index.html` |
| 图片 404 | 图片路径写法不对 | `public/` 下用 `/img/logo.png` 绝对路径 |
| API 请求 404 | 开发时靠 `server.proxy` 转发，生产没有代理 | nginx 配反向代理 |
| hash 文件名没变 | 代码确实没变 | 正常现象，不用管 |

**核心原理一句话**：SPA 只有一个 `index.html`，所有路由都由前端 JS 控制。服务器需要把所有路径都 fallback 到 `index.html`，否则非根路径刷新直接 404。

---

## 动手记录

### 任务 1：构建产物

实际 hash 文件名：**`index-CNQwonAQ.js`**

hash 命名由 Vite 底层 Rolldown 自动生成，配置项为 `build.rolldownOptions.output.entryFileNames`，默认值就是 `assets/[name]-[hash].js`。

当前项目只有一个 JS 是因为没有路由懒加载——配了 Router 懒加载后会自动出现多 chunk。

### 任务 2：本地预览

`pnpm preview` 渲染正常。项目内容少时看不出差异，项目变大后（有懒加载、有静态资源）preview 的验证价值更高。

### 任务 3：子路径部署

结论确认：`base` 值拼到 `/assets/` 前面，修改后 `index.html` 里 script src 路径立刻反映。

---

## 验证题答案

一个 Vue3 项目部署到 `https://mysite.com/admin/`，以下问题的原因和修复：

### 1. 白屏 + JS/CSS 404，路径是 `/assets/index-xxx.js`

**原因**：`vite.config.ts` 里 `base` 没有改。打包后资源引用是 `/assets/index-xxx.js`，但实际部署在 `/admin/` 子路径下，浏览器发请求到 `https://mysite.com/assets/index-xxx.js` → 404。

**修复**：

```ts
base: '/admin/'
```

### 2. 能进首页，刷新 `/admin/user` 报 nginx 404

**原因**：这是 SPA 路由的核心问题，和资源加载无关。

- 从首页点击链接进入 `/admin/user` → Vue Router 在浏览器端拦截点击，用 History API 改 URL，不发请求
- 直接在地址栏回车或 F5 刷新 → 浏览器向服务器发 `GET /admin/user` → nginx 在硬盘上找不到这个文件 → 404

**修复**：nginx 配置 SPA fallback：

```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

逻辑：先找 `$uri` 文件，找不到找目录，再找不到就返回 `index.html`。然后 Vue Router 在浏览器端接管 URL 渲染正确组件。

### 3. 开发时 API 正常，部署后 `/api/login` 返回 nginx 404

**原因**：开发环境 `vite.config.ts` 里的 `server.proxy` 只在 `pnpm dev` 时生效。生产环境 `pnpm build` 后没有 Vite dev server，`/api/login` 请求直接打到 nginx，nginx 没有配这个路径的转发规则。

**修复**：在 nginx 里加反向代理：

```nginx
location /api/ {
    proxy_pass http://your-backend-server:3000/;
}
```
