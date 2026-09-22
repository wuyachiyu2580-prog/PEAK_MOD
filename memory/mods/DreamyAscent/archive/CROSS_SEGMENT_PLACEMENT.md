# DreamyAscent Cross-Segment Placement

更新时间：2026-05-12

本文专门记录“把别的区段的物品放到当前区段”的实现依据、数据模型、例子、风险和资料缺口。它补充 `MAP_GENERATION.md`，避免后续只停留在“模板库可混合”的概念层。

## 核心结论

- 可以实现跨区段物品放置，但不能把“来源区段模板”直接当“目标区段生成器”运行。正确模型是：来源模板提供 prefab、默认参数、组件和风险；目标子区提供 XYZ、范围、ray、layer、落地面、约束和 ownership。
- HazardSpam 是最直接参考：`HazardTemplateManager.PropPrefabs` 保存模板来源，`PropSpawners[(Zone, SubZoneArea)]` 保存目标区域，`HazardManager.CreateSpawner` 把目标区域 spawner 的 `area/ray/layer` 与来源模板 prefab 组合，再由主机广播 positions/rotations。
- 原版 `PropSpawner` 支持这个拆分：`GetRandomPoint()` 从 spawner transform、`area`、raycast、`layerType` 找落点；`Spawn()` 才从 `props` 选择 prefab 并执行 modifiers/postConstraints。
- DreamyAscent 当前 `ObjectCatalog` 已有来源模板信息，但还不是可执行 `DaObjectRegistry`：缺运行时加载方式、稳定 path 绑定、目标子区、ownership、网络数据和资源缺失处理。
- 最新诊断显示同一个 prefab 名称可能在多个 Segment 中出现，例如 `Jungle_PalmTree_*` 同时出现在 Beach 与 Jungle 的 catalog 条目里。因此后续模板 ID 必须包含来源 Segment/Grouper/Step/path，不能只用 prefab 名称。

## 必须拆开的四个概念

- `Segment`：游戏关卡大段，例如 `Jungle_Segment`、`Desert_Segment`、`Roots Segment`。
- `TemplateSource`：物品模板来源，来自某个 segment/grouper/step/path，例如 `Jungle_Segment/Pops_Plat/Palms/Jungle_PalmTree_Thick`。
- `SubArea`：目标放置子区，玩家要编辑的山顶、山腰、洞口、平台、墙面等。它保存中心 XYZ、范围/形状、ray 方向、layer、法线/高度/材质约束。
- `PlacementRule`：把一个模板放进一个目标子区的规则，保存模板 ID、数量、缩放、随机旋转、落地策略、材质覆盖、父子依赖、网络和清理策略。

不要再把 `PropSpawner.area` 直接叫“区域”。它只是一个生成器的范围参数，不等于玩家要编辑的山顶/山腰/洞口。

## 跨区段放置流程

1. 从 `DaObjectRegistry` 选择来源模板，例如 `item:jungle-segment:step-prop-prefab:dcb9dd57`。
2. 检查模板风险：`hasChildGeneration`、`hasSingleItemSpawner`、`hasPhotonView`、组件清单、renderer/material、默认 modifiers/constraints。
3. 选择目标 Segment 下的 `SubArea`，例如 `Desert_Segment / PlateauTop`。
4. 由目标 `SubArea` 或目标参考 `PropSpawner` 提供 `area`、transform、raycast、`layerType`、ray 长度和约束。
5. 创建 DreamyAscent 自己的临时/持久 spawner 或手动采样器：复制目标区域的落点参数，使用来源模板的 prefab；不要直接运行来源区段原 step。
6. 主机生成 positions/rotations，执行落地和 post validation，写入 `DaNetworkSpawnData`。
7. 所有客户端按模板 ID + positions/rotations 实例化；带 PhotonView 的模板必须由主机分配/广播 ViewID，或先标记为禁止自定义生成。
8. 生成物挂到 DreamyAscent ownership root 下，后续清理只删本 MOD 生成物，不误删原版结构。

## 正确实现与错误实现

错误实现：

- 把来源区段的 `PropGrouper` 或 `LevelGenStep` 直接拖到目标 Segment 跑。
- 复制来源 step 的 transform、`area`、`nrOfSpawns` 和所有 constraints 到目标区段。
- 用 prefab 名称当唯一 ID，例如只存 `Jungle_PalmTree_Thick`。
- 客机自己按本地随机重新生成。

会出现的问题：

- 物品落到来源区段坐标、半空或错误墙面。
- 小目标子区继承来源数量后过量，例如 Jungle 树 50、Desert 仙人掌 1000。
- 父子模板继续执行子生成器，导致桥、平台、附属物错位。
- 多人位置不同步，带 PhotonView 的对象 ID 不一致。

正确实现：

- 来源只提供 `TemplateSource`：prefab、默认建议值、材质、组件摘要、风险。
- 目标只提供 `SubArea`：中心 XYZ、范围/形状、ray、layer、落地面、约束。
- `PlacementRule` 明确数量、缩放、旋转、材质覆盖、网络和清理。
- 主机生成 positions/rotations 并广播，客机只复现。

## PlacementRule 例子

下面是第一条建议实现的跨区段规则。字段名可以后续按代码风格调整，但语义不能丢。

```json
{
  "id": "rule:desert-plateau:jungle-palm-small-test",
  "templateId": "item:jungle-segment:step-prop-prefab:dcb9dd57",
  "templateName": "Jungle_PalmTree_Thick",
  "templateSource": {
    "segment": "Jungle_Segment",
    "grouper": "Pops_Plat",
    "step": "Palms",
    "field": "props[0]"
  },
  "targetSubAreaId": "subarea:desert-segment:plateau-top:manual-001",
  "targetSegment": "Desert_Segment",
  "targetSubAreaName": "PlateauTop",
  "count": {
    "min": 3,
    "max": 10
  },
  "scale": {
    "min": 0.8,
    "max": 1.25
  },
  "rotation": {
    "randomYaw": true,
    "alignToSurfaceNormal": true
  },
  "placement": {
    "useTargetSubAreaRay": true,
    "layerType": "TerrainMap",
    "raycastPosition": true,
    "rejectIfNoHit": true,
    "maxAllowedFloatHeight": 0.25
  },
  "risk": {
    "hasChildGeneration": false,
    "hasSingleItemSpawner": false,
    "hasPhotonView": false,
    "allowedStage": "low-risk-first-pass"
  },
  "ownership": "DreamyAscent",
  "network": {
    "authority": "MasterClient",
    "sync": "positions-rotations"
  }
}
```

生成后诊断必须至少输出：

- `templateId`、`targetSubAreaId`、实际生成数量、失败 raycast 数、被约束拒绝数量。
- 每个实例的 position、rotation、scale、命中法线、命中 layer、离地高度。
- 是否出现资源缺失、模板风险变化、客机复现失败。

## 实例 1：雨林棕榈放到沙漠平台

来源模板：

- `Jungle_Segment / Pops_Plat / Palms`
- prefab：`Jungle_PalmTree_Thick`、`Jungle_PalmTree_Crook`、`Jungle_PalmTree_Thin`
- 诊断依据：`ObjectCatalog.json` 中这些条目是 `step-prop-prefab`，`hasChildGeneration=false`，`hasPhotonView=false`，默认 `nrOfSpawns=50`，`area=401.5,150`。

目标子区：

- `Desert_Segment / PlateauTop`，后续由用户手动框选或从干净模板快照映射。
- 可临时参考 Desert `Props/Cactus` 的目标落地参数：默认 `area=300,500`、地形 raycast、TerrainMap layer。

规则建议：

- 允许作为第一批低风险跨区段模板，因为无 PhotonView、无子生成器。
- 不复制雨林 `Pops_Plat/Palms` 的原始世界 transform；只复制 prefab 和安全 modifiers。
- 使用沙漠目标子区的 ray/layer/高度约束落地，避免把棕榈生成到雨林原坐标或半空。
- 数量建议先从 3-10 棵开始，不能继承原 `nrOfSpawns=50` 直接铺满。
- 注意最新诊断里 Beach 也有 `Jungle_PalmTree_*` 条目，Beach 默认 `nrOfSpawns=100`、Jungle 默认 `nrOfSpawns=50`。如果用户明确要“雨林棕榈”，模板来源应选 `item:jungle-segment:*`，不要因同名 prefab 误选 Beach 条目。

## 实例 2：沙漠仙人掌放到雨林平台

来源模板：

- `Desert_Segment / Props / Cactus`
- prefab：`Cactus Ball Big`、`Cactus Ball Base`
- `Cactus Ball Base` 在诊断中 `hasChildGeneration=true`，说明可能带子 `LevelGenStep`，不是最低风险模板。

目标子区：

- `Jungle_Segment / Plateau`，可参考 `Pops_Plat/Bushes` 或 `Pops_Plat/Palms` 的目标区域参数。

规则建议：

- `Cactus Ball Big` 这类无子生成器版本可先允许低风险放置。
- `Cactus Ball Base`、`Tall Cactus`、`Short Cactus` 这类 `hasChildGeneration=true` 的模板先只读标记，不允许直接添加，直到父子依赖建模完成。
- 如果未来允许，应按父子组整体生成、移动、删除，不能只把子物当普通 prefab 扔到雨林地面。
- Desert `Cactus` 默认数量很大：`Cactus Ball Big` 来源 step 默认 `nrOfSpawns=1000`，`Cactus_Big` 默认 `nrOfSpawns=100`。跨区段时只能把这些当来源建议，不可直接复制到目标子区。

## 实例 3：Roots Redwood 放到别的区段

来源模板：

- `Roots Segment / PlateauProps / Mush Trees` 中的 `Redwood`
- 诊断依据：`role=parent-child-template`，`hasChildGeneration=true`，组件含 `PropGrouper`、`MultipleGroundPoints`、`SpawnConnectingBridge`。

风险：

- 它会继续执行子 `LevelGenStep`，可能生成平台、桥、树上附属物。
- 它依赖多个地面点和连接桥逻辑，放到 Desert/Jungle 后很容易漂浮、穿模或桥连接错误。

规则建议：

- 第一阶段禁止直接跨区段生成。
- 后续若支持，必须建立 `ParentChildPlacementGroup`：父树先落地，子 step 以父树本地坐标执行，所有子物一起 ownership、保存、删除、同步。
- 需要生成后验证：父树接地点、桥连接目标、子生成物高度、与目标子区边界的关系。

## 实例 4：行李、藤蔓、虫类等 Photon 高风险模板

来源模板示例：

- Jungle `LuggageAncient/LuggageEpic/LuggageBig/LuggageSmall`：`hasPhotonView=true`。
- Jungle `JungleVine`：`hasPhotonView=true`。
- Roots `SpiderDropper`、`Beetle`：带 `PhotonView`，还可能带 AI、Rigidbody、PhysicsSyncer。

规则建议：

- 第一阶段只展示，不允许自定义放置。
- 第二阶段如果支持，必须参考 TerrainRandomiser 的 `PhotonView` ID 分发和 HazardSpam 的主机 RPC。主机分配 ViewID 后同步给客机，不能让每台机器各自 Instantiate。
- AI/物理类还要确认是否需要额外初始化、触发器、禁用条件和场景管理器注册。

## 实例 5：火山/火山口物件放到其他区段

来源模板示例：

- `Volcano_Segment / Edges|Middle` 下的岩浆、桥、岩石类物件。
- HazardSpam 中有把 Tropics/Alpine 的 `LavaRiver` 当 HazardType 的参考，但它仍是复制目标区域 spawner 后生成。

规则建议：

- 普通静态岩石可作为中风险模板，但要检查材质、碰撞和落地面。
- 岩浆机制、上升岩浆、带伤害/网络/全局状态的对象默认禁止跨区段放置。
- Caldera/Volcano 的机制对象在 `CustomBlank` 中默认保留，不能被普通清理规则误删。

## 兼容性规则

- 低风险：无 `PhotonView`、无 `LevelGenStep` 子生成、无 `SingleItemSpawner`、无 Rigidbody/AI、renderer/material 普通、能通过目标子区 raycast 落地。
- 中风险：带材质替换、复杂 collider、需要特殊法线、带多个 renderer、体积很大、来源 step 有 postConstraints。
- 高风险：`hasPhotonView=true`、`hasChildGeneration=true`、`hasSingleItemSpawner=true`、AI/物理/触发器/伤害/全局机制对象。
- 禁止第一阶段生成：Roots Redwood/Forest Cave/Mushroom tree 等父子模板，行李、藤蔓、虫类、岩浆机制、Dynamite/SingleItemSpawner 物品。

## 兼容矩阵初稿

| 来源模板 | 目标子区 | 第一阶段结论 | 依据 |
| --- | --- | --- | --- |
| Jungle `Jungle_PalmTree_Thick/Crook/Thin` | Desert `PlateauTop` / 山腰 | 允许低风险试验 | `hasChildGeneration=false`、`hasPhotonView=false`；使用目标子区 ray/layer；数量 3-10 |
| Jungle `Jungle_PalmTree_*` | Volcano/Caldera 机制区 | 暂缓 | 普通树可生成，但机制区地形/伤害/岩浆对象复杂，先不做第一例 |
| Jungle `JungleVine` | 任意跨区段 | 禁止第一阶段 | `hasPhotonView=true`，组件含 `JungleVine`、`BerryVine` |
| Jungle/Beach/Desert `Luggage*` | 任意跨区段 | 禁止第一阶段 | `hasPhotonView=true`，组件含 `Luggage/Item/Animator/SpineCheck` 等网络或交互逻辑 |
| Desert `Cactus Ball Big` | Jungle `PlateauTop` | 允许低风险试验 | `hasChildGeneration=false`、`hasPhotonView=false`，但数量必须重设，不能继承 1000 |
| Desert `Cactus Ball Base` | Jungle/Beach/Roots 平台 | 禁止第一阶段 | `hasChildGeneration=true`，有子 step |
| Desert `Tall Cactus` / `Short Cactus` | Jungle/Beach/Roots 平台 | 禁止第一阶段 | `hasChildGeneration=true`，还可能带 `SpineCheck/BerryBush` |
| Desert `Dynamite` | 任意跨区段 | 禁止第一阶段 | `role=single-item-prefab`、`hasPhotonView=true`，组件含 `ItemPhysicsSyncer`、`Dynamite`、`Rigidbody` |
| Roots `Redwood Trunk` | Desert/Jungle 平台 | 可作为中风险候选 | 无子生成、无 PhotonView，但有 `MultipleGroundPoints`，需多个接地点验证 |
| Roots `Redwood` | 任意跨区段 | 禁止第一阶段 | `parent-child-template`、`hasChildGeneration=true`、子 step 4、连接桥和多接地点 |
| Roots `Mushroom tree Flat tall` | 任意跨区段 | 禁止第一阶段 | `hasChildGeneration=true`、组件含 `PropGrouper` |
| Caldera/Volcano 静态岩石 | 非机制子区 | 中风险候选 | 需确认只是静态 renderer/collider，不带伤害/全局状态 |
| Caldera/Volcano 岩浆机制/上升岩浆 | 任意跨区段 | 禁止第一阶段 | 机制对象、伤害/网络/全局状态风险 |

## UI 设计落点

- 模板库必须可全局搜索，但默认按当前 Segment 过滤。
- 每个模板显示来源 Segment/Grouper/Step、风险标记、默认数量/范围、是否 Photon、是否父子模板、是否单物品模板。
- 添加时必须先选目标 `SubArea`。右键菜单使用“添加到当前子区”，拖拽到预览区域时落到鼠标命中的目标子区或手动坐标。
- 对跨区段模板显示兼容提示：例如“来源：雨林平台；目标：沙漠平台；使用目标区域落地参数，来源默认数量不直接继承”。
- 高风险模板按钮禁用或需要明确实验开关，不能和普通灌木树石头混在一起。

## 资料缺口

- 仍缺每个地图变体的干净模板快照，尤其 active variant 与完整层级 path。
- 仍缺 `SubArea` 默认映射：Plateau、Wall、WallLeft/Right、Entrance、山顶、山腰、洞口如何映射到 XYZ 和 ray/layer。
- 仍缺可执行 prefab 加载方式：当前 `ObjectCatalog` 能描述模板，但不保证重进后能找到同一个 prefab 引用。
- 仍缺父子模板的依赖图：哪些子 `LevelGenStep` 要执行，哪些要禁用，哪些必须随父物一起移动/删除/同步。
- 仍缺外部 Unity 物品格式：AssetBundle、Addressables、场景克隆、还是只允许游戏内 prefab。
- 仍缺多人协议细节：中途加入如何补发已生成物，资源缺失如何降级，ViewID 数量不一致如何报错。

## 下一步实现顺序

1. 先补稳定 path 和 `DaObjectRegistry`，把 `ObjectCatalog` 从只读诊断升级为可查找模板注册表。
2. 加 `DaSubAreaData`，允许手动创建/编辑一个目标子区，先保存中心 XYZ、范围、ray、layer。
3. 做第一条低风险 `PlacementRule`：游戏内普通 prefab + 数量 + 缩放 + 随机旋转 + 目标子区落地 + ownership 清理。
4. 再做跨区段过滤和 UI：当前区段模板/全局模板切换，右键或拖拽添加到当前子区。
5. 最后再处理父子模板、PhotonView、SingleItemSpawner、外部 AssetBundle 和材质编辑。
