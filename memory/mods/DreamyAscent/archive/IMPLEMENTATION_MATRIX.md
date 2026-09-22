# DreamyAscent Implementation Matrix

更新时间：2026-05-12

本文把后期地图编辑需求逐项拆成数据层、运行时层、UI 层、同步层、证据和资料缺口。它补充 `MAP_GENERATION.md` 与 `CROSS_SEGMENT_PLACEMENT.md`，重点解决“别的区段物品如何放到当前区段”“需求如何落地”“依据够不够明确”的问题。

## 多轮审阅口径

这次不再只按“读了几遍”记口号，而按不同问题切面记录可追溯结论：

1. 资源盘点：DreamyAscent 源码、当前 memory、最新诊断 JSON、PEAK 1.61.b 反编译、TerrainCustomiser、TerrainRandomiser、HazardSpam 源码和 NetGameState 参考代码。
2. 原版运行流：从 `MapHandler/MapSegment` 到 `PropGrouper.RunAll()`、`LevelGenStep.Execute()`、`PropSpawner.GetRandomPoint()`、`PropSpawner.Spawn()`。
3. 数据诊断流：对照 `RuntimeExport.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`、`CustomBlankRemaining_*.json`，确认哪些数据能保存，哪些只是运行时引用。
4. 故障反推流：用 Desert 空心、Roots/Jungle 乱石、`CustomBlank -> OfficialTemplate` 不可靠、漂浮物问题反推长期架构。
5. 跨区段放置流：把“来源模板”和“目标子区”拆开，逐项验证 prefab、目标 area/ray/layer、数量、父子依赖和网络。
6. UI/交互流：把 Segment、SubArea、ObjectPalette、PlacementRule 拆开，确认模板库是否独立窗口、如何右键/拖拽添加。
7. 多人同步流：主机生成 settings/seed/positions/rotations/PhotonView ID，客机只复现，不能各自随机。
8. 资源扩展流：外部 Unity 物品、材质/颜色替换、性能分帧和资源缺失回退。

## 需求到实现矩阵

### 1. 官方模板模式

用户目标：各区段参考原来游戏的官方模板，在原版生成器基础上调整数量、大小、范围、概率等参数。

实现路径：

- 继续使用 `Segment -> Grouper -> Step -> Properties/Modifiers/Constraints/PostConstraints`。
- `OfficialTemplate` 调用原版 `PropGrouper.RunAll(true)`，让原版自己执行 `ClearAll()`、steps 和 deferred steps。
- 只编辑可序列化标量参数，不把 prefab/material 引用硬塞进 `DaPropertyData.Value`。

依据：

- PEAK `PropGrouper.RunAll()` 先 `ClearAll()` 再执行 steps/deferred steps。
- PEAK `PropSpawner` 暴露 `area`、`nrOfSpawns`、`minSpawnCount`、`chanceToUseSpawner`、`layerType`、constraints、modifiers、postConstraints。
- TerrainCustomiser 的价值主要在这一层：标量属性编辑、UI 和生成刷新。

缺口：

- Desert 有重复 `Props/Rocks`，必须补稳定 path 或稳定 ID。
- Roots/Jungle 岩石组当前靠保护性跳过，长期要用干净模板快照和 step 级规则替代。

### 2. 空白自定义模式

用户目标：各区段默认为空，用户自己添加想要的物品来创造地图。

实现路径：

- `CustomBlank` 只负责清理官方普通生成物，不承诺绝对清空机制/地形对象。
- 新增 `DaSubAreaData`：保存子区中心 XYZ、形状/范围、ray、layer、法线/高度/材质约束。
- 新增 `DaPlacementRuleData`：保存模板 ID、目标子区 ID、数量、缩放、旋转、概率、落地策略、清理策略和网络策略。
- 新增 ownership root：DreamyAscent 生成物只挂到本 MOD 管理节点，清理时只删本 MOD 生成物。

依据：

- 当前 `CustomBlankRemaining` 显示 Roots 剩余多为 `Ground/Water/Wall/Splitmesh/Spores`，Caldera/Volcano 剩余为岩浆机制，不应当作普通装饰清理。
- 原版 `PropSpawner.GetRandomPoint()` 证明放置必须有目标 transform/area/ray/layer，不是只存物品名。

第一批测试例子：

- 在 `Desert_Segment / PlateauTop` 手动建一个 `SubArea`，只添加 3-10 个普通树/灌木模板，生成后检查漂浮、穿模、清理是否只影响本 MOD 生成物。

缺口：

- 每个 Segment 的默认 SubArea 还没有内置映射。
- 仍缺生成后验证诊断，例如每个物体的 raycast 命中、离地高度、是否出界、是否被 post constraint 拒绝。

### 3. 当前区段内自选物品

用户目标：在某个区段内自由选择该区段模板，例如在 Jungle 的平台添加灌木、棕榈、藤蔓，配置数量和大小。

实现路径：

- `ObjectPalette` 默认按当前 Segment 过滤。
- 用户先选择目标 `SubArea`，再从模板库右键“添加到当前子区”或拖拽到预览命中位置。
- 生成时使用目标子区的落地参数，模板只提供 prefab、默认 modifier、组件风险和默认建议值。

依据：

- `ObjectCatalog.json` 已能列出 item/material、来源 step、默认 `area/nrOfSpawns/layerType`、`hasChildGeneration`、`hasSingleItemSpawner`、`hasPhotonView`。
- 当前 UI 已有只读“区段模板库”，但它还不是可执行 `DaObjectRegistry`。

例子：

- Jungle 当前区段：从 `Jungle_Segment/Pops_Plat/Palms` 选择 `Jungle_PalmTree_Thick`，目标为 `Jungle_Segment/PlateauTop`，数量 5-15，随机 yaw，按目标子区 TerrainMap raycast 落地。

缺口：

- 需要把 `ObjectCatalog` 升级为 `DaObjectRegistry`，重进/换图后能重新找到 prefab 或明确报告缺失。

### 4. 跨区段物品放置

用户目标：别的区段的物品也能放到当前区段，例如雨林棕榈放到沙漠平台，沙漠仙人掌放到雨林平台。

核心模型：

- `TemplateSource`：来源 Segment/Grouper/Step/path、prefab 稳定 ID、默认数量/范围/材质、组件摘要和风险。
- `SubArea`：目标 Segment 下的山顶、山腰、洞口、平台、墙面等实际 XYZ 子区。
- `PlacementRule`：把一个来源模板放进一个目标子区的规则。

正确运行流：

1. 选择来源模板，例如 `item:jungle-segment:step-prop-prefab:dcb9dd57`。
2. 检查风险：无 `PhotonView`、无子 `LevelGenStep`、无 `SingleItemSpawner` 才进入低风险路径。
3. 选择目标子区，例如 `Desert_Segment / PlateauTop`。
4. 用目标子区或目标参考 spawner 的 transform/area/ray/layer 采样落点。
5. 用来源模板的 prefab 实例化。
6. 主机生成 positions/rotations，客机按结果复现。

依据：

- PEAK `PropSpawner.GetRandomPoint()` 用当前 spawner transform、`area`、raycast、`layerType` 算落点；`Spawn()` 才从 `props` 选择 prefab。
- HazardSpam `HazardTemplateManager.PropPrefabs` 保存来源模板，`PropSpawners[(Zone, SubZoneArea)]` 保存目标区域。
- HazardSpam `HazardManager.CreateSpawner()` 复制目标区域 spawner 的 `area/rayDirectionOffset/rayLength/raycastPosition/layerType`，但 `props` 使用来源模板 `propSpawnerPrefab.props[0]`。
- HazardSpam `SpawnerLogic.GenerateSpawnPoints()` 只生成 positions/rotations，`NetComm.SpawnHazardsNetwork()` 由主机 RPC 广播。

可执行例子 A：Jungle 棕榈放到 Desert 平台。

- 来源：`item:jungle-segment:step-prop-prefab:dcb9dd57`，`Jungle_Segment/Pops_Plat/Palms/props[0]`，`Jungle_PalmTree_Thick`。
- 诊断：`hasChildGeneration=false`、`hasPhotonView=false`、默认 `area=401.5,150`、`nrOfSpawns=50`、`layerType=TerrainMap`。
- 目标：`Desert_Segment / PlateauTop`，先手动建 SubArea；可参考 Desert `Props/Cactus` 的 `layerType=TerrainMap` 和 raycast 逻辑，但不要继承 Desert 原 `nrOfSpawns=1000`。
- 规则：数量 3-10，缩放 0.8-1.25，随机 yaw，对齐命中法线，生成后输出诊断。

可执行例子 B：Desert 普通仙人掌放到 Jungle 平台。

- 来源允许：`item:desert-segment:step-prop-prefab:cf9ea4a1`，`Cactus Ball Big`，`hasChildGeneration=false`，`hasPhotonView=false`。
- 来源禁止：`Cactus Ball Base`、`Tall Cactus`、`Short Cactus` 都有 `hasChildGeneration=true`，第一阶段只读。
- 目标：`Jungle_Segment / PlateauTop`，数量 5-20，不能继承 Desert `Cactus` 默认 `nrOfSpawns=1000`。

反例：不能把 `Jungle_Segment/Pops_Plat/Palms` 的整个 `PropSpawner` 拿到 Desert 运行。那会带来源 transform、来源 area、来源数量和来源 constraints，容易生成在错误位置或过量。

缺口：

- 目标 SubArea 默认坐标还缺。
- 需要跨区段兼容矩阵和第一版生成后诊断。

### 5. 父子/衍生关系

用户目标：有些物品有衍生关系，例如椰子长在椰子树上，树在地上，不能把它们平级随机撒。

实现路径：

- 建 `ParentChildPlacementGroup` 或依赖图：父物先落地，子物按父物本地坐标或父物内部 `LevelGenStep` 执行。
- 移动、缩放、删除、保存、导入、多人同步都按绑定组处理。
- 第一阶段所有 `hasChildGeneration=true` 的模板只读或实验开关，不进入普通添加。

依据：

- PEAK `PSM_ChildSpawners.ModifyObject()` 会在父物生成后执行父物 children 中的 `LevelGenStep`。
- PEAK `PSB_ChildSpawners.RunBehavior()` 也会对 spawned 列表里的每个父物执行子 `LevelGenStep`。
- Roots `Redwood` 诊断：`role=parent-child-template`、`hasChildGeneration=true`、`childLevelGenStepCount=4`、组件含 `PropGrouper`、`MultipleGroundPoints`、`SpawnConnectingBridge`。

例子：

- Roots `Redwood` 不能直接放到 Desert 平台。它可能生成树上平台/桥/附属物，且依赖多个地面点。
- `Mushroom tree Flat tall` 也有子生成器，不能当普通蘑菇模型散点生成。

缺口：

- 缺父子模板依赖图：哪些子 step 应执行，哪些要禁用，子物的 ownership 如何保存。

### 6. 外部 Unity 物品导入

用户目标：外部可以手动加 Unity 物品进来，放在地形上。

实现路径：

- 先确定资源格式：AssetBundle、Addressables、场景对象克隆，或仅游戏内 prefab 扫描。
- 外部物品进入 `DaObjectRegistry`，必须有资源 ID、来源路径、显示名、默认缩放、碰撞/落地策略、依赖和资源缺失回退。
- 外部物品和游戏内模板使用同一套 `PlacementRule`、SubArea、ownership 和网络策略。

依据：

- 当前 `RuntimeExport.json` 跳过 `UnityEngine.Object`、数组、集合，说明外部 prefab/material 不能靠普通属性 JSON 恢复。
- `ObjectReferenceMap.json` 能提供 renderer/material/path 思路，但还不是外部资源加载器。

例子：

- `AssetBundle: custom_bridge_01` 放到 `Jungle_Segment / WallEntrance`：先按 bundle key 注册，目标子区提供落地/朝向，资源缺失时跳过并写诊断。

缺口：

- 还没有外部资源格式和加载代码资料。第一版不应先做外部导入，应先做游戏内低风险 prefab。

### 7. 材质/颜色替换

用户目标：物品材质是否可以更换，例如颜色等。

实现路径：

- 新增 `DaMaterialOverrideData`：renderer path、材质槽位、颜色/材质 key、是否递归、回滚策略。
- UI 第一阶段优先做实例级颜色覆盖，避免全局污染。
- 多实例随机材质要保存 seed 或主机生成结果，保证客机一致。

依据：

- PEAK `PSM_SetMaterial` 直接写 `renderer.sharedMaterial`。
- PEAK `PSM_ReplaceMaterial` 读写 `renderer.sharedMaterials`。
- 这些说明原版有材质规则，但 DreamyAscent 运行时 UI 不能照搬 shared material 写法，否则同源对象会被全场改色。
- `ObjectCatalog` 已导出 renderer materials、shader、color、mainTexture，可作为候选清单。

例子：

- Jungle 棕榈叶子局部变色：只对生成出来的 `Jungle_PalmTree_Thin` 实例使用 MaterialPropertyBlock 或复制材质，保存 renderer path 和 slot；不改全局 `M_Foliage_Palmtree 5`。

缺口：

- 缺 renderer 稳定 path 规范、材质槽位选择 UI、贴图/shader 是否允许替换的边界。

### 8. UI 窗口和添加交互

用户目标：参数和模板库是否需要分窗口；能否通过拖动、右键等方式添加到区段内。

实现路径：

- 保留参数窗口用于当前 Step/当前 SubArea/当前 PlacementRule。
- 模板库拆成独立窗口或固定侧栏：搜索、来源 Segment 过滤、当前区段/全局切换、风险过滤。
- 目标子区在主视图/预览中选中或创建，模板库右键“添加到当前子区”，拖拽则落到鼠标命中的 SubArea 或手动坐标。

依据：

- 当前同窗只读模板库适合诊断阶段，但添加/搜索/拖拽后会挤占参数编辑空间。
- HazardSpam 用 Zone/SubZone 与 hazard 列表分层，证明先选区域再管理规则更清晰。

例子：

- 选中 `Desert_Segment / PlateauTop`，模板库切到“全局”，搜索 `Palm`，右键 `Jungle_PalmTree_Thick -> 添加到当前子区`，参数面板出现数量/缩放/旋转/材质。

缺口：

- 需要先实现 SubArea 可视化和选中状态，否则拖拽目标不明确。

### 9. 内置模板快照和诊断

用户目标：后期每个变体内置一份模板，避免每次读取实时地图不可靠。

实现路径：

- 在干净新图、每个地图组合/变体进入后导出 `RuntimeExport/ObjectCatalog/ObjectReferenceMap/CustomBlankRemaining`。
- 每个 snapshot 记录游戏版本、mapKey、segment、variant、grouper path、step path、源文件时间。
- 运行时用 snapshot 做默认参数和模板索引，再绑定当前场景实际对象或使用自定义 spawner。

依据：

- 用户已确认 live scene 读取不牢靠。
- Desert 空心和 Roots/Jungle 乱石都说明被清理/重跑污染后的场景不能作为唯一模板来源。

缺口：

- 缺完整干净样本，尤其 Roots/Jungle/Snow/Desert 的 variant 分支和重复 groupers。

### 10. 多人同步和中途加入

用户目标：自定义生成在主客机一致，不出现主机和客机各自扣/生成不同状态这类问题。

实现路径：

- 主机权威：主机决定模板 ID、SubArea、seed、positions、rotations、必要 PhotonView ID。
- 客机只复现：收到模板 ID 和 positions/rotations 后实例化，不自行随机。
- 中途加入：补发 active PlacementRule 和已生成结果。

依据：

- TerrainRandomiser 同步 room settings/seed，并处理 `PhotonView` ID 分发。
- HazardSpam `NetComm` 由主机 RPC 创建 spawner，再广播 positions/rotations。

例子：

- Jungle 棕榈放 Desert：主机生成 7 个落点并广播；客机只在这 7 个坐标实例化。若客机缺模板，写诊断并跳过，不自行换成别的树。

缺口：

- 缺 DreamyAscent 自己的 `DaNetworkSpawnData`、中途加入缓存、资源缺失协议和 PhotonView 模板白名单。

## 证据索引

- PEAK 反编译 `PropSpawner.cs`：`SpawnNew()` 决定数量和尝试次数，`TryToSpawn()` 调 `GetRandomPoint()`，`Spawn()` 使用 `props` 实例化，`GetRandomPoint()` 使用 transform、`area`、raycast、`layerType`。
- PEAK 反编译 `PropGrouper.cs`：`RunAll()` 先 `ClearAll()` 再执行 steps/deferred steps。
- PEAK 反编译 `PSM_ChildSpawners.cs`、`PSB_ChildSpawners.cs`：父物生成后执行子 `LevelGenStep`。
- PEAK 反编译 `PSM_SingleItemSpawner.cs`：单物品生成器壳需要绑定 `objToSpawn`。
- PEAK 反编译 `PSM_SetMaterial.cs`、`PSM_ReplaceMaterial.cs`：原版材质规则写 `sharedMaterial/sharedMaterials`。
- HazardSpam `HazardTemplateManager.cs`：`PropPrefabs` 与 `PropSpawners[(Zone, SubZoneArea)]` 分离。
- HazardSpam `HazardManager.cs`：`CreateSpawner()` 复制目标区域 spawner 参数，`props` 使用来源模板 prefab。
- HazardSpam `SpawnerLogic.cs`：反射调用 `GetRandomPoint()` 生成 positions/rotations。
- HazardSpam `NetComm.cs`：主机 RPC 创建 spawner 并广播 positions/rotations。
- DreamyAscent 最新诊断：`ObjectCatalog.json` 已列出 `hasChildGeneration`、`hasSingleItemSpawner`、`hasPhotonView`、components、renderer materials、source 和 defaults。

