# Memory Index

更新时间：2026-05-17

## 当前结构

- `README.md`：唯一入口和读取顺序。
- `CHANGELOG.md`：memory 变更履历（按时间倒序追加）。
- `TODO.md`：永久待办和风险。
- `MEMORY_INDEX.md`：本文件，总览索引。
- `common/`：跨 MOD 通用规则（00-06 兼共 7 个主题）。
- `mods/`：每个 MOD 的独立四件套（README + RECENT + DECISIONS + FILES）。

## 通用规则（common/）

- `common/00_用户偏好.md`：用户身份、沟通风格、接手基线。
- `common/01_协作与记忆规则.md`：四同步铁律、接手前检查、日期规则。
- `common/02_工程与构建规范.md`：OutputPath/HintPath 规约、L&SZ&N 作为重构参照。
- `common/03_日志与诊断规范.md`：日志分级、FieldProbe DSL、JSON 禁注释。
- `common/04_联机与同步规范.md`：主客机权限、RPC 校验、中途加入同步。
- `common/05_发布与版本规范.md`：README/CHANGELOG/manifest 三文件协同、135 字符限制。
- `common/06_UI与字体规范.md`：CJK 字体四级兜底、FontHelper 标准实现、描边与字号规范。

## MOD 四件套（mods/）

### ItemInfoCN（1.0.0 已发布）

- `mods/ItemInfoCN/README.md`：中文化 HUD 整体架构与功能轮廓。
- `mods/ItemInfoCN/RECENT.md`：1.0.0 发布态、配置项表、翻译覆盖面、已知边界。
- `mods/ItemInfoCN/DECISIONS.md`：架构决策、翻译策略、补丁边界、EasyBackpack 兼容。
- `mods/ItemInfoCN/FILES.md`：源码路径、项目文件、构建命令、关键文件（含 `Helpers/FontHelper.cs` CJK 字体四级兜底）。

### Lantern_ShootZombies_Night（0.2.1）

- `mods/Lantern_ShootZombies_Night/README.md`：灯笼/打僵尸/日夜/寒冷回暖整合入口。
- `mods/Lantern_ShootZombies_Night/RECENT.md`：近期修复，含客机备用池/灯燃料双扣、本地燃料权威和重复灯处理。
- `mods/Lantern_ShootZombies_Night/DECISIONS.md`：版本、灯笼同步、本地燃料权威、配置同步、兼容和禁止回退。
- `mods/Lantern_ShootZombies_Night/FILES.md`：源码路径、构建命令、关键 Helper / Patch 清单。

### PlayersInfo（0.1.1）

- `mods/PlayersInfo/README.md`：队友 HUD 聚合概览与功能轮廓。
- `mods/PlayersInfo/RECENT.md`：0.1.0 首发状态、HUD 聚合、本地体力显示、TMP 描边、2026-05-17 性能优化（脏检查+反射降频+Sprite 销毁）、发布 0.1.1 安静维护版、2026-05-17 修复 SettingChanged 全配置订阅导致体力条整体闪烁的 BUG（拆分为 3 个结构性开关订阅）、2026-05-17 修复 NearbyRange 边缘拖动导致单条闪烁的 BUG（加 5m 距离滞回）、2026-05-17 网络波动加固（现象 D2： viewId→actor 反查缓存 + 放宽 Owner null 剔除 + 成员丢失保留 1.5s）、2026-05-17 合补重打 0.1.1 发行包（3 条 Bugfix 入包）。
- `mods/PlayersInfo/DECISIONS.md`：只读展示不发 RPC、HUD 架构、首发发布口径和跨 MOD 数据边界。
- `mods/PlayersInfo/FILES.md`：源码路径、项目文件、版本和关键文件（含 `Helpers/FontHelper.cs` CJK 字体四级兜底）。

### DreamyAscent（0.1.0 开发中）

- `mods/DreamyAscent/README.md`：项目概览和接手入口。
- `mods/DreamyAscent/RECENT.md`：近期完成内容、当前验证结论和后期物品编辑需求初评。
- `mods/DreamyAscent/DECISIONS.md`：已确认的技术决策、禁止回退项和后期区域物品编辑边界。
- `mods/DreamyAscent/FILES.md`：关键路径、构建命令、诊断目录和关键源码文件。
- `mods/DreamyAscent/MAP_GENERATION.md`：地图生成链路、需求拆分、分层实现路线、诊断事实、已知故障反推、资料缺口和推荐路线。
- `mods/DreamyAscent/IMPLEMENTATION_MATRIX.md`：后期需求到实现矩阵，逐项记录官方模板、空白自定义、跨区段、父子依赖、外部物品、材质、UI、模板快照和多人同步的实现路径、依据、例子和资料缺口。
- `mods/DreamyAscent/MAP_GENERATION_RESEARCH_NOTES.md`：按用户要求多轮通读资源的过程记录、原始依据和多角度实现分析。
- `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`：跨区段物品放置专门记忆，记录来源模板 + 目标子区模型、雨林棕榈放沙漠等例子、风险等级和资料缺口。
- `MOD开发/DreamyAscent/data/map-data/SAMPLE_AUDIT_2026-05-13.md`：项目内地图样本集中审计，记录官方自然样本、TerrainRandomiser 验证样本、完整性、变体覆盖和纯净性判断。
- `MOD开发/DreamyAscent/data/map-data/sample-index.json`：项目内地图样本机器可读索引，供模板快照、对象注册表和回归检查工具使用。
- `MOD开发/DreamyAscent/data/map-data/COLLECTION_V2.md`：Snapshot V2 重采规则，规定官方自然样本和 TerrainRandomiser 强制变体样本分目录采集，且必须包含 `GeneratedChildrenSnapshot.json`。
- `MOD开发/DreamyAscent/data/tools/build_map_data_artifacts.py`：从诊断样本生成模板快照、对象注册表输入和样本回归报告的离线工具。
- `MOD开发/DreamyAscent/data/map-data/generated/`：离线生成产物目录，当前包含 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`。

### WhySoLaggy（1.0.3）

- `mods/WhySoLaggy/README.md`：性能和网络诊断项目入口与 mermaid 能力矩阵。
- `mods/WhySoLaggy/RECENT.md`：帧率/联机/Harmony/结构化日志四类诊断能力。
- `mods/WhySoLaggy/DECISIONS.md`：只做诊断不改业务、FieldProbe 默认关闭、IMGUI 堆叠淡出。
- `mods/WhySoLaggy/FILES.md`：源码、项目文件、版本和关键文件。

## 当前重点

- `Lantern_ShootZombies_Night` 当前重点是实机验证客机本地燃料权威：有备用池时只降备用池、不降灯燃料，且远端 fuel 下降不覆盖本地。
- `DreamyAscent` 预览主体已可用，7 个主要区段已有源码默认预览位姿；2026-05-10 已新增对象引用诊断、`ObjectCatalog.json`、只读“区段模板库”UI、区段模板库翻译和生成器范围高亮收敛。2026-05-11 `CustomBlank` 第一版也已跑通。2026-05-12 已新增 `MAP_GENERATION.md`、`IMPLEMENTATION_MATRIX.md` 和 `CROSS_SEGMENT_PLACEMENT.md`。2026-05-13 `data/map-data` 样本审计收口并生成第一版离线产物。2026-05-17 进入 Snapshot V2：旧样本已删除，新样本必须包含 `GeneratedChildrenSnapshot.json`，先跑示范样本验字段，再全量重采。
- 其他 MOD 新增功能前先读对应 `RECENT.md` 和 `DECISIONS.md` 的"禁止回退"条款。

## 记忆维护规则

### 定位说明

`memory/` 是**给其他 AI 软件/会话快速上手用**的公开知识库。每次人类 session 之间切换都可能换 AI，所以结论必须完整落盘，不能只存在 BepInEx 内存记忆里。

### 写入流程（四同步铁律）

1. `update_memory`：内存记忆（本 AI session 可直接读）。
2. 改对应 MD 文件：让其他 AI 能读到。
3. 同步本索引 `MEMORY_INDEX.md`：新增/重命名/删除条目时必改。
4. 追加 `CHANGELOG.md`：按时间倒序记录。

### 读取流程（接手前检查）

1. `list_dir memory/`：看文件大小/修改时间。
2. `read_file CHANGELOG.md`：看最近两条时间戳条目。
3. 有变更 → 按需读对应 MD；无变更 → 直接按内存记忆走。

### 日期铁律

- 首选 `Get-Date -Format "yyyy-MM-dd"`。
- 命令异常才问用户。
- 禁止凭系统时间戳或对话历史猜。

### 禁止

- 不要把某个 MOD 的待办写进 `common/`。
- 不再依赖已删除的历史文件；缺失信息必须重新从源码、日志或用户反馈确认。
- 不要在 JSON 配置里写注释；用 `_doc`（全局）/ `note`（单条）承载说明。


