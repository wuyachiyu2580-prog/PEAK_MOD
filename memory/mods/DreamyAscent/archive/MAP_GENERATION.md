# DreamyAscent Map Generation

更新时间：2026-05-13

本文件是 DreamyAscent 地图生成长期记忆。详细多轮阅读过程和原始依据见 `MAP_GENERATION_RESEARCH_NOTES.md`；需求到实现矩阵见 `IMPLEMENTATION_MATRIX.md`；跨区段物品放置的例子和流程见 `CROSS_SEGMENT_PLACEMENT.md`。

## 资源可信度

- 最高可信：PEAK 1.61.b 反编译地图生成类、DreamyAscent 当前源码、最新 `DreamyAscent Diagnostics` JSON。
- 当前样本基线：`MOD开发\DreamyAscent\data\map-data`，其中 `1.62.a/` 是官方自然跑图样本，`TerrainRandomiser/` 是强制切变体后的验证样本；集中审计见 `SAMPLE_AUDIT_2026-05-13.md`，机器可读摘要见 `sample-index.json`。
- 当前生成产物：`MOD开发\DreamyAscent\data\map-data\generated`，由 `data/tools/build_map_data_artifacts.py` 生成，包含模板快照、对象注册表输入和样本回归报告。
- 可参考：TerrainCustomiser 反编译代码、TerrainRandomiser 反编译代码、HazardSpam GitHub 源码和反编译附带 NetGameState。
- 低可信：旧日期诊断、被 CustomBlank/重复生成污染过的 live scene、旧 ObjectCatalog 乱码样本。

## 多轮阅读结论

- 第 1 轮按资源盘点确认：DreamyAscent 当前仍是 `Segment -> Grouper -> Step -> 标量属性/约束` 模型；对象库是诊断/只读索引，不是可执行资源注册表。
- 第 2 轮按原版生成链路确认：真正决定放置位置的是生成器 transform、`area`、raycast、`layerType`、constraints、modifiers、postConstraints；prefab 只在 `Spawn()` 阶段作为 `props` 被实例化。
- 第 3 轮按故障反推确认：Desert 空心、Roots/Jungle 乱石、CustomBlank 后无法恢复官方模板，都指向同一个问题：不能长期依赖被运行时清理/重跑污染的 live scene 作为唯一模板来源。
- 第 4 轮按参考 MOD 确认：TerrainCustomiser 只证明标量参数编辑可行；TerrainRandomiser 证明主机同步设置/seed/ViewID 的必要性；HazardSpam 证明“模板来源”和“目标区域”可以拆开。
- 第 5 轮按跨区段放置确认：别的区段物品可以放到当前区段，但必须用来源模板 + 目标子区落地参数组合，不能直接运行来源区段的原 step。
- 第 6 轮按 UI/数据模型确认：后期必须拆成 Segment、SubArea、ObjectPalette、PlacementRule 四层；模板库要独立窗口或固定侧栏，参数面板只编辑当前 step/子区/规则。
- 第 7 轮按需求逐项验算确认：官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照、多人同步是十个不同工作面，不能压成一个 `RunSegment` 特判。
- 第 8 轮按测试用例倒推确认：第一条可执行验证应是低风险游戏内 prefab 放置，再做跨区段；高风险 Photon/父子/SingleItem/岩浆机制必须先只读标记。

## 原版生成链路

- 原版链路：`MapHandler -> MapSegment/variant -> PropGrouper -> LevelGenStep -> PropSpawner/DecorSpawner/PropDeleter/... -> constraints/modifiers/postConstraints`。
- `PropGrouper.RunAll()` 会执行 `ClearAll()` 再跑 steps/deferred steps；官方模板模式应尽量让原版自己清理，不要提前手动 Destroy 其层级。
- `PropSpawner` 的核心生成参数是 `area`、raycast、`layerType`、数量、概率、constraints、modifiers、postConstraints。
- `PropSpawner.GetRandomPoint()` 用当前 spawner 的 transform、`area` 和 raycast 找落点；`Spawn()` 再从 `props` 里选 prefab。因此“来源模板”和“目标区域”在技术上可以拆开。
- `DecorSpawner` 依赖 `spawnPoints: Transform[]`。
- `DesertRockSpawner` 依赖 `Enterences` / `Inside` 子节点。
- `PSM_ChildSpawners` / `PSB_ChildSpawners` 说明父子依附应按“先父后子”执行。
- `PSM_SingleItemSpawner` 说明单物品生成器壳需要绑定具体 `objToSpawn` prefab。
- 原版材质 modifier 多用 `sharedMaterial/sharedMaterials`，只能作为规则参考，不能直接作为 DreamyAscent UI 改色实现。

## 当前能力

- 可扫描当前地图并导出 `RuntimeExport.json`、`NameMap.json`、`ObjectReferenceMap.json`、`ObjectCatalog.json`。
- 当前样本集已覆盖 `Beach / Jungle / Roots / Snow / Desert` 的全部已知变体；可以开始基于样本构建模板快照、变体回归对照和对象注册表输入。
- 已有离线生成产物：130 个 Segment snapshot、193 个模板候选、25 个材质候选；样本回归报告 `status=pass`、issue 0、warning 0。源码侧已接入 `DaObjectRegistry` 只读加载和 UI 摘要，并新增 `SubArea` / `PlacementRule` 配置保存层；当前仍没有实际自定义生成/实例化。
- `PlacementRule` 配置层当前可保存目标子区 ID、模板 registry ID、数量、最小/最大缩放、放置模式、旋转模式、ownership 和本地偏移；Catalog 页可从首批推荐候选添加规则。
- 可编辑原版 step/constraint 的标量参数，例如数量、范围、概率、缩放等。
- 可运行官方 grouper/segment，但 Roots/Jungle 岩石组有保护性跳过。
- 可做第一版 `CustomBlank` 清理，语义是清理官方普通生成物，不是清空结构/机制对象。
- 可只读展示区段模板库 item/material、来源 step、默认参数、父子/Photon 风险标记。

## 当前边界

- `RuntimeExport.json` 不保存 `UnityEngine.Object`、数组和集合；因此无法只靠它恢复 prefab/material/spawnPoints。
- `ObjectCatalog.json` 目前是运行时索引；离线 `object-registry-input.json` 已接入 `DaObjectRegistry` 只读骨架，但还不是可执行资源定位/生成服务。
- `CustomBlank -> OfficialTemplate` 不保证恢复。清理可能破坏原版生成器依赖的层级和引用。
- 只靠 grouper 名称不够。Desert 有重复 `Props/Rocks`，后续必须使用层级 path 或稳定 ID。
- live scene 扫描会受当前地图状态影响。后期每个地图组合/变体应内置一份干净模板快照。
- 跨区段放置还没有可执行数据层。当前只能诊断出模板来源和风险，不能稳定地在重进/客机/换图后找到同一个 prefab 并复现生成。

## 诊断事实

- 2026-05-13 样本审计：官方自然样本 `DreamyAscent Files` 有 22 个 JSON、19 个完整诊断目录；`TerrainRandomiser` 目录有 29 个 JSON、7 个完整诊断目录。所有诊断目录均包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`。
- 2026-05-13 批量脚本复核：26 个诊断目录完整，`RuntimeExport.json` 解析失败 0 个，`0 grouper` segment 0 个，未知 variant 0 个；结果已写入 `data/map-data/sample-index.json`。
- 2026-05-13 生成产物：`template-snapshots.json` 覆盖 130 个 segment、520 个 grouper、4001 个 step；`object-registry-input.json` 合并 193 个模板候选、25 个材质候选，其中 79 个技术低风险候选、20 个第一批推荐测试候选；`sample-regression-report.json` 当前 `pass`。
- 官方自然样本已覆盖 `Beach Default/SnakeBeach/RedBeach/BlueBeach/BlackSand`、`Jungle Default/Lava/Pillars/Bombs/Ivy`、`Roots Default/Deep Woods`、`Snow Default/Lava/Spiky/GeyserHell`、`Desert NoVariant/CactusForest/DynamiteHell/TornadoHell`。
- TerrainRandomiser 验证样本补齐 `Beach JellyHell`、`Jungle Thorny/SkyJungle`、`Roots Cave Mania/Deep Water/Bomb Beetle/Clearcut`、`Desert ScorpionsHell/CacusHell/TumblerHell`。
- 代表性 Roots/Desert 官方组合有 5 segments、20 groupers、165 steps、161 catalog items、19 catalog materials、1160 object references。代表性 Jungle/Snow 官方组合有 5 segments、20 groupers、144 steps、126 catalog items、13 catalog materials、871 object references。
- Roots/Desert 组合中 Roots 默认组为 `PlateauProps`、`PlateauRocks`、`Redwood`、`WallProps`、`WallRocks`、`Waterfalls`。
- Jungle 组为 `Pops_Plat`、`Props_Wall`、`Rocks_Plat`、`Rocks_Wall`。
- Desert 组含重复 `Props/Rocks`，必须用 path 区分。
- `ObjectCatalog` 最新 Roots/Desert 样本：161 items、19 materials，含 `parent-child-template` 和 `single-item-prefab`。
- 纯净性判断不能只看 step 名。Desert 当前激活层级内常出现 step `ScorpionsHell`、`TumblerHell`，Jungle/SkyJungle 当前激活层级内也可出现少量 `Lava/Pillars/Ivy` step 名；应以 active variant root、完整 `hierarchyPath` 和是否有未激活 variant root 整支泄漏作为判断依据。
- `CustomBlankRemaining`：Desert 最新只剩 `GroundMesh`；Caldera/Volcano 主要剩机制对象；Roots 剩余主要是地形、水体、墙体 splitmesh、粒子，不应默认当普通装饰清理。
- 低风险跨区段模板样例：Jungle `Pops_Plat/Palms` 下的 `Jungle_PalmTree_Thick/Crook/Thin` 是 `step-prop-prefab`，无子生成器、无 PhotonView；可作为以后“雨林棕榈放到沙漠平台”的第一批候选。
- 中/高风险跨区段模板样例：Desert `Cactus Ball Base`、`Tall Cactus`、Roots `Redwood` 带 `hasChildGeneration=true`；Jungle/Roots 行李、藤蔓、虫类带 `hasPhotonView=true`；第一阶段只读标记，不直接允许生成。

## 需求拆分

后期需求不能只理解为“模板库混合”。稳定设计至少分十个工作面，详细矩阵见 `IMPLEMENTATION_MATRIX.md`。

| 需求面 | 第一版实现 | 关键依据 | 第一批例子 |
| --- | --- | --- | --- |
| 官方模板模式 | 继续编辑原版 Step 标量并调用 `PropGrouper.RunAll(true)` | `PropGrouper.RunAll()` 自带清理和 deferred step | 新图复测 Roots/Jungle 官方生成 |
| 空白自定义模式 | `CustomBlank` + `SubArea` + `PlacementRule` | 空白区段没有原版 Step 可承载新增物品 | Desert 平台添加少量普通 prefab |
| 当前区段自选物品 | 当前 Segment 过滤的 `ObjectPalette` | `ObjectCatalog` 已有来源 step 与默认参数 | Jungle 平台添加 Jungle 棕榈 |
| 跨区段放置 | 来源模板 + 目标子区 | `GetRandomPoint()` 和 HazardSpam 均支持拆分 | Jungle 棕榈放 Desert 平台 |
| 父子依赖 | `ParentChildPlacementGroup` | `PSM_ChildSpawners` / `PSB_ChildSpawners` | Redwood 先禁止，后续整组支持 |
| 外部 Unity 物品 | 外部资源进入 `DaObjectRegistry` | JSON 无法保存 `UnityEngine.Object` | AssetBundle 物体注册后再放置 |
| 材质/颜色 | 实例级覆盖或 MaterialPropertyBlock | 原版 modifier 写 `sharedMaterial` 有污染风险 | 只给生成出的棕榈改叶色 |
| UI 拆窗 | 模板库独立窗口/侧栏，参数面板编辑规则 | 当前同窗只适合只读诊断 | 右键添加到当前子区 |
| 内置模板快照 | 干净新图 snapshot + 稳定 path | live scene 会被清理/重跑污染 | 每个 mapKey/variant 采样 |
| 多人同步 | 主机生成 positions/rotations/ViewID，客机复现 | TerrainRandomiser/HazardSpam | 7 棵棕榈同坐标同步 |

## 分层实现路线

### 官方模板模式

保留原版生成器，只编辑参数后调用原版生成。

适合近期继续做：

- 参数编辑、单组/整段生成、导入导出标量参数。
- 在干净新图或未执行 CustomBlank 的前提下复测官方模板。

必须遵守：

- 不在 `RunAll()` 前手动 Destroy 原版生成器层级。
- Roots/Jungle 岩石组短期继续跳过，长期改成模板快照 + step 级白名单。
- 生成前可以重绑运行时引用，但不能把重绑当完整恢复。

### 空白自定义模式

区段默认不继承官方普通物品，由用户自己添加规则。

必须新增：

- `SubArea` 子区数据：中心 XYZ、形状/范围、ray、layer、法线/高度/材质约束。
- `PlacementRule`：模板 ID、数量、缩放、旋转、概率、落地策略、清理策略。
- ownership：区分 DreamyAscent 生成物、原版结构、原版机制、外部资源。

必须避免：

- 不把 Segment、区段模板库、`PropSpawner.area` 三者混成一个概念。
- 不继续靠路径黑名单无限清理。

### 模板库/外部资源/材质/多人同步

目标是全局模板注册表 + 当前 Segment 默认过滤 + 目标子区兼容规则。

必须新增：

- `DaObjectRegistry`：稳定模板 ID、来源 path、加载方式、组件摘要、PhotonView 风险、默认参数。
- `DaMaterialOverrideData`：renderer path、材质槽、颜色/材质 key、递归范围、回滚信息。
- `DaNetworkSpawnData`：主机生成 positions/rotations、模板 ID、子区 ID、PhotonView ID、资源缺失降级。

参考依据：

- HazardSpam 把 `PropPrefabs` 和 `(Zone, SubZoneArea)` 的 `PropSpawners` 分离，并由主机广播 positions/rotations。
- TerrainRandomiser 通过 room property 同步 map settings/seed，并分发 PhotonView ID。

### 跨区段物品放置

跨区段放置不是“把别的区段的 grouper/step 拖过来跑”，而是“来源模板 + 目标子区”的组合。

必须新增：

- `TemplateSource`：来源 map/segment/grouper/step/path、prefab 稳定 ID、默认数量/缩放/材质、组件摘要和风险标记。
- `SubArea`：目标 Segment 下的山顶、山腰、洞口、平台、墙面等，保存中心 XYZ、范围/形状、ray、layer、法线/高度/材质约束。
- `PlacementRule`：模板 ID、目标子区 ID、数量、随机缩放/旋转、生成概率、落地策略、材质覆盖、清理策略和网络策略。

例子：

- 雨林棕榈放到沙漠平台：来源用 Jungle `Jungle_PalmTree_*` prefab，目标用 Desert 平台子区的 ray/layer/范围。数量从 3-10 试起，不继承原 `nrOfSpawns=50`。
- 沙漠普通仙人掌放到雨林平台：无子生成器的版本可低风险试验；带 `hasChildGeneration=true` 的仙人掌先禁用，等父子依赖建模。
- Roots Redwood 放到别的区段：高风险。它带 `PropGrouper`、`MultipleGroundPoints`、连接桥逻辑，必须按父子组整体生成和同步。
- 行李、藤蔓、虫类：带 PhotonView 或 AI/物理组件，必须等主机 ViewID/RPC 同步方案完成后再开放。

详细流程和风险表见 `CROSS_SEGMENT_PLACEMENT.md`。

### 父子/绑定物关系

绑定物不能当普通散点。椰子树与椰子、Redwood 与树上平台/桥、Mushroom tree 与子生成物都需要按依赖组处理。

必须新增：

- `ParentChildPlacementGroup` 或同等依赖图：父物先按目标子区落地，子物按父物本地坐标或父物内 `LevelGenStep` 执行。
- 组级 ownership：移动、缩放、删除、导入导出、多人同步都以组为单位。
- 风险闸门：`hasChildGeneration=true` 的模板第一阶段只读，不能进入普通添加按钮。

依据：

- `PSM_ChildSpawners` / `PSB_ChildSpawners` 会在父物生成后执行子 `LevelGenStep`。
- Roots `Redwood` 诊断显示有 `PropGrouper`、`MultipleGroundPoints`、`SpawnConnectingBridge` 和 4 个子 step。

### 外部 Unity 物品和材质

外部资源与材质不走当前 `DaPropertyData.Value` 路径。

必须新增：

- 外部资源格式决策：AssetBundle、Addressables、场景克隆，或先只允许游戏内 prefab。
- 外部资源 registry：资源 ID、加载路径、默认缩放、碰撞/落地策略、依赖、资源缺失回退。
- 材质覆盖数据：renderer path、slot、颜色/材质 key、递归范围、回滚策略。

依据：

- `RuntimeExport.json` 跳过 `UnityEngine.Object`、数组和集合，不能恢复 prefab/material 引用。
- 原版 `PSM_SetMaterial*` / `PSM_ReplaceMaterial` 写 shared material，只能参考规则，不能直接作为运行时 UI 实现。

## 必须补的数据

- 每个地图组合/变体的干净新图诊断快照已具备第一批全覆盖样本；`template-snapshots.json` 已提取第一版离线快照，下一步是接入源码侧查询和稳定 ID 匹配。
- 每个 grouper/step 的稳定层级 path 已进入离线快照；下一步是把这些 path/ID 用到运行时模板匹配和回归对比。
- 子区定义和默认映射：Plateau、Wall、WallLeft/Right、Entrance、山顶、山腰等。
- 游戏内 prefab 的稳定 ID 和可加载方式。
- 跨区段模板兼容矩阵：哪些来源模板能落到哪些目标 SubArea，哪些要缩放/材质替换，哪些必须禁用。
- 外部 Unity 物品格式：AssetBundle、Addressables、场景对象克隆或仅游戏内 prefab。
- 父子模板清单和 `SingleItemSpawner` 风险清单。
- 材质替换边界：仅颜色、整材质、贴图/shader、是否允许随机材质。
- 多人同步协议和中途加入策略。
- 性能基准和分帧/批处理策略。

## 推荐路线

1. 先进游戏验证 `SubArea` / `PlacementRule` 配置保存链路：添加规则、导出 JSON、重新导入、确认 UI 回显和 `placementConfigs` 日志。
2. 用 `generated/template-snapshots.json` 做稳定 path/ID 匹配，停止把 per-map skip 当长期方案。
3. 补强 `SubArea` 编辑：中心 XYZ、形状/范围、ray/layer、落地约束和兼容模板。
4. 拆 UI 概念：Segment、SubArea、ObjectPalette、PlacementRule 参数面板。
5. 先支持游戏内 prefab 的单机/主机端自定义放置，再做外部资源。
6. 第一条可执行自定义规则只做低风险普通 prefab：数量、缩放、落地、ownership、清理。
7. 再开放跨区段模板选择：先限低风险模板，目标子区必须提供落地参数，生成后写诊断。
8. 父子模板、`SingleItemSpawner`、PhotonView prefab 先只读标记风险，确认同步方案后再允许生成。
9. 材质/颜色最后接入，优先 MaterialPropertyBlock 或实例材质副本。

## 后续测试重点

- 测官方模板时用新图，不从 CustomBlank 切回官方后判断恢复质量。
- Roots/Jungle 重点看官方整段生成是否仍出现乱石，日志是否显示跳过危险岩石组。
- CustomBlank 重点看普通装饰是否清掉，结构/机制对象是否合理保留。
- 若测试自定义放置，必须记录目标子区、模板 ID、生成数量、漂浮/穿模情况、多人状态。
