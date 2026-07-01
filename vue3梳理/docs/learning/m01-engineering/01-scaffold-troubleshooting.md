# Vue3 项目初始化各个爆红排错流程

> 环境：Vue 3.5 + Vite 8 + TypeScript 6 + pnpm 11 + Node 24  
> 脚手架：`pnpm create vue@latest`（选了 TS / Router / Pinia / ESLint / Prettier）

---

## 现象清单

项目创建后，VSCode 中以下位置全部爆红：

| 文件 | 表现 |
|------|------|
| `src/main.ts` | `import { createApp } from 'vue'` 等全部 import 红色 |
| `vite.config.ts` | 全部 import 红色 |
| `eslint.config.ts` | 全部 import 红色 |
| `tsconfig.app.json` 第 1 行 | 红色，报"找不到 @vue/tsconfig/tsconfig.dom.json" |
| `tsconfig.node.json` 第 1 行 | 红色，报"找不到 @tsconfig/node24/tsconfig.json" |

---

## 排查过程

### 第一轮：检查 node_modules

**怀疑**：脚手架创建完项目后，忘了执行 `pnpm install`。

**验证**：打开 `node_modules/` 目录——**空的，不存在**。

**结论**：确认为根因之一。`pnpm create vue@latest` 只生成了脚手架代码和 `package.json`，不会自动安装依赖。

**修复**：

```bash
pnpm install
```

**结果**：`src/main.ts`、`vite.config.ts`、`eslint.config.ts` 的 import 红色全部消失。  
但 `tsconfig.app.json` 和 `tsconfig.node.json` 的 `extends` 仍然红色。

---

### 第二轮：验证 extends 指向的文件是否存在

报错内容是 "找不到 @vue/tsconfig/tsconfig.dom.json" 和 "找不到 @tsconfig/node24/tsconfig.json"。

**验证**：检查 `node_modules/` 下对应路径：

```
node_modules/@vue/tsconfig/tsconfig.dom.json     ← 存在
node_modules/@tsconfig/node24/tsconfig.json       ← 存在
```

**结论**：文件存在但 TypeScript 解析不到。问题不在包缺失，而在 pnpm 的 node_modules 结构。

---

### 第三轮：切换 pnpm 链接模式

**根因**：pnpm 默认使用 `isolated` 模式，node_modules 通过虚拟存储（`.pnpm/`）+ 硬链接组织。TypeScript 6.x 的 `extends` 解析器无法正确追踪这种链路，而普通 `import`（如 `import { ref } from 'vue'`）走的是 Node 模块解析，不受影响。

**解决**：在项目根目录创建 `pnpm-workspace.yaml`：

```yaml
nodeLinker: hoisted
```

然后重新安装：

```bash
rm -rf node_modules
pnpm install
```

**结果**：`tsconfig.app.json` 和 `tsconfig.node.json` 的 `extends` 红色消失。

**原理**：`hoisted` 模式创建扁平 node_modules（和 npm/yarn 一样），TypeScript 能直接追踪文件。

---

### 第四轮：反向验证

**验证 1**：修复后，删除 `pnpm-workspace.yaml`。  
**结果**：仍不爆红。因为 node_modules 已经是扁平结构，删了配置文件不会回溯。

**验证 2**：将 Node 版本从 24.17.0 降到 22。  
**结果**：即使不切换 hoisted 模式也不再爆红。说明 TypeScript 6 + Node 24 的组合存在额外的 extends 解析兼容性问题。

---

## 总结

| 错误 | 根因 | 解决 |
|------|------|------|
| 所有 `.ts` 文件 import 红色 | `pnpm install` 未执行 | `pnpm install` |
| tsconfig `extends` 红色 | pnpm isolated 模式 + TS 6.x + Node 24 的 extends 解析链路断裂 | 方案 A / B / C |

### 推荐解决方案（三选一）

| 方案 | 操作 | 适用场景 |
|------|------|---------|
| **A** | 创建 `pnpm-workspace.yaml`，加 `nodeLinker: hoisted`，重装 | 首选，pnpm 官方推荐的兼容模式 |
| **B** | 将 Node 降到 22.x | 不想改 pnpm 配置 |
| **C** | 去掉 tsconfig 的 `extends`，手动内联配置 | 所有方案都无效时的最终手段 |

---

## 关键认知

1. **不是所有红色都是"包没装"**。先确认 `node_modules` 里文件是否存在，再判断是安装问题还是解析问题。
2. **pnpm isolated → hoisted 不是 hack**，是 [pnpm 官方文档](https://pnpm.io/settings#nodeLinker) 明确列出的正当使用场景。
3. **修复一但生效，删掉配置不会回滚**——node_modules 目录结构已经固化了。
4. **Node 版本是变量**。TS 6 + Node 24 这种版本组合太新，遇到奇怪问题可以降 Node 试一下。
