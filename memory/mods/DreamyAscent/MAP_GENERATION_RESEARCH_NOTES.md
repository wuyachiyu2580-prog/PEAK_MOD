# DreamyAscent Map Generation Research Notes

更新时间：2026-05-18

本文件是研究草稿，记录多轮阅读过程中的原始依据、临时结论和资料缺口。正式、稳定结论整理到 `MAP_GENERATION.md`；跨区段放置的专门流程和例子整理到 `CROSS_SEGMENT_PLACEMENT.md`；本文件保留给后续追溯“为什么这么判断”。

## 2026-05-18 调试经验：官方生成链先看前后差异

- 当官方模板生成后出现“只剩地形/生成物归零”，不要先假设是样本缺失、变体缺失或 `CustomBlank` 污染；先用生成前后的 `GeneratedChildrenSnapshot` 或运行时 trace 比较同一 segment/grouper/step 的直接子物数量。
- 本次已确认关键现象是 Beach 官方 `RunSegment` 后 `PlateauProps`、`WallProps` 从非 0 变为 0，而 baseline 已命中当前变体默认模板，所以问题应优先追 `PropGrouper.RunAll(true)`、原版 step 清理/重建、运行时引用和预览/刷新时机。
- 已在 `DaRuntimeEditService` 为 `RunSegment` 入口加调用堆栈，为 `RunGrouper` 和白名单 step 加前后 child/descendant delta。后续看日志时优先搜索 `Generation trace:`，重点看 `grouper delta`、`zeroedSteps`、`segment delta`。
- 该 trace 是临时定位工具，不应长期保留为高频 Info 日志；问题定位后要降级、加开关或删除，避免用户正常点击生成时日志膨胀。
- 反编译 1.62.a `PropGrouper.RunAll()` 的当前可见路径：`ClearAll()` 后把 Early steps 加进 list，把 Late steps 加进 local `late`，但主路径只执行 Early list 和 `AfterCurrentGroupTiming` deferred；local `Done()` 才执行 Late list，但主路径未调用。实机日志与此吻合：Late Props 组被清空后没有重建。
- DreamyAscent 现在对 `PropGrouper.timing == Late` 使用替代 pipeline，不再直接调用 `RunAll(true)`；对 Early grouper 仍保持原版路径。这个设计的关键点是“替代”而不是“补跑”，避免恢复 2026-05-17 删除过的 double-run late 逻辑。
- 下次日志验收标准：Late Props 组应出现 `Generation trace: late grouper pipeline used`，且 `grouper delta` 不应再出现 `zeroedSteps=... children N->0`；若仍归零，继续看该组是否 `timing=Late`、`executedSteps` 是否为 0、是否有 step Execute 异常。
- 因旧 DLL 已经在当前 live scene 把 Beach `PlateauProps/WallProps` 清成 0，修复验证应要求用户重开游戏或换新地图。对已清空场景重复点击不能证明修复失败，因为原始生成物和部分 runtime 状态已经被旧路径破坏。

## 阅读范围

- DreamyAscent 当前源码：`MOD开发/DreamyAscent/DreamyAscent` 下所有 `.cs`、`.csproj`、`.slnx`。
- DreamyAscent 当前记忆：`memory/mods/DreamyAscent/*.md`，以及根 `TODO.md`、`CHANGELOG.md`、`MEMORY_INDEX.md`。
- 运行时诊断：`DreamyAscent Diagnostics` 下最近有效的 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`、`CustomBlankRemaining_*.json`。
- PEAK 1.61.b 反编译地图生成相关代码：`MapHandler`、`Biome/BiomeVariant`、`PropGrouper`、`LevelGenStep`、`PropSpawner*`、`DecorSpawner`、`DesertRockSpawner`、`PSM_*`、`PSC_*`、`PostSpawnBehavior`、`SingleItemSpawner`、地图机制/材质/生成约束相关类。
- TerrainCustomiser 反编译代码：导出、UI、属性编辑、生成刷新和网络工具相关文件。
- TerrainRandomiser 反编译代码：地图设置、Biome 数据、生成补丁、同步和 PhotonView 处理相关文件。
- HazardSpam 源码和反编译可用代码：模板注册、Zone/SubZone、Spawner 创建、网络同步、设置保存和 UI 描述层。

## 排除项

- `NullableAttribute`、`NullableContextAttribute`、`EmbeddedAttribute`、`MonoTODOAttribute`、自动生成 assembly metadata 等无业务信息文件。
- PEAK 全量游戏逻辑中与地图生成/放置/同步无关的角色、道具、成就、UI、音频等类；它们不作为本轮“地图生成资源”的主体。

## 审阅轮次 A：资源盘点与事实记录

### 盘点方式

- DreamyAscent 当前业务源码按文件全量盘点：18 个 `.cs` 文件，约 8644 行；排除 `obj/`、`bin/`、自动生成 nullable/assembly metadata。
- TerrainCustomiser 反编译业务源码按文件盘点：29 个 `.cs` 文件，约 4218 行；它是当前数据模型和 IMGUI/属性编辑来源。
- TerrainRandomiser 反编译业务源码按文件盘点：15 个 `.cs` 文件，约 2360 行；它是随机种子、初始化期地图替换、PhotonView ID 同步参考。
- HazardSpam GitHub 源码按文件盘点：40 个 `.cs` 文件，约 7070 行；它是“模板、子区、主机生成点、RPC 广播”参考。
- HazardSpam 反编译附带 NetGameState 按文件盘点：32 个 `.cs` 文件，约 3233 行；它提供 Zone/SubZone、MapObjectPaths/Refs、SegmentMapper/SubZoneHelper 参考。
- PEAK 1.61.b 反编译地图生成核心重点读取：`MapHandler`、`Biome`、`BiomeVariant`、`VariantObject`、`PropGrouper`、`LevelGenStep`、`PropSpawner*`、`DecorSpawner`、`DesertRockSpawner`、`PropDeleter`、`PSM_*`、`PSC_*`、`PostSpawnBehavior`、`SingleItemSpawner`，合计约 3055 行。
- 诊断目录确认：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/terrain/BepInEx/plugins/DreamyAscent Diagnostics`，含 5 个地图组合的 `RuntimeExport.json`、`NameMap.json`、`ObjectReferenceMap.json`、`ObjectCatalog.json`，以及若干 `CustomBlankRemaining_*.json`。

### DreamyAscent 事实

- 主数据模型仍是 `DaTerrainData -> Map -> Segments -> Groupers -> Steps -> Properties/Modifiers/Constraints/PostConstraints`。
- `DaSegmentEditMode` 目前只有 `OfficialTemplate` 和 `CustomBlank`。`OfficialTemplate` 调原版 `PropGrouper.RunAll(true)`；`CustomBlank` 手动清理当前区段生成物。
- `DaTerrainExportService` 只保存可序列化叶子字段和少数白名单字段；`UnityEngine.Object`、数组、集合等会被跳过，所以 `props`、`Material`、`Transform[] spawnPoints`、外部 prefab 引用不能靠 `RuntimeExport.json` 复原。
- `DaObjectReferenceDiagnosticService` 和 `DaObjectCatalogDiagnosticService` 已能把 prefab/material/PhotonView/父子生成候选输出到诊断 JSON，但当前仍是只读索引/诊断，不是可执行的资源注册表。
- `DaRuntimeEditService` 现状仍依赖 live scene 的 `SourceSegment`、`SourceRoots`、`PropGrouper`、`LevelGenStep`、constraint 引用；引用缺失时尝试按当前场景重新绑定。
- `CustomBlank` 当前是“跳过官方生成器并清理已生成官方子物”，不是绝对空白地图。Roots 的墙体/地形 splitmesh、水体、粒子，Caldera/Volcano 的岩浆机制对象会作为结构/机制残留出现。
- 官方模板模式当前存在保护性跳过：Roots 跳过 `PlateauRocks` / `WallRocks`；Jungle 跳过 `Rocks_Plat` / `Rocks_Wall`，原因是已有地图上重复执行会造成乱石堆叠。

### 原版地图生成事实

- 原版链路是 `MapHandler -> MapSegment/variant -> PropGrouper -> LevelGenStep -> PropSpawner/DecorSpawner/PropDeleter/... -> constraints/modifiers/postConstraints`。
- `PropGrouper.RunAll()` 会先 `ClearAll()`，然后执行 early/normal/deferred steps；late timing 在反编译里通过 local `Done` 路径执行，DreamyAscent 目前补跑 late steps。
- `PropSpawner.Clear()` 会删除 step transform 下的 children，并清 `_propSpawnData` 和 `_deferredSteps`。
- `DecorSpawner` 依赖 `spawnPoints: Transform[]`，`Execute()` 直接读取 `spawnPoints[i].position`。如果手动清理破坏这些 spawn point transform，后续会异常或生成不完整。
- `DesertRockSpawner.Clear()` 依赖 `Enterences` / `Inside` 子节点；DreamyAscent 曾在官方生成前预清理这些层级，导致 `Clear()` NRE 和 Desert 空心化。
- `PSM_ChildSpawners` / `PSB_ChildSpawners` 表明父子/依附物必须先生成父物，再执行父物体内的子 `LevelGenStep`；不能把椰子、树、地面约束当成平级散点。
- `PSM_SingleItemSpawner` 表明单物品生成器壳要在运行时绑定 `objToSpawn` prefab，且原版物品生成会涉及 `SingleItemSpawner` 和 Photon。
- `PSM_SetMaterial*`、`PSM_ReplaceMaterial`、`PSC_RequiredMaterial`、`PSC_BannedMaterial` 都围绕 `sharedMaterial/sharedMaterials` 工作；这是规则参考，但 DreamyAscent 的 UI 改材质不能直接污染共享材质。

### 诊断事实

- 最新 5 个 `RuntimeExport.json` 地图组合：
  - `Beach/Jungle/Snow/Caldera/Volcano`：5 segments，20 groupers，142 steps。
  - `Beach/Roots/Desert/Caldera/Volcano`：5 segments，20 groupers，165 steps。
  - `Beach/Jungle/Desert/Caldera/Volcano`：5 segments，18 groupers，151 steps。
  - `Beach/Roots/Snow/Caldera/Volcano`：5 segments，26 groupers，175 steps；旧样本里 Roots 仍含多个 inactive variant 分支，说明历史导出不能作为最终模板基准。
  - `Beach/Desert/Caldera/Volcano`：4 segments，4 groupers，36 steps；明显不完整/特殊状态，只能当异常样本。
- Roots/Desert 组合结构：
  - Beach：`PlateauProps:10, PlateauRocks:5, WallProps:9, WallRocks:12`
  - Roots：`PlateauProps:16, PlateauRocks:5, Redwood:4, WallProps:11, WallRocks:10, Waterfalls:1`
  - Desert：`Platteau:1, Props:15, Rocks:6, Props:20, Rocks:12`；`Props/Rocks` 重名，后续必须靠层级路径/稳定 ID 区分。
  - Caldera：`LavaRivers:4, Props:4, Rocks:10`
  - Volcano：`Edges:5, Middle:5`
- Jungle/Snow 组合结构：
  - Jungle：`Pops_Plat:9, Props_Wall:14, Rocks_Plat:3, Rocks_Wall:6`
  - Snow：`Lights:2, PlateauProps:4, PlateauRocks:5, Rocks:9, IceRockSpawn_L:11, IceRockSpawn_R:11, WallProps:4`
- Step 类型主要是 `PropSpawner`，另有 `PropSpawner_Line`、`DecorSpawner`、`PropDeleter`、`DesertRockSpawner`。
- `ObjectCatalog.json` 最新统计：
  - Jungle/Snow：124 items，11 materials，item roles 包含 `step-prop-prefab=122`、`parent-child-template=2`。
  - Roots/Desert：161 items，19 materials，item roles 包含 `step-prop-prefab=155`、`parent-child-template=5`、`single-item-prefab=1`。
  - Roots/Snow 旧样本：145 items，23 materials，`parent-child-template=8`，但含历史 variant 导出污染，不能单独作为模板基准。
- `CustomBlankRemaining` 最新结论：
  - Desert 最新只剩 `GroundMesh=1`，这是地形骨架，不是普通装饰。
  - Caldera 最新只剩 `River/Coll`，旧样本有 `ash/Bubbles`，后续要以最新 DLL+最新日志复核。
  - Volcano 最新只剩 `RisingLava/Lava` 下 `Plane/Coll`。
  - Jungle/Beach/Snow 多轮样本 candidates=0。
  - Roots 仍有 184/186 个候选，主要是 `Ground`、`Water`、`Water Collision`、`SporesParticles`、`WallBottom/Splitmesh*`，这些更像结构/机制/地形对象，不应按普通物品清理。

### 参考代码价值

- TerrainCustomiser：证明 `Segment -> Grouper -> Step -> scalar properties/constraints` 的编辑模型可行；不解决新增 prefab、材质、外部资源、父子依赖和多人同步。
- TerrainRandomiser：证明应在地图初始化期由主机同步设置/seed，并收集/分发 `PhotonView` ID；适合参考中途加入和多人一致性。
- HazardSpam：证明应把模板来源、目标 Zone/SubZone 区域、生成位置、网络广播拆开；它复制目标区域 spawner 的 area/ray/layer/constraints，再使用模板 prefab，由主机生成 positions/rotations 并 RPC 给所有人。
- NetGameState：提供 Zone/SubZone、MapObjectPaths/Refs、SegmentMapper/SubZoneHelper 这类稳定路径/区域枚举参考；DreamyAscent 不能只用中文显示名或当前场景 instance id 当长期存档键。

## 审阅轮次 B：多种实现角度交叉分析

### 角度 A：官方模板模式

目标：保留原版区段生成器，编辑其数量、大小、范围、概率等标量参数，再重跑原版 `PropGrouper`。

可实现依据：

- DreamyAscent 已能导出 `nrOfSpawns`、`minSpawnCount`、`area`、`scaleMinMax`、`chanceToUseSpawner`、`overallSpawnChance` 等字段。
- 原版 `PropGrouper.RunAll(true)` 会处理 Clear/Execute/deferred；适合继续作为官方模板模式的核心。
- TerrainCustomiser 的模型也基本是这一层编辑，说明这个方向是最稳的近期功能。

边界：

- 官方模板模式不能在调用 `RunAll()` 前手动清理 step/grouper 子层级，否则会破坏 `DecorSpawner.spawnPoints`、`DesertRockSpawner.Enterences/Inside`、父子生成器层级。
- 只靠 grouper 名称不够，Desert 有重复 `Props/Rocks`，后续必须加入层级路径或稳定 ID。
- 对 Roots/Jungle 的岩石组继续使用“保护性跳过”只能作为短期措施。后续应内置模板快照和 step 级白名单，而不是无限追加 `if segment == X && grouper == Y`。
- `CustomBlank -> OfficialTemplate` 不能承诺完整恢复，因为清理会改变 live scene 的运行时层级和引用状态。

### 角度 B：空白自定义模式

目标：区段默认不继承官方物品，由用户自己添加物品/规则，从空白地图开始创造。

可实现依据：

- 当前 `CustomBlank` 已能清掉大多数官方生成物：Jungle/Beach/Snow/Desert 多轮诊断接近 0，Caldera/Volcano 只保留岩浆机制。
- `CustomBlankRemaining` 已能告诉我们“哪些东西仍在”，可用于区分结构对象、机制对象、普通装饰。

必须新增的数据层：

- 空白区段没有可编辑的原版 `LevelGenStep` 时，必须有 `DaPlacementData` 或类似结构保存自定义规则。
- 需要 `Segment -> SubArea -> PlacementRule`：子区保存中心 XYZ、形状/范围、落地射线方向、layer、法线/材质/高度约束、允许模板、清理策略。
- 需要生成物 ownership：哪些物体由 DreamyAscent 创建，哪些是原版结构，哪些是外部资源；清理时只能删除本 MOD owning 的物体或明确归类的官方装饰。

边界：

- Roots 的 `Ground/Water/Wall/Splitmesh/Spores` 等不应被默认清理；这些更像地形/机制，不是普通可添加物品。
- Caldera/Volcano 的岩浆机制对象应默认保留；如果未来要绝对空白，需要独立开关和风险提示。
- 漂浮物不能只靠“再清理一个路径”解决，必须记录每条规则的落地依据、父子依附、post constraints 和生成后验证。

### 角度 C：全局模板库、外部资源、材质、父子依赖、多人同步

目标：模板库可跨区段混合，能添加游戏内 prefab 和外部 Unity 物体，支持材质/颜色覆盖，并多人一致。

可实现依据：

- `ObjectCatalog.json` 已经能列出 `step-prop-prefab`、`parent-child-template`、`single-item-prefab`、材质角色、PhotonView 标记、来源 step 默认参数。
- HazardSpam 已证明“模板来源”和“目标区域”可以分离：模板提供 prefab/modifier，目标区域提供 area/ray/layer/constraints。
- TerrainRandomiser 已证明多人应走主机权威设置/seed/PhotonView ID 同步。

必须新增的数据层：

- `DaObjectRegistry`：资源稳定 ID、来源 map/segment/grouper/step/path、prefab/material 加载方式、组件摘要、PhotonView 风险、默认缩放/碰撞/落地策略。
- `DaSubAreaData`：山顶、山腰、洞口这类 XYZ 子区域，不等同于 Segment，也不等同于 `PropSpawner.area` 的显示框。
- `DaPlacementRuleData`：模板 ID、数量、随机缩放、随机旋转、概率、约束、父子依赖、材质覆盖、清理策略。
- `DaMaterialOverrideData`：renderer 路径、材质槽位、颜色或材质 key、是否递归作用子物体、回滚信息。
- `DaNetworkSpawnData`：主机生成出来的 positions/rotations、模板 ID、子区 ID、必要 PhotonView ID、资源缺失降级。

边界：

- 不能把当前 `ObjectCatalog` 直接当可执行模板库；它缺可加载 prefab 引用和资源生命周期。
- 不能把 `UnityEngine.Object` 的 instance id 当存档键；换地图/重进/客机都会失效。
- 外部 Unity 物品需要明确格式：AssetBundle、Addressables、场景对象克隆、还是仅当前游戏 prefab 扫描。没有格式资料前不能承诺完整外部导入。
- 材质替换不能直接照搬原版 `sharedMaterial` 写法。UI 改色优先 MaterialPropertyBlock 或实例材质副本，并保存恢复路径。

### 角度 D：内置模板快照与诊断

目标：后期每个变体内置一份模板，减少依赖当前 live scene 扫描。

可实现依据：

- 用户已经明确担心“每次读取实时地图不牢靠”，目前故障也证明 live scene 会被清理/重跑污染。
- 最新诊断已有多个地图组合，能作为模板快照采样起点。

必须做法：

- 在干净新图、每个组合/变体进入后导出完整 `RuntimeExport/ObjectCatalog/ObjectReferenceMap/CustomBlankRemaining`。
- 为每个模板快照记录游戏版本、地图组合、segment、variant、grouper path、step path、源文件时间。
- 内置模板只能作为“官方默认结构/参数/模板来源”，运行时仍要把模板绑定到当前场景实际对象或用自定义 spawner 生成。

### 角度 E：跨区段物品放置

目标：允许从别的区段选择物品模板，放到当前区段的指定子区，例如把雨林的棕榈放到沙漠平台，把沙漠的普通仙人掌放到雨林平台。

可实现依据：

- 原版 `PropSpawner.GetRandomPoint()` 从当前 spawner transform、`area`、raycast、`layerType` 算落点，`Spawn()` 才使用 `props` 里的 prefab。说明“目标区域的落地参数”和“来源 prefab”天然可以拆开。
- HazardSpam 明确拆分 `PropPrefabs` 与 `PropSpawners[(Zone, SubZoneArea)]`。`CreateSpawner()` 复制目标区域 spawner 的 `area/rayDirectionOffset/rayLength/raycastPosition/layerType`，但 `props` 使用来源模板 `propSpawnerPrefab.props[0]`。
- HazardSpam 的 `SpawnerLogic.GenerateSpawnPoints()` 只生成 positions/rotations，`NetComm.SpawnHazardsNetwork()` 再由主机广播给所有客户端。说明跨区段生成物不能让客机各自随机。
- DreamyAscent `ObjectCatalog.json` 已能给出来源模板、默认参数、`hasChildGeneration`、`hasSingleItemSpawner`、`hasPhotonView`、components 和 renderer materials。

实际例子：

- 雨林棕榈放到沙漠平台：`Jungle_Segment/Pops_Plat/Palms` 下 `Jungle_PalmTree_Thick/Crook/Thin` 均为 `step-prop-prefab`，无子生成、无 PhotonView，默认 `nrOfSpawns=50`、`area=401.5,150`。目标如果是 Desert 平台，应使用 Desert `SubArea` 的 ray/layer/范围，数量先限制 3-10，不能直接继承雨林原 step 的数量和世界 transform。
- 沙漠普通仙人掌放到雨林平台：Desert `Props/Cactus` 中 `Cactus Ball Big` 较低风险；`Cactus Ball Base`、`Tall Cactus`、`Short Cactus` 带 `hasChildGeneration=true`，必须等父子依赖模型完成后再允许。
- Roots `Redwood` 放到其他区段：诊断显示它是 `parent-child-template`，组件含 `PropGrouper`、`MultipleGroundPoints`、`SpawnConnectingBridge`。它不是普通树模型，跨区段放置前必须有父子组、连接桥、落地验证和同步策略。
- 行李、藤蔓、虫类：Jungle/Roots 的 `Luggage*`、`JungleVine`、`SpiderDropper`、`Beetle` 等有 `PhotonView` 或 AI/物理组件。第一阶段只能只读标记，不能直接生成。

必须新增：

- `TemplateSource`：来源 map/segment/grouper/step/path、prefab 稳定 ID、默认参数、组件摘要、风险等级。
- `SubArea`：目标区段内的山顶、山腰、洞口、平台、墙面等 XYZ 子区，不是 `Segment`，也不是旧 UI 里的区段模板库。
- `PlacementRule`：模板 ID、目标子区 ID、数量、缩放、随机旋转、落地策略、材质覆盖、父子依赖、清理策略。
- `DaNetworkSpawnData`：主机生成的 positions/rotations、模板 ID、目标子区 ID、必要 PhotonView ID 和资源缺失降级。

边界：

- 不能把来源区段的 `PropGrouper.RunAll()` 拿到目标区段运行；那会使用来源层级、来源约束和来源子生成器，导致漂浮、穿模、变体叠加或机制污染。
- 不能把来源 step 的 `nrOfSpawns` 原样复制到目标子区；例如 Jungle 树的 50、Monsteras 的 500、Weed 的 1000 放到小子区会过量。
- 高风险模板必须先禁用或实验开关隔离：`hasChildGeneration`、`hasSingleItemSpawner`、`hasPhotonView`、AI、Rigidbody、全局机制、伤害/触发器对象。

### 角度 F：UI 交互与窗口拆分

目标：让用户能从模板库添加物品到目标子区，而不是在参数窗口里迷路。

可实现依据：

- 当前 `DaCustomiserWindow` 已有 `参数 / 区段模板库` 标签页，但模板库仍是只读列表。
- HazardSpam UI 用 Zone/SubZone 作为一级/二级导航，具体 hazard 行可增删，说明“先选区域，再增删规则”的交互更适合长期编辑。

建议：

- 模板库从参数面板拆为独立窗口或固定侧栏，支持搜索、来源区段筛选、全局/当前区段切换、风险过滤。
- 目标子区在主视图中选择或创建；模板库右键“添加到当前子区”，或拖拽到预览/高亮区域。
- 参数面板只编辑当前 `PlacementRule`：数量、缩放、随机旋转、材质覆盖、生成概率、清理策略。
- 高风险模板显示原因并禁用添加按钮，例如“带 PhotonView，需同步方案”“带子生成器，需父子组”。

### 角度 G：多人同步与中途加入

目标：自定义生成物在主客机一致，且中途加入能补状态。

可实现依据：

- TerrainRandomiser 用 room property 同步 `roomMapSettings`，并用 `propViews` 分发主机分配的 PhotonView ID。
- HazardSpam 由主机创建 spawner 标识，再广播 positions/rotations；客户端按同一模板和位置实例化。
- 原版生成物若带 `PhotonView`，客机本地随机或本地 Allocate ID 都会导致不同步。

必须做法：

- 主机权威生成：主机决定配置、seed、目标子区、positions/rotations、ViewID。
- 客机只复现：按收到的模板 ID 和 positions/rotations 实例化，资源缺失时记录降级，不自行换模板。
- 中途加入需要补发当前 active `PlacementRule` 和已生成结果；不能只依赖开局 RPC。

### 角度 H：材质/外部资源/性能

目标：避免把材质和外部资源当作普通字段直接写，避免性能与共享材质污染。

可实现依据：

- 原版 `PSM_SetMaterial*`、`PSM_SetRandomMaterial`、`PSM_ReplaceMaterial` 都写 `sharedMaterial/sharedMaterials`。这说明材质规则存在，但不能照搬到运行时 UI。
- `ObjectCatalog` 已输出 renderer materials、shader、color、mainTexture，可作为材质候选清单。
- TerrainCustomiser/TerrainRandomiser 都有批处理、分帧、ViewID 收集参考；大规模 Instantiate/Raycast/RPC 不能一次性无上限执行。

必须补：

- 外部 Unity 物品格式：AssetBundle、Addressables、场景对象克隆，或仅游戏内 prefab。
- 材质覆盖数据：renderer path、material slot、颜色/材质 key、是否递归、回滚策略。
- 性能基准：每帧 raycast 数、Instantiate 数、RPC 包大小、PhotonView 数量上限。

### 角度 I：需求逐项验算

目标：把用户提出的需求逐条落到架构，不再把所有功能混成“模板库混合”。

验算结果：

- 官方模板：已有原版 `LevelGenStep` 可编辑，继续走 `OfficialTemplate`。边界是不能预清理原版层级。
- 空白创造：没有原版 Step 可承载新增物品，必须新增 `SubArea` 和 `PlacementRule`。
- 当前区段选物：当前只读 `ObjectCatalog` 可作为模板来源，但要升级到可执行 `DaObjectRegistry`。
- 别的区段物品放到当前区段：必须使用来源模板 + 目标子区，不能把来源 step 整体搬过去。
- 衍生/绑定物：`hasChildGeneration=true` 的模板必须按父子组处理，不能平级散点。
- 外部 Unity 物品：必须先确定资源格式并注册到对象库，当前 JSON 不能保存 Unity 引用。
- 材质/颜色：原版有材质 modifier 证据，但直接写 `sharedMaterial` 会污染全场，运行时 UI 要实例级覆盖。
- UI 分窗：只读阶段同窗可以；添加、搜索、拖拽、风险过滤阶段应拆成模板库窗口/侧栏和规则参数面板。
- 多人：主机生成模板/位置/PhotonView ID，客机复现；中途加入要补发状态。

对应文件：

- 稳定矩阵已整理到 `IMPLEMENTATION_MATRIX.md`。
- 跨区段细节已整理到 `CROSS_SEGMENT_PLACEMENT.md`。

### 角度 J：测试用例倒推

目标：从下一步实机可测动作倒推最小可落地功能，避免一上来做高风险对象。

推荐测试链路：

1. 手动建一个 `SubArea`：例如 `Desert_Segment / PlateauTop`，保存中心 XYZ、范围、ray、layer。
2. 从当前游戏内低风险模板选一个普通 prefab：例如 Jungle `Jungle_PalmTree_Thick`。
3. 生成 3-10 个实例，输出每个实例的 position/rotation/scale、ray 命中、离地高度。
4. 清理本规则，确认只删 DreamyAscent ownership 下的对象。
5. 再开多人：主机生成 positions/rotations，客机只复现。

第一阶段不测试：

- Roots `Redwood`、`Mushroom tree Flat tall` 等父子模板。
- `Dynamite`、`Luggage*`、`JungleVine`、虫类等 Photon/Item/AI/物理模板。
- Caldera/Volcano 岩浆机制。
- 外部 AssetBundle 和材质批量随机。

倒推出来的最小新增代码：

- `DaSubAreaData`
- `DaPlacementRuleData`
- `DaObjectRegistryService`
- `DaPlacementService`
- 生成后诊断 JSON
- 后续 `DaNetworkSpawnService`

## 审阅轮次 C：落地方案、风险和资料缺口

### 已知故障反推

- Desert 空心：根因不是某个 JSON 字段缺失，而是官方模板生成前手动 Destroy 子层级，破坏 `DesertRockSpawner.Clear()` 依赖的 `Enterences/Inside`。结论：官方模板模式严禁预清理原版层级。
- Roots/Jungle 乱石：根因包括历史 fallback 导出 inactive variant 分支，以及在已有地图上重复运行岩石 grouper。结论：短期跳过危险岩石组，长期用内置模板快照 + step 级白名单/子区规则替代。
- Roots 空白后切官方只剩蘑菇：根因是 `CustomBlank` 清理改变 live scene 层级/引用，官方生成器不能保证完整恢复。结论：`CustomBlank -> OfficialTemplate` 只能提示重开/新图复测，不应承诺自动恢复。
- 漂浮物：可能来自父子依附丢失、落地 ray/constraint 不适配目标子区、清理后保留了失去父级的生成物、或材质/碰撞面判定不完整。结论：后续每条自定义规则必须保存落地策略和生成后验证，不要只按名字清理。
- 材质污染：原版 modifier 使用 `sharedMaterial`，若 UI 直接改共享材质会影响全场同源对象。结论：DreamyAscent 的材质 UI 需实例级覆盖/可回滚。
- 多人不同步：当前 DreamyAscent 没有主机权威的模板/positions/PhotonView ID 流程。结论：未来自定义生成必须参考 TerrainRandomiser/HazardSpam，由主机决定设置、种子、位置和需要同步的 ID。

### 稳定能力

- 编辑原版 `LevelGenStep` 的标量参数。
- 导出当前地图结构和对象引用诊断。
- 第一版只读模板库展示。
- `CustomBlank` 作为运行时清理实验路径，能清掉大部分普通生成物。
- 高亮显示原版 step 的 `area` 范围，用于理解生成器，不等同未来手动放置区域。

### 不稳定能力

- 从 `CustomBlank` 直接恢复官方模板。
- 只靠 live scene 扫描建立长期模板。
- 只靠 grouper 名称区分重复组。
- 用当前 `ObjectCatalog` 直接实例化 prefab/material。
- 外部 Unity 物品导入和多人同步。

### 必须补的数据

- 每个地图组合/变体的干净新图诊断快照，尤其 Roots/Jungle/Snow/Desert 的 variant 分支。
- 每个 grouper/step 的稳定层级路径，解决 Desert `Props/Rocks` 重名。
- 子区定义资料：每个 Segment 下可编辑的 Plateau/Wall/Left/Right/Entrance/山顶/山腰等区域如何映射到实际 XYZ、ray、layer 和约束。
- 可复用 prefab 的稳定 ID 与加载方式：场景克隆、Resources/Photon prefab、AssetBundle 还是外部包。
- 跨区段兼容矩阵：来源模板能否放到目标子区，是否需要缩放、材质替换、父子组、网络 ViewID 或禁止生成。
- 父子模板清单：哪些模板带子 `LevelGenStep`、哪些必须成组移动/删除/同步。
- 材质规则清单：哪些材质可替换颜色，哪些只可整材质替换，哪些禁止改。
- 多人同步协议：主机配置、生成位置、PhotonView ID、客机缺资源降级和中途加入。
- 性能基准：一次生成多少物体、多少 raycast/RPC 可接受，哪些路径需要批处理或分帧。

### 推荐路线

1. 先把诊断/内置模板做扎实：干净新图采样，输出稳定 path，建立 `OfficialTemplateSnapshot`。
2. 把 UI 概念拆开：Segment 选择、SubArea 子区、ObjectPalette 模板库、PlacementRule 参数面板。
3. 把 `ObjectCatalog` 升级为 `DaObjectRegistry`，至少能稳定找回游戏内 prefab 模板或报告缺失。
4. 先做游戏内 prefab 的自定义放置，不先做外部 AssetBundle。原因是当前 `ObjectCatalog` 已有游戏内模板样本，外部格式资料缺口更大。
5. 第一条规则只允许低风险模板，例如无 PhotonView/无子生成器的普通树、灌木、岩石；跨区段时使用目标子区落地参数。
6. 再接多人同步。多人时由主机生成 positions/rotations，并广播模板 ID、目标子区 ID 和必要 PhotonView ID。
7. 材质/颜色最后接入，优先 MaterialPropertyBlock/实例材质，避免 sharedMaterial 污染。

### 下一轮开发优先级

- P0：补内置模板快照/稳定 path 诊断，不再继续无限追加 per-map 清理 if。
- P0：实现 `SubArea` 数据模型和 UI 原型，哪怕先只读/手动输入 XYZ，也要把它和 Segment/ObjectCatalog 分开。
- P1：把 `ObjectCatalog` 抽成正式 `DaObjectRegistry`，能按稳定 ID 找回当前场景模板或报告缺失。
- P1：实现第一条自定义规则：在某个子区添加一个游戏内 prefab，配置数量/缩放/落地，生成后记录 ownership，支持清理。
- P1：实现第一条跨区段低风险例子：从 Jungle 棕榈模板放到 Desert 平台子区，验证目标 ray/layer 落地、数量限制、ownership 清理和诊断输出。
- P1：为父子模板和 `SingleItemSpawner` 先做只读风险标记，暂不允许直接添加高风险模板。
