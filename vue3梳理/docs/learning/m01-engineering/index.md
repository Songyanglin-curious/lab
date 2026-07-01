# 模块一：工程基础

## 知识点

- 脚手架和项目结构
- Vite 配置：路径别名、代理、环境变量
- 打包构建与部署路径

## 学习轮次

| 轮次 | 状态 | 内容 |
|------|------|------|
| [1.1](./01-scaffold.md) | ✅ 已完成 | `create vue` 脚手架与项目结构 |
| [1.2](./02-config.md) | ✅ 已完成 | Vite 配置与环境变量 |
| [1.3](./03-build.md) | ✅ 已完成 | 打包与部署 |
| [排错实录](./01-scaffold-troubleshooting.md) | 📄 参考 | 初始化爆红排错流程 |

---

## 模块小结

### 核心概念（必须能说清）

1. **脚手架做了什么**：生成 `package.json`（依赖声明）+ 源码骨架 + 配置文件，**不会自动 install**
2. **env.d.ts ≠ 环境变量**：它是 TS 类型声明（`declare module '*.vue'`），不是 `.env` 文件
3. **别名要双配**：Vite 用 `resolve.alias`，TS 用 `compilerOptions.paths`，缺一不可
4. **proxy 只在 dev 生效**：生产环境需要 nginx 配反向代理
5. **SPA fallback**：所有非根路径需要 nginx `try_files` 回到 `index.html`
6. **base 就是路径前缀**：控制资源引用路径的拼接

### 已经搭建了的基础

`Vue3Atlas` 项目已具备：
- Vue3 + TypeScript + Vite 脚手架
- Router / Pinia 集成
- ESLint + Prettier
- pnpm + hoisted node_modules
- @ 路径别名可用
