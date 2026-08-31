# Memory Index

更新时间：2026-08-31

## 当前结构

- `README.md`：唯一入口和读取顺序。
- `CHANGELOG.md`：memory 变更履历（按时间倒序追加）。
- `TODO.md`：永久待办和风险。
- `MEMORY_INDEX.md`：本文件，总览索引。
- `common/`：跨 MOD 通用规则（00-08 共 9 个主题）。
- `mods/`：每个 MOD 的独立四件套（README + RECENT + DECISIONS + FILES）和 `temp/` 临时思考记忆区。

## 通用规则（common/）

- `common/00_用户偏好.md`：用户身份、沟通风格、接手基线。
- `common/01_协作与记忆规则.md`：四同步铁律、接手前检查、日期规则、每 3 次阶段性判断写入 MOD 临时 MD 的规则。
- `common/02_工程与构建规范.md`：OutputPath/HintPath 规约、L&SZ&N 作为重构参照。
- `common/03_日志与诊断规范.md`：日志分级、FieldProbe DSL、JSON 禁注释。
- `common/04_联机与同步规范.md`：主客机权限、RPC 校验、中途加入同步。
- `common/05_发布与版本规范.md`：README/CHANGELOG/manifest 三文件协同、135 字符限制。
- `common/06_UI与字体规范.md`：CJK 字体四级兜底、FontHelper 标准实现、描边与字号规范。
- `common/07_PEAK版本与反编译基线.md`：PEAK 2.1.a 相对 2.0.a 的业务级变化、MOD 兼容结论和待验证风险。
- `common/08_ModConfig本地化与安全集成规范.md`：ModConfig 中英文显示的 section/key 方案、禁止全局缓存重建、生命周期、迁移步骤和验证清单。

## MOD 四件套（mods/）

### 临时思考记忆区

- 每个 MOD 目录下必须有 `temp/`。
- 当天临时文件命名为 `mods/<ModName>/temp/YYYY-MM-DD.md`。
- 每形成 3 次明确的阶段性判断、排查结论或方案取舍，就追加一次摘要。
- 上下文压缩、会话中断或换 AI 后，先读该 MOD 最新临时 MD，再读正式四件套。
- 当前已初始化：`DreamyAscent/temp/2026-05-19.md`、`DreamyAscent/temp/2026-05-20.md`、`DreamyAscent/temp/2026-05-21.md`、`DreamyAscent/temp/2026-05-24.md`、`ItemInfoCN/temp/2026-05-19.md`、`Lantern_ShootZombies_Night/temp/2026-05-19.md`、`PlayersInfo/temp/2026-05-19.md`、`PlayersInfo/temp/2026-05-21.md`、`PlayersInfo/temp/2026-05-24.md`、`PlayersInfo/temp/2026-05-30.md`、`PlayersInfo/temp/2026-06-04.md`、`PlayersInfo/temp/2026-08-17.md`、`WhySoLaggy/temp/2026-05-19.md`、`WhySoLaggy/temp/2026-08-28.md`、`WhySoLaggy/temp/2026-08-31.md`、`WhereIsMyAmulet/temp/2026-08-31.md`、`TerrainCustomiserCN/temp/2026-05-23.md`、`TerrainCustomiserCN/temp/2026-05-30.md`、`PeakMapBrowser/temp/2026-07-23.md`、`PeakMapBrowser/temp/2026-07-30.md`。

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

### PlayersInfo（0.2.1）

- `mods/PlayersInfo/README.md`：队友 HUD 聚合概览与功能轮廓。
- `mods/PlayersInfo/RECENT.md`：含 2026-08-17 本地额外体力条恢复原版层级/缩放控制、当前值内嵌显示、观战目标保留、队友额外图形条隐藏和物品栏燃料条保留结论，以及此前 0.2.1 功能历史。
- `mods/PlayersInfo/DECISIONS.md`：只读展示不发 RPC、HUD 架构、原版负责本地额外条布局、观战目标、队友额外条抑制、燃料条保留和禁止整体回退边界。
- `mods/PlayersInfo/FILES.md`：源码路径、项目文件、0.2.1 版本和当前 DLL 输出路径（含 `Helpers/FontHelper.cs`、`Helpers/DisplayCharacterHelper.cs`、`MonoBehaviours/TeammateBarAffliction.cs`）。

### DreamyAscent（永久暂停/归档）

- 2026-05-24 用户明确要求 `DreamyAscent永久暂停`；除非用户明确恢复，否则不要继续 DA 的功能开发、排查、构建、诊断或 TODO。

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

### WhySoLaggy（1.0.4 已建立发行包，PEAK 2.3.a）

- `mods/WhySoLaggy/README.md`：性能和网络诊断项目入口与 mermaid 能力矩阵。
- `mods/WhySoLaggy/RECENT.md`：1.0.4 全量修复、自动测试、构建结果和联机测试配置。
- `mods/WhySoLaggy/DECISIONS.md`：远端入站阈值、Ownership 分类、有界队列、批量日志和卸载顺序等禁止回退项。
- `mods/WhySoLaggy/FILES.md`：源码、测试项目、1.0.4 版本和关键 Helper。
- `mods/WhySoLaggy/temp/2026-08-28.md`：本轮修复状态和双客户端实机验收入口。

### OldPC（0.0.1 开发中）

- `mods/OldPC/README.md`：本地旧电脑画面模式和望远镜清晰模式入口。
- `mods/OldPC/RECENT.md`：当前功能范围和未完成的构建/实机验收。
- `mods/OldPC/DECISIONS.md`：本地视觉边界、画质恢复和风险。
- `mods/OldPC/FILES.md`：源码、工程和构建命令。

### TerrainCustomiserCN（0.1.2 已发布，对应原版 0.3.2）

- `mods/TerrainCustomiserCN/README.md`：TerrainCustomiser 中文 UI 版入口、版本对应、玩家侧说明和接手要求。
- `mods/TerrainCustomiserCN/RECENT.md`：0.1.2 发布状态、原版 0.3.2 同步、`.json.old` 只读列表、Collector 状态和已知风险。
- `mods/TerrainCustomiserCN/DECISIONS.md`：中文 UI 边界、原版联机/存档兼容、版本对应、翻译策略、发布决策和禁止回退项。
- `mods/TerrainCustomiserCN/FILES.md`：源码路径、构建命令、输出目录、关键文件、保存路径和发布包内容。

### PeakMapBrowser（0.1.1，已打包）

- `mods/PeakMapBrowser/README.md`：地图库客户端概览、账号功能和接手入口。
- `mods/PeakMapBrowser/RECENT.md`：0.1.1 发布包、账号/个人地图/点赞同步、DPAPI session 安全、当前 IMGUI 页面状态和待实机验证。
- `mods/PeakMapBrowser/DECISIONS.md`：点赞身份规则、DPAPI session 安全边界、服务端退出接口、UI 暂缓决策和禁止回退项。
- `mods/PeakMapBrowser/FILES.md`：客户端/服务端路径、关键源码、API 文档和构建命令。
- `mods/PeakMapBrowser/temp/2026-07-30.md`：0.1.1 发布、安全改动和线上退出接口验证摘要。

### WhereIsThing（0.1.1 已发布，待反馈收口）

- `mods/WhereIsThing/README.md`：多物品位置显示能力、动态类别初稿、0.1.1 发布状态和接手入口。
- `mods/WhereIsThing/RECENT.md`：2.1.a 接口复查、已实现功能、发布状态和剩余验证。
- `mods/WhereIsThing/DECISIONS.md`：动态 ItemDatabase、游戏 Localization、快捷键、窗口和背包位置边界。
- `mods/WhereIsThing/FILES.md`：源码路径、依赖、构建命令和输出位置。
- `mods/WhereIsThing/PLAN.md`：分阶段研究、实机验证、收口和发布计划。
- `mods/WhereIsThing/temp/2026-08-14.md`：当前阶段压缩恢复摘要。

### WhereIsMyAmulet（1.0.3 已发布）

- `mods/WhereIsMyAmulet/README.md`：护符定位 MOD 概览和接手入口。
- `mods/WhereIsMyAmulet/RECENT.md`：1.0.3 Scout Statue 映射修复、标签间距、构建和发布状态。
- `mods/WhereIsMyAmulet/DECISIONS.md`：标签生命周期、雕像映射、FakeItem 识别边界和 ModConfig 本地化规则。
- `mods/WhereIsMyAmulet/FILES.md`：源码、构建命令、实际 profile 输出和 1.0.3 发行包路径。
- `mods/WhereIsMyAmulet/temp/2026-08-20.md`：1.0.2 实现和验证入口。
- `mods/WhereIsMyAmulet/temp/2026-08-31.md`：1.0.3 映射修复、发行产物和剩余验收入口。

## 当前重点

- `WhereIsThing` 0.1.1 非 ZIP 发行目录已完成；当前重点是根据实机和玩家反馈继续确认重名合并、行李箱目标、场景危险/地标、雕像碎片、自适应窗口、中文字体、鼠标恢复和大量目标性能。

- `Lantern_ShootZombies_Night` 当前重点是实机验证客机本地燃料权威：有备用池时只降备用池、不降灯燃料，且远端 fuel 下降不覆盖本地。
- `PlayersInfo` 的 0.2.0 发布准备记录已归档；当前状态以 0.2.1 专属 memory 四件套和 2026-08-17 临时记忆为准。本地额外条已恢复 PEAK 原版层级/缩放控制，仍待实机确认布局、石化、观战、队友额外条抑制、背包燃料和性能。
- `DreamyAscent` 已于 2026-05-24 永久暂停/归档。此前预览、模板库、Snapshot V2、官方生成链和 zero-output 恢复等资料仅作为历史记录，不作为当前重点推进。
- `TerrainCustomiserCN` 已发布 0.1.2，对应原版 0.3.2。当前重点是后续玩家反馈漏翻时补 `DisplayNameTranslator.cs`、重建 Release、更新发布包；若玩家反馈旧地图缺失，先核对新持久化目录和旧插件目录，不要自动迁移；任何功能改动前先读 `DECISIONS.md` 的联机/存档兼容禁止回退项。
- 其他 MOD 新增功能前先读对应 `RECENT.md` 和 `DECISIONS.md` 的"禁止回退"条款。

## 记忆维护规则

### 定位说明

`memory/` 是**给其他 AI 软件/会话快速上手用**的公开知识库。每次人类 session 之间切换都可能换 AI，所以结论必须完整落盘，不能只存在 BepInEx 内存记忆里。

### 写入流程（四同步铁律）

1. `update_memory`：内存记忆（本 AI session 可直接读）。
2. 改对应 MD 文件：让其他 AI 能读到。
3. 同步本索引 `MEMORY_INDEX.md`：新增/重命名/删除条目时必改。
4. 追加 `CHANGELOG.md`：按时间倒序记录。

### 临时思考记忆

- 具体 MOD 的阶段性思考结果写入 `mods/<ModName>/temp/YYYY-MM-DD.md`。
- 每 3 次明确阶段性判断追加一次摘要。
- 该文件用于压缩恢复，不替代正式 `RECENT.md` / `DECISIONS.md` / `TODO.md`。

### 读取流程（接手前检查）

1. `list_dir memory/`：看文件大小/修改时间。
2. `read_file CHANGELOG.md`：看最近两条时间戳条目。
3. 处理具体 MOD 时先读该 MOD `temp/` 下最新日期文件，尤其是压缩恢复后。
4. 有变更 → 按需读对应 MD；无变更 → 直接按内存记忆走。

### 日期铁律

- 首选 `Get-Date -Format "yyyy-MM-dd"`。
- 命令异常才问用户。
- 禁止凭系统时间戳或对话历史猜。

### 禁止

- 不要把某个 MOD 的待办写进 `common/`。
- 不再依赖已删除的历史文件；缺失信息必须重新从源码、日志或用户反馈确认。
- 不要在 JSON 配置里写注释；用 `_doc`（全局）/ `note`（单条）承载说明。
