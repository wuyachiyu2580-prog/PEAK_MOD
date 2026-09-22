# Memory Index

更新时间：2026-09-21

- 发行文案最新规则（2026-09-21）：使用英文，保留上一版格式，README 和 CHANGELOG 均明确列出本版更新；见 common/00、05。四 MOD 已纠正，仍无 ZIP。

- 四 MOD ModConfig 更新已构建部署：`mods/ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`（实现设计见同目录 INTEGRATION_PLAN_2026-09-21.md）。PlayersInfo 0.2.5、WhySoLaggy 1.0.5、WhereIsThing 0.1.2、WhereIsMyAmulet 1.0.4；23 项测试通过，最终 DLL 完整实机验收待完成；四个新版发行目录已备齐五件套，无 ZIP、未上传，历史目录未改。

- StateKeeper当前使用归因：`mods/StateKeeper/research/USE_RULES_2026-09-12.md`，analysisVersion7；使用事实与效果归因分离，按字段资源汇总。当前12局Runs回放含一局缺6块，原扩展分析待办见该MOD的TODO。

## 当前结构

- `README.md`：唯一入口和读取顺序。
- `CHANGELOG.md`：memory 变更履历（按时间倒序追加）。
- `TODO.md`：跨 MOD 待办和风险；各 MOD 专属待办在对应目录 `TODO.md`。
- `MEMORY_INDEX.md`：本文件，总览索引。
- `common/`：跨 MOD 通用规则（00-08 共 9 个主题）。
- `mods/`：每个 MOD 的独立入口（README + STATUS + TODO + RECENT + DECISIONS + FILES）和 `temp/current.md` 临时恢复摘要。

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

- 每个 MOD 目录下如有临时记录，当前摘要固定为 `temp/current.md`。
- 旧日期临时文件统一放在 `mods/<ModName>/temp/archive/`。
- 每形成 3 次明确的阶段性判断、排查结论或方案取舍，就追加一次摘要。
- 上下文压缩、会话中断或换 AI 后，先读该 MOD 最新临时 MD，再读正式四件套。
- 各 MOD 当前临时摘要统一为 `mods/<ModName>/temp/current.md`；历史日期文件位于对应 `temp/archive/`。

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

### PlayersInfo（0.2.5 发行文件已备齐 / PEAK 2.4.b）

- `mods/PlayersInfo/README.md`：队友 HUD 聚合概览与功能轮廓。
- `mods/PlayersInfo/RECENT.md`：含 0.2.4 试发行、死亡条排除、骸骨之书影响结论、0.2.3 三档异常图标、死亡/晕倒距离修复、零体力饥饿倒计时、统一刷新、日志审计和发行产物结论，以及此前 0.2.1 功能历史。
- `mods/PlayersInfo/DECISIONS.md`：只读展示不发 RPC、HUD 架构、图标所有权隔离、安全位置解析、原生 `maxStaminaBar` 倒计时、统一刷新和日志门控边界。
- `mods/PlayersInfo/FILES.md`：当前 `0.2.5` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/PlayersInfo/temp/2026-09-07.md`：本轮 0.2.4 试发行、骸骨之书影响判断和待实机验证入口。
- `mods/PlayersInfo/temp/2026-09-06.md`：此前 0.2.4 死亡条修复、0.2.3 实现、构建、日志审计和待实机验证入口。

### StateKeeper（0.1.0 / 开发中）

- `mods/StateKeeper/temp/2026-09-09.md`：最新英文按钮/重命名修复、实际资源证据、字体宽度和新版包哈希。
- `mods/StateKeeper/RELEASE_AUDIT_2026-09-08.md`：最新发布入口；0.1.0自审清理、64项测试、本地ZIP和DLL哈希、未上传与未实机验收边界。
- `mods/StateKeeper/REPORT_REBUILD_2026-09-08.md`：当前实现/验收入口；analysisVersion5复盘报告、62项测试与七局回放、部署结果及Unity待验收项，优先于旧报告。
- `mods/StateKeeper/temp/2026-09-08.md`：最新接续摘要，纠正死亡重复、六个进度点、127处时间回退与卡顿误判。
- `mods/StateKeeper/README.md`：项目定位、展示名称、采集内容、RunId 结束判定、存储结构和当前路径。
- `mods/StateKeeper/PLAN.md`：整体 JSON 合并、合并/分析双进度、双语面板、面板收藏和 PEAK-MAP 匿名提交的后续计划。
- `mods/StateKeeper/RECENT.md`：StateKeeper 改名、本机旧数据迁移、体力阈值和性能优化停点。
- `mods/StateKeeper/DECISIONS.md`：版本、GUID、采样频率、事件降噪、后台写盘和禁止回退项。
- `mods/StateKeeper/FILES.md`：源码、测试工程、构建命令、profile 输出和数据结构。
- `mods/StateKeeper/temp/2026-09-06.md`：本轮恢复入口，含旧数据分析结果和下一步实机验证。

### PEAK-MAP（网站集成调研）

- `mods/PEAK-MAP/README.md`：网站现状、StateKeeper 集成边界和当前未改源码状态。
- `mods/PEAK-MAP/PLAN.md`：网站独立 API、Supabase/R2 存储、适度脱敏、安全校验、关闭机制和实施顺序。
- `mods/PEAK-MAP/DECISIONS.md`：独立 API/表/R2、匿名化、隐私和安全决策。
- `mods/PEAK-MAP/FILES.md`：网站现有上传/R2/安全/UI 文件和计划新增文件。
- `mods/PEAK-MAP/RECENT.md`：2026-09-06 网站调研停点。

### ModConfigDiagnostics（0.1.0 / 诊断与修改建议）

- `mods/ModConfigDiagnostics/README.md`：项目定位、真实报告结论和接手入口。
- `mods/ModConfigDiagnostics/RECENT.md`：0.1.0 已完成能力、2.4.b 报告结果和未完成边界。
- `mods/ModConfigDiagnostics/DECISIONS.md`：只读诊断、活动对象证据和禁止误归因规则。
- `mods/ModConfigDiagnostics/FILES.md`：源码、发行包、profile 报告和反编译依据路径。
- `mods/ModConfigDiagnostics/PLAN.md`：公共 `SettingsCell` 根修复、五个 MOD迁移、诊断增强和验证顺序。
- `mods/ModConfigDiagnostics/temp/2026-09-09.md`：当前报告、根因候选和下一步摘要。

### DreamyAscent（永久暂停/归档）

- 2026-05-24 用户明确要求 `DreamyAscent永久暂停`；除非用户明确恢复，否则不要继续 DA 的功能开发、排查、构建、诊断或 TODO。

- `mods/DreamyAscent/README.md`：项目概览和接手入口。
- `mods/DreamyAscent/RECENT.md`：近期完成内容、当前验证结论和后期物品编辑需求初评。
- `mods/DreamyAscent/DECISIONS.md`：已确认的技术决策、禁止回退项和后期区域物品编辑边界。
- `mods/DreamyAscent/FILES.md`：关键路径、构建命令、诊断目录和关键源码文件。
- `mods/DreamyAscent/archive/MAP_GENERATION.md`：地图生成链路、需求拆分、分层实现路线和历史资料。
- `mods/DreamyAscent/archive/IMPLEMENTATION_MATRIX.md`：后期需求到实现矩阵（归档）。
- `mods/DreamyAscent/MAP_GENERATION_RESEARCH_NOTES.md`：按用户要求多轮通读资源的过程记录、原始依据和多角度实现分析。
- `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`：跨区段物品放置专门记忆，记录来源模板 + 目标子区模型、雨林棕榈放沙漠等例子、风险等级和资料缺口。
- `MOD开发/DreamyAscent/data/map-data/SAMPLE_AUDIT_2026-05-13.md`：项目内地图样本集中审计，记录官方自然样本、TerrainRandomiser 验证样本、完整性、变体覆盖和纯净性判断。
- `MOD开发/DreamyAscent/data/map-data/sample-index.json`：项目内地图样本机器可读索引，供模板快照、对象注册表和回归检查工具使用。
- `MOD开发/DreamyAscent/data/map-data/COLLECTION_V2.md`：Snapshot V2 重采规则，规定官方自然样本和 TerrainRandomiser 强制变体样本分目录采集，且必须包含 `GeneratedChildrenSnapshot.json`。
- `MOD开发/DreamyAscent/data/tools/build_map_data_artifacts.py`：从诊断样本生成模板快照、对象注册表输入和样本回归报告的离线工具。
- `MOD开发/DreamyAscent/data/map-data/generated/`：离线生成产物目录，当前包含 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`。

### WhySoLaggy（1.0.5 发行文件已备齐 / PEAK 2.4.b）

- `mods/WhySoLaggy/README.md`：性能和网络诊断项目入口与 mermaid 能力矩阵。
- `mods/WhySoLaggy/RECENT.md`：1.0.4 全量修复、自动测试、构建结果和联机测试配置。
- `mods/WhySoLaggy/DECISIONS.md`：远端入站阈值、Ownership 分类、有界队列、批量日志和卸载顺序等禁止回退项。
- `mods/WhySoLaggy/FILES.md`：当前 `1.0.5` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
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

### WhereIsThing（0.1.2 发行文件已备齐 / PEAK 2.4.b）

- 发行 README Notes 已声明玩家名仅供娱乐，晚加入可能缺此前放置者记录；部分目标仅显示物品名称和距离。依据及边界见该 MOD DECISIONS/RECENT。

- 2026-09-20最新：Canvas排序降为20000并部署普通0.1.2测试版，保留交互Raycaster；TFA共存待实机复测，见STATUS/RECENT/temp/current。

- `mods/WhereIsThing/README.md`：0.1.2 当前能力、2.4.b 测试状态、owner 边界和分类审计入口。
- `mods/WhereIsThing/RECENT.md`：2.4.b 放置来源索引、绳索/蘑菇 owner 修复、分类校正、正式测试 DLL 和未完成验收。
- `mods/WhereIsThing/DECISIONS.md`：互斥用途分类、ItemSpawnerEnhanced 证据边界、纯客户端 owner、系统排除和禁止回退项。
- `mods/WhereIsThing/FILES.md`：当前 `0.1.2` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/WhereIsThing/PLAN.md`：0.1.2 房主/客户端正反例、分类回归、生命周期和性能验收计划。
- `mods/WhereIsThing/temp/2026-09-04.md`：2.4.b 当前停点、测试 DLL hash 和下一步恢复摘要。
- `mods/WhereIsThing/temp/2026-09-06.md`：ItemSpawnerEnhanced 分类审计、51 项精确覆盖、正式 profile 构建和最新 DLL hash。
- `mods/WhereIsThing/temp/2026-08-14.md`、`temp/2026-08-16.md`：2.1.a 早期开发历史。

### WhereIsMyAmulet（1.0.4 发行文件已备齐 / 实机待验收）

- 最新1.0.4字号补丁：投影Z清零、TMP自动字号关闭；已编译部署，远近字形尺寸实测待确认，见STATUS/RECENT/temp/current。

- `mods/WhereIsMyAmulet/STATUS.md`、`TODO.md`、`temp/current.md`：2026-09-20最终对照30000失败、20000成功；固定20000，移除探针，普通1.0.4已部署。Manual mode禁用行为与排序故障分别判断。

- `mods/WhereIsMyAmulet/README.md`：护符定位 MOD 概览和接手入口。
- `mods/WhereIsMyAmulet/RECENT.md`：1.0.3 Scout Statue 映射修复、标签间距、构建和发布状态。
- `mods/WhereIsMyAmulet/DECISIONS.md`：标签生命周期、雕像映射、FakeItem 识别边界和 ModConfig 本地化规则。
- `mods/WhereIsMyAmulet/FILES.md`：当前 `1.0.4` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/WhereIsMyAmulet/temp/archive/`：历史1.0.2/1.0.3实现和发布记录。

## 当前重点

- `ModConfigDiagnostics` 当前已确认公共 `SettingsCell` 模板上的 `LocalizedText row=0` 是 `LOC: 0` 潜在来源；先按 `PLAN.md` 修公共克隆链和危险 `RefreshCache()`，再迁移五个 MOD的新版菜单类型。当前尚未实施代码修复。

- `WhereIsThing` 当前为 `0.1.2` / PEAK `2.4.b`；发行文件已按用户要求备齐，无 ZIP、未上传。仍需验收分类窗口、绳索、岩钉、蘑菇、owner 和系统目标排除。

- `StateKeeper` 当前为 `0.1.0` 开发阶段；展示名为 `STATE KEEPER` / `状态分析`。当前重点是长时间多人实机验证，并按 `PLAN.md` 规划整体 JSON 合并、合并/分析双进度、双语 ESC 面板、面板收藏和 PEAK-MAP 自愿匿名提交；具体分析算法和发行包暂缓。

- `Lantern_ShootZombies_Night` 当前重点是实机验证客机本地燃料权威：有备用池时只降备用池、不降灯燃料，且远端 fuel 下降不覆盖本地。
- `PlayersInfo` 当前为 `0.2.5` / PEAK `2.4.b`；发行文件已备齐，无 ZIP、未上传。仍需验收语言/下拉、死亡条、图标、晕倒距离、倒计时、观战及多人边界。
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
