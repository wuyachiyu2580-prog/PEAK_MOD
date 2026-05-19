# DreamyAscent

更新时间：2026-05-17

## 项目定位

`DreamyAscent` 是 PEAK 的地形定制中文化与功能修复 MOD。它参考 TerrainCustomiser 反编译代码，但目标是独立实现，不是给原 MOD 打补丁，也不是做兼容层。

## 当前状态

- 版本号暂时固定为 `0.1.0`。
- 项目已从 `TerrainCustomiserCN` 改名为 `DreamyAscent`；插件 ID 为 `com.wuyachiyu.dreamyascent`，构建输出为 `DreamyAscent.dll`。
- 2026-05-13 已收口改名后的 Git 脏状态：旧目录删除和新目录新增已整理为 staged rename/add；旧 `TerrainCustomiserCN` 只允许作为历史来源、反编译参考名或旧运行时数据迁移来源。
- 预览主体已可用。
- 7 个主要区段已有源码默认预览位姿，外部 `DreamyAscent PreviewPoses.json` 仍可覆盖。2026-05-17 已把用户新保存的 `Jungle_Segment`、`Snow_Segment`、`Caldera_Segment` 位姿同步进 DLL fallback 默认值。
- 已接入对象引用诊断和 `ObjectCatalog.json`，用于补足当前 JSON 无法保存 prefab/material 引用的问题。
- 2026-05-17 Snapshot V2 全量采集与审计完成：官方自然样本 21 份、TerrainRandomiser 补变体样本 6 份，总计 27 份，`issues=0`，所有已知 `Beach / Jungle / Roots / Snow / Desert` 变体均覆盖，`GeneratedChildrenSnapshot.schemaVersion=3`，原始快照合计约 8.87GB。
- 已接入第一版只读“区段模板库”UI：按 Segment 展示可复用 item/material、来源 step、默认数量/范围/概率和父子/联机同步标记。
- 已接入 `SubArea` / `PlacementRule` 配置保存层：可从推荐候选添加规则、导出 JSON 并导入配置；当前仍不做实际自定义生成。
- 2026-05-16：`SubArea` / `PlacementRule` 导入回显与增删回环已经实测通过，下一步转向 `template-snapshots.json` 的稳定模板匹配。
- 2026-05-16：第一版运行时自定义放置已接入。`CustomBlank` / `Hybrid` 自动应用低风险 `PlacementRule`，`OfficialTemplate` 只手动应用；当前仅支持低风险静态模板，父子绑定物、生成器壳、Photon/网络物、外部物品和多人同步后置。
- 2026-05-16：`CustomBlank` 的清理语义按用户最新定义收窄为“只保留起始点过渡”。桥、绳子、终点、边缘中段等都应清掉，不再作为空白模板保留结构。
- 2026-05-16：第三关“卡皮巴拉”已确认至少会在 Desert `Platteau/Rocks/Oasis` 下出现，用户也反馈 Snow 关卡会出现。`CustomBlank` 已改成全段兜底清理名称包含 `Capy/Capybara` 或当前 Renderer 材质为 `M_Capybara` 的对象；后续实机要分别用 Desert/Oasis 和 Snow 抽到卡皮巴拉的地图复测。
- 2026-05-15：UI 暂时收口，左下参数说明和焦点联动已稳定，先不继续扩 UI 结构，除非复现回归。
- 中文本地化已改为外置优先：`DreamyAscent Data\localization.zh-CN.json` 是主来源，DLL 内映射只做 fallback，后续新增中文优先写外置文件。
- 当前三层联动仍是 UI/诊断联动：生成物列表显示当前 step 类型和 catalog item/material 数量，用于判断每个区域扫到了哪些生成物；它不代表已经实现自定义生成或重叠检测。
- 预览雾状/白雾遮挡已经从 P0 移出：2026-05-10 多张实机截图显示主预览不再被白雾遮挡；后续仅作为普通回归风险观察。
- `CustomBlank` 当前语义：跳过官方生成器、清理普通官方装饰生成物和大部分连接结构，只保留起始点过渡，然后应用低风险自定义放置规则。它不是“官方模板去装饰版”，而是偏空白创造模式。
- 当前阶段应按“已有第一版低风险运行时放置，但还不是完整额外生成系统”理解；手动放置、跨区段高级放置、绑定物关系、spawner/Photon 物品和多人同步都属于后续实现，不要和当前静态 prefab 放置混为已完成。
- 当前主要待验证项是地图里剩余漂浮物和后续生成稳定性；UI 先暂停改动。
- 运行时生成、地图轮换、多人数同步、导入导出和随机种子仍有后续工作。
- 后期目标新增“按区域管理可添加物品”：每个区域独立配置可生成物品、数量、大小、材质/颜色和外部导入物体。
- “区段模板库”和未来“放置子区”是两层概念：前者是 Segment 可用模板清单，后者才是山顶、山腰、洞口这类 XYZ 编辑区域。
- 区段模板库后期不能被设计成只能用当前 Segment 模板；需要支持跨区段混合选择，但由目标放置子区、兼容规则和默认参数决定能否安全生成。
- 跨区段放置必须按“来源模板 + 目标子区”实现。来源模板只提供 prefab/默认参数/组件/风险，真正落到哪里由目标 `SubArea` 的 XYZ、范围、ray、layer 和约束决定。
- 第一条推荐验证例子是把 Jungle `Jungle_PalmTree_*` 低风险模板放到 Desert 平台/山腰子区；Roots `Redwood`、行李、藤蔓、虫类、岩浆机制等先只读标记。
- 后期交互应把模板库从参数编辑区拆出为独立窗口/侧栏：模板库负责全局/当前区段模板检索，参数面板负责当前子区或生成规则编辑。
- 后期地图编辑要支持两条方向：一是“官方模板模式”，以原版各区段生成器为基础做参数调整；二是“空白自定义模式”，区段默认不放官方物品，由用户从模板库/外部导入自行添加物品生成规则。后续可再支持混合模式。
- 绑定物关系必须作为一等能力处理，例如椰子依附椰子树、椰子树落地，不能只把所有物体当成平级散点。
- HazardSpam 已确认可作为地图生成设计参考：它把模板、Zone/SubZone 和网络生成分开；反编译 TerrainCustomiser/TerrainRandomiser/原版生成代码负责确认真实 LevelGenStep、PropSpawner、PhotonView 同步边界。
- 2026-05-17 起地图数据进入 Snapshot V2：旧 `data/map-data/1.62.a/` 和 `TerrainRandomiser/` 样本已删除，新采集只使用 `1.62.a-snapshot-v2/` 与 `TerrainRandomiser-snapshot-v2/`，每份诊断必须包含 `GeneratedChildrenSnapshot.json`。该轮采集已完成，当前不要再混入旧样本。
- `GeneratedChildrenSnapshot.json` 是后续官方生成结果重建的关键输入；2026-05-17 后要求 `schemaVersion=3`，并带 `relationshipCandidates` 与 `interestingComponentFields`，用于一次样本里检查椰子/椰子树、子生成器、SingleItemSpawner、桥、营火附属物、RisingLava、独立机关等父子/业务关系候选。旧 `RuntimeExport/ObjectCatalog/ObjectReferenceMap` 只能继续支撑模板/对象注册，不能单独用于完整地形重建。
- `generated/template-snapshots.json` 和 `generated/object-registry-input.json` 已基于 Snapshot V2 重跑并随 Release 构建复制到插件目录；当前统计为 135 segment snapshots、193 模板候选、25 材质候选，`sample-regression-report.json status=pass`。
- 诊断内存策略：原始 `GeneratedChildrenSnapshot.json` 不瘦身，但运行时写出已改为流式 JSON；UI 左下样本资产和 Catalog 注册表改为手动加载，避免启动/打开 UI 即加载开发期离线资产。

## 必读文件

- `RECENT.md`：近期已完成和当前验证结论。
- `DECISIONS.md`：已经确认的技术决策，尤其是禁止恢复的旧方案。
- `MAP_GENERATION.md`：地图生成链路、需求拆分、分层实现路线、诊断事实、资料缺口和推荐路线。后续改官方模板、空白自定义、模板库、外部资源、材质或多人同步前必须先读。
- `IMPLEMENTATION_MATRIX.md`：后期需求到实现矩阵。覆盖官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、模板快照和多人同步。
- `CROSS_SEGMENT_PLACEMENT.md`：跨区段物品放置专门记忆。后续做“别的区段的物品放到当前区段”前必须先读。
- `FILES.md`：源码、构建输出、诊断目录和关键文件。
- `MOD开发\DreamyAscent\data\map-data\SAMPLE_AUDIT_2026-05-13.md`：当前地图样本的集中审计记录，包含变体覆盖、诊断完整性、关键 TR 样本和纯净性判断。
- `MOD开发\DreamyAscent\data\map-data\sample-index.json`：当前 Snapshot V2 样本集机器可读索引；后续自动化检查和模板抽取优先读取它。
- `MOD开发\DreamyAscent\data\map-data\COLLECTION_V2.md`：Snapshot V2 采集规则，采集已完成；只有 schema 或游戏版本变化时才需要重跑。
- 根目录 `TODO.md`：该 MOD 的永久待办。

## 接手原则

- 不使用补丁方式包原 TerrainCustomiser。
- 不基于下载包里的 `Map Saves` 做兼容层。
- 不要删除 `TerrainCustomiserCN Files/Exports/Imports/PreviewPoses.json` 这类旧名迁移兼容字符串；它们不是新开发入口，而是为了把旧测试数据复制到 DreamyAscent 新目录。
- 预览相关改动必须优先保证游戏输入不被破坏。
- 用户反馈“日志已更新”时，先查诊断目录，再判断代码改动。
- 用户反馈“还有英文”时，优先补 `DaLocalization` 固定映射或编号规则，不要让 catalog 缺翻译刷日志。
- 地图生成问题不要只按单个截图/单个 grouper 追加特判；先对照 `MAP_GENERATION.md` 的官方模板、空白自定义、跨区段放置、模板库/同步路线判断属于哪一层。
- 后续优先级不要再只按 UI 入口推进：当前先实机验证新版 Snapshot V2 Data 加载和手动样本资产按钮，再建立当前变体内置默认模板基线，之后才继续扩展放置功能。
- 明天接着测 DA 时，优先确认空白模板只留下起始点过渡；如果桥、绳子、终点、边缘中段仍残留，继续修清理边界。
- Snapshot V2 采集和离线产物已完成，不要继续盲目刷图；除非后续改 schema/游戏版本/采集规则，否则下一步是实机验证新版产物并实现内置模板基线。


