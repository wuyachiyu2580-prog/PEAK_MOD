# 🏔️ PEAK 地图参数手册

> 全关卡 · 全变体 · 全生成器 · 全参数 — 中英双语 · 可搜索 · 可折叠

基于 PEAK 1.62.a DreamyAscent Snapshot V2（27份诊断样本）自动生成的地图参数查阅工具。覆盖 7 个关卡、32 个变体、1261 个生成步骤的全部参数。

---

## 📖 使用指南（第一次用看这里）

### 打开方式

- **完整版**：双击 `PEAK地图参数手册.html`，浏览器直接打开。一个文件包含全部内容，可离线使用、可下载分享。
- **碎片版**：打开 `index.html`（需联网或用本地服务器，因为关卡数据按需加载）。

### 基本操作

| 操作 | 方法 |
|------|------|
| **切换关卡** | 点击顶部的标签按钮（海滩/雨林/森蕈/雪山/沙漠/破火山口/火山口） |
| **展开/折叠** | 点击「📂 全部展开」或「📁 全部折叠」按钮 |
| **搜索** | 在搜索框输入中文或英文关键词（物件名、参数名、变体名、数值均可），高亮显示匹配项 |
| **清除搜索** | 点击搜索框右侧 ✕ 按钮，或按 `Esc` 键 |
| **Ctrl+F** | 按 `Ctrl+F` 自动聚焦到搜索框 |

### 进阶功能

#### 🎯 快速总览表
每个关卡顶部有一张「快速总览表」，列出所有变体及其与默认变体的差异：
- 🟢 **绿色 +xxx**：该变体比默认多出的物件/分组器
- 🔴 **红色 -xxx**：该变体比默认少的物件/分组器
- 点击绿色差异项可直接跳转到下方对应的步骤详情

#### ⊕ 物件对比
每个步骤右侧有 `⊕` 按钮，可将不同变体的同一物件加入对比面板：
1. 点击两个不同变体中同名物件的 ⊕ 按钮
2. 底部弹出对比面板，并排展示参数差异
3. 点击「清空对比」重新选择

#### ⚙ 自定义精简模式
点击「⚙ 参数设置」可以：
- 只显示你关心的参数（勾选/取消勾选）
- 快速预设：「核心参数」/「全部显示」/「全部隐藏」
- 隐藏的参数行不会占用空间，页面更清爽

#### 🔇 静音参数
默认隐藏 `mute`（静音）参数行。点击「🔇 显示静音参数」可切换显示。

---

## 🚀 部署到 Vercel（给 AI / 开发者）

### 方式一：通过 Vercel CLI

```bash
# 1. 安装并登录 Vercel CLI
npm i -g vercel
vercel login

# 2. 进入 vercel-deploy 目录
cd vercel-deploy

# 3. 部署（按提示确认即可）
vercel --prod
```

### 方式二：通过 Vercel 网页导入

1. 将整个 `vercel-deploy/` 目录上传到 GitHub 仓库
2. 打开 [vercel.com/new](https://vercel.com/new)
3. 导入该仓库
4. **关键设置**：
   - Framework Preset：选择 **Other**（或留空）
   - Root Directory：设为 `vercel-deploy`（如果仓库根目录不是 vercel-deploy）
   - 无需 Build Command，无需 Output Directory
5. 点击 Deploy

### 方式三：一键命令

如果当前环境已安装 Vercel CLI 且已登录，直接运行：

```bash
cd vercel-deploy && vercel --prod
```

### 部署说明

- 这是**纯静态网站**（HTML + CSS + JS），不需要构建步骤
- `segments/` 目录下的 HTML 片段由 `index.html` 通过 `fetch()` 按需加载
- 完整单体文件 `PEAK地图参数手册.html` 也可直接部署，但碎片化版本首次加载更快

---

## 🔄 更新文档

当游戏版本更新或采集到新的诊断样本后：

```bash
# 1. 重新聚合数据
cd 地图参数
python build_docs.py

# 2. 重新生成 HTML
python generate_docs.py

# 3. 重新拆分为 Vercel 版本
python split_vercel.py
```

---

## 📊 数据说明

- **数据来源**：PEAK 1.62.a DreamyAscent Snapshot V2，共 27 份 RuntimeExport.json 诊断样本
- **翻译参考**：TerrainCustomiserCN DisplayNameTranslator
- **生成日期**：见页面顶部统计栏
