# StateKeeper Item Analysis

> 2026-09-08 审计更正：以 `RESEARCH_2026-09-08.md` 为当前采集语义与算法实现状态依据。本文的关联算法是设计，不代表已经实现；Petrify 数组槽不能代表真实石化；完整快照差分不能单独证明使用；prefab 和烹饪次数不足以保证还原所有运行时 MOD/随机作用参数。

更新时间：2026-09-07

## 结论

第一版不需要监听 BetterItemInfoDisplay 的全部 `ItemAction`。现有三类数据已经足够做可解释的基础分析：

1. 物品栏快照差分：判断实例是否移动、替换、消耗或资源变化。
2. 玩家位置/距离：判断物品效果发生时的空间关系、救援距离和队伍是否分散。
3. 体力/状态曲线：判断食物、药物、体力类物品使用后的时间关联结果。

BetterItemInfoDisplay 和 PEAK 2.4.b 反编译代码应作为 `ItemDefinition` 的来源，用来解释物品理论作用；不能把它的 UI 文本或静态 Action 定义直接当作实际发生结果。

首局 schema 3 长局已经验证实例级链路可用：1,998 条带 GUID 的物品事件有 1,944 条能连接到库存实例（约 97.3%）。因此第一版分析器可以开始实现，但资源变化应以相邻完整快照的逐字段差分为主，`StatsEvent.resourceKey` 只作索引和即时证据，不应视为该次扫描唯一变化字段。

`DataEntryKey.Used` 存在两种运行时类型：MagicBean 使用 `OptionableBoolItemData`，Mandrake 使用 `BoolItemData`。采集器必须兼容两种类型；只读取前者会漏掉 Mandrake。

定义目录优先在游戏内从 `ItemDatabase` 的 prefab 实例构建一次，再保存稳定的结构化摘要。参考插件已经证明可以从 prefab 遍历 `ItemActionBase` 和组件；反编译代码用于确认字段语义和分类，不建议把 115 个物品手工硬编码成名称表。这样也能在 `ItemCooking.UpdateCookedBehavior()` 动态增加或修改 Action 时，按实例的 `CookedAmount` 得到正确的定义版本。

推荐最终在面板中分开显示：

| 理论作用 | 实际观察结果 | 证据/置信度 |
| --- | --- | --- |
| 由 Action、Component、Cooking 规则推导 | 由快照差分、体力、状态、位置和事件推导 | Certain / Likely / Ambiguous |

## 五局覆盖结果

数据目录中的五局共有 639 个 GZip 碎块、6,911 个库存快照、238,915 个事件。库存快照中有 25,007 个非空物品条目，出现 115 种 `itemId + itemName + prefabName` 组合；所有非空条目都有 GUID。

当前字段覆盖与同 GUID 变化转移如下：

| 字段 | 非空条目覆盖 | 同 GUID 变化转移 | 解释 |
| --- | ---: | ---: | --- |
| `ItemUses` | 24,798 | 610 | 普通次数、无限值 `-1`、特殊物品兼容值都可能使用此字段 |
| `UseRemainingPercentage` | 7,954 | 638 | 可显示为剩余比例；不能一概命名为耐久 |
| `Fuel` | 1,767 | 297 | Jetpack、灯笼、绳索卷、魔法号角等资源主证据 |
| `CookedAmount` | 23,878 | 305 | 几乎是通用实例数据；`0` 才表示未烹饪，非“字段存在即已烹饪” |

实际事件量为：`ItemChangedObserved=3,211`、`ItemRemovedObserved=3,113`、`ItemResourceChanged=1,443`、`ItemConsumed=906`、`ItemUsesReduced=390`。物品移除明显多于次数减少，因此“槽位消失 = 使用”会严重夸大消耗。

### `ItemUses` 的重要陷阱

当前采集代码使用 `TryGetDataEntry<OptionableIntItemData>` 后就设置 `hasUses=true`，但 PEAK 的 `OptionableIntItemData` 还有自己的 `HasData`。`Item.Start()` 会为物品创建这个条目，即使该物品没有有效的使用次数；只有 `HasData=true` 时，`Value` 才是可解释的次数。后续模型应分开保存：

```text
hasUsesEntry       // 数据字典里存在条目
hasUsesValue       // OptionableIntItemData.HasData == true
uses               // 仅在 hasUsesValue 时解释
```

历史文件只有 `hasUses`，不能事后可靠恢复这个区别；分析器应将旧 schema 的该字段标记为 `legacy-ambiguous`，并优先使用 GUID 资源差分、`ItemUsesReduced` 和定义层 `totalUses` 交叉确认。

五局出现频率较高且应优先验证语义的物品包括 Scout Cookies、Backpack、Rescue Hook、Marshmallow、Hot Dog、RopeSpool、ClimbingSpike、多个 Amulet、Jetpack、Lantern、RopeShooter、Glider、治疗/食物类物品和多个蘑菇变体。数据还覆盖 MagicBean、Mandrake、Flare、Balloon、Beehive、Snowball、Dynamite、Frog、Scorpion、RitualDagger 等特殊物品，说明不能只为食物设计算法。

## 静态定义来源

`BetterItemInfoDisplay/ActionInfoBuilder.cs` 的有效思路是遍历：

```csharp
item.GetComponentsInChildren<ItemActionBase>(false)
```

并按具体 Action 类型与触发器解释作用。2.4.b 中 `ItemAction` 的触发器包括：

```text
PrimaryPressed, PrimaryHeld, PrimaryReleased,
SecondaryPressed, SecondaryHeld, SecondaryReleased,
PrimaryCastFinished, SecondaryCastFinished, Cancelled, Consumed
```

应提取成结构化定义，而不是保存 UI 文本：

```text
ItemDefinition
  itemId
  itemName
  prefabName
  tags
  baseUses
  primaryUseTime
  secondaryUseTime
  actions[]
  components[]
  cookingRules[]
```

Action 归类建议：

| 类别 | 典型 Action/Component | 可关联的实际数据 |
| --- | --- | --- |
| 食物/恢复 | `Action_RestoreHunger`, `Action_GiveExtraStamina`, `Action_HealingGem` | 体力、状态、消耗/次数变化 |
| 状态改变 | `Action_ModifyStatus`, `Action_ApplyAffliction`, `Action_ClearAllStatus`, `Action_ApplyInfiniteStamina` | `statuses[]`、`activeAfflictionTypes`、体力曲线 |
| 燃料/照明 | `JetpackItem`, `Lantern`, `Candle`, `Flare`, `MagicBugle`, `RopeSpool` | `Fuel`、照明状态、火箭状态 |
| 绳索/救援 | `RescueHook`, `RopeShooter`, `RopeSpool`, `VineShooter` | `ItemUses`、`PetterItemUses`、玩家距离/位置 |
| 护符/被动 | `AmuletBase`, `DoubleJumpAmulet`, `HealingAmulet`, `InfiniteStamAmulet` | `PowerEnabled`、状态/体力、携带时段 |
| 随机/投掷 | `Action_RandomMushroomEffect`, `Action_LaunchPlayer`, `Action_Balloon`, `Snowball`, `Frisbee`, `Dynamite` | 状态、位置突变、资源变化、事件时间 |
| 一次性/生成 | `MagicBean`, `Mandrake`, `Action_Spawn`, `Action_ConsumeAndSpawn`, `Beehive` | `Used`、生成/消耗事件、位置变化 |
| 烹饪变体 | `ItemCooking`, `CookingBehavior_*` | `CookedAmount` 与烹饪前后的作用定义 |

2.4.b 的 `DataEntryKey` 还包含：`Used`、`FlareActive`、`SpawnedBees`、`InstanceID`、`Color`、`PetterItemUses`、`ScreamTime`、`Scale`、`PowerEnabled`。这些字段不是都属于资源消耗，必须按语义分别处理。

## 与当前采集的差距

当前 `ItemSnapshot` 已保存：

```text
itemId, itemName, prefabName, guid, slot,
ItemUses, UseRemainingPercentage, Fuel, CookedAmount,
nestedSignature
```

当前 `StatsEvent` 已保存物品 ID、名称、槽位和时间，但没有 GUID，也没有事件前后的资源值。当前补丁记录 `ItemUseStarted` 和 `ItemPrimaryCastFinished`，但这两者只能作为时间锚点：

- `StartUsePrimary` 只表示开始尝试使用。
- `FinishCastPrimary` 只表示施法流程结束。
- `Action_Consume` 可能在延迟协程中才真正消耗。
- `Action_ReduceUses` 通过 RPC 改变使用次数。
- 燃料耗尽、降落伞触发、护符切换等消耗或状态改变可能不经过普通主动使用链路。

## 实现前缺口审计

### 已经足够，不必重复采集

- `PlayerTelemetry.statuses[]` 按 `statusTypeOrder` 保存全部 `CharacterAfflictions.STATUSTYPE`，其中已经包括 `Hunger`、`Injury`、`Poison`、`Petrify` 等。因此食物、治疗、毒、护符和蘑菇分析不需要另加独立的饥饿或生命值字段。
- `regularStamina`、`extraStamina` 和状态数组已经能支持体力/额外体力/状态的前后窗口比较。
- 5Hz 位置和两两距离已经足够做救援距离、队伍分散和物品事件附近距离；不需要提高库存或位置采样频率。

### 必须追加

1. **有效次数语义**：`OptionableIntItemData` 要同时记录条目存在和内部 `HasData`。旧数据中的 `hasUses` 只能按 `legacy-ambiguous` 处理。
2. **特殊实例字段**：优先追加 `PetterItemUses`、`Used`、`FlareActive`、`PowerEnabled`，并进入 `ItemFingerprint`，否则字段变化不会触发快照。
3. **事件实例关联**：`StatsEvent` 至少追加 `guid`、`definitionKey` 或 prefab 名称，以及可选的 `resourceKey`。事件必须能和同一 GUID 的快照差分连接。
4. **前后值证据**：资源变化事件追加 `value`、`previousValue` 的明确字段语义；如果补丁时不能可靠获得前值，仍要记录 GUID 和资源字段名，由分析器从快照补齐。
5. **跳跃事件**：如果需求中的“跳变化”包含实际跳跃/额外跳跃统计，低频追加 `PlayerJumped` 事件即可，优先补 `Character.OnJump`，不要把跳跃次数塞进每个 5Hz 样本。

### 建议追加，但可以延后

- `SpawnedBees`、`InstanceID`：只服务 Beehive 等特殊物品，不影响普通物品分析。
- `Scale`：雪球行为分析需要，但会是连续变化字段，不应默认进入高频指纹；可只在物品处于 Ground/Held 且变化超过阈值时采样。
- `Color`：只用于 Balloon/Flare 的变体识别，不属于使用结果，优先放在定义/实例上下文而不是资源变化事件。
- `ScreamTime`：曼德拉草的易变运行时计时，容易制造快照噪声；第一版只需 `Used`、`CookedAmount` 和物品流转，不建议采集。
- `ItemState`/丢弃/投掷事件：可提高“移除是丢弃还是消耗”的判定，但第一版可以保留 `Ambiguous`，不应为此监听所有物理状态。

### 还需要做的定向调研

1. **ItemDatabase prefab 实际可读性**：在真实运行时确认 `SingletonAsset<ItemDatabase>.Instance.itemLookup` 的 prefab 是否可以无副作用读取 Action/Component/Cooking 字段，并确认定义目录缓存键包含游戏版本和 MOD 变更。
2. **使用次数语义矩阵**：对普通食物、RescueHook、RopeShooter、Jetpack、Lantern、Amulet、MagicBean、Mandrake、Snowball 各做一次受控操作，确认 `ItemUses`、`UseRemainingPercentage`、`Fuel`、特殊字段和事件的变化顺序。
3. **事件重复与权威性**：确认 `ReduceUsesRPC`、`Consume`、`SetState` 在本机和远端对象上是否会重复触发；事件必须保留 `LocalAuthoritative/RemoteObserved`，必要时增加去重键。
4. **库存流转边界**：测试移动槽位、丢弃、投掷、喂给队友、死亡掉落、烹饪爆炸和跨玩家转移，验证 GUID 轨迹算法不会把流转误判为使用。
5. **跳跃定义**：确认 `Character.OnJump` 是实际起跳还是输入/动画阶段事件，并观察双跳护符、超级跳和普通跳的调用次数；以受控事件决定是否加入 `PlayerJumped`。
6. **性能基线**：新字段加入后用 8 人长局比较库存扫描耗时、GC、事件量、GZip 体积和结算峰值；不需要先做完整面板才能测量。

这几项是实现前的验证，不是继续扩大功能范围。除有效次数语义、四个特殊字段、事件 GUID 和可选跳跃事件外，不建议追加更多高频字段。

建议下一次 schema 以可选字段向后兼容地补齐：

```text
hasUsed / used
hasFlareActive / flareActive
hasPetterItemUses / petterItemUses
hasSpawnedBees / spawnedBees
hasPowerEnabled / powerEnabled
hasScale / scale
hasScreamTime / screamTime
hasInstanceId / instanceId
hasColor / colorIndex 或 colorR/G/B/A
```

快照字段应增加统一的 `DataEntryPresence` 语义，避免每种字段各自发明真假含义。例如：

```text
hasItemUsesEntry / hasItemUsesValue
hasUseRemaining / hasFuel / hasCookedAmount
hasUsed / hasFlareActive / hasPowerEnabled
```

普通 `BoolItemData`、`IntItemData` 和 `FloatItemData` 的“条目存在”通常已经意味着值有效；`Optionable*` 则必须额外检查其内部 `HasData`。

实现顺序：

1. 先补 `Used`、`PetterItemUses`、`FlareActive`、`PowerEnabled`。
2. 同时让 `ItemFingerprint` 和差分事件包含这些字段；否则采集了字段却仍会漏掉变化。
3. 将 `ItemConsumed`、`ItemUsesReduced` 的事件快照增加 GUID 和前后值；若无法在补丁时可靠取得前值，至少增加 GUID 和字段名。
4. 再补 `SpawnedBees`、`Scale`、`Color`、`ScreamTime`、`InstanceID`，按实际面板需求决定是否显示。

其中 `InstanceID` 不是物品身份替代品。实例关联仍优先使用 GUID；`InstanceID` 只用于 Beehive 等游戏内部对象关联。颜色和尺寸属于视觉/行为上下文，不能作为普通“使用次数”。

## 定义目录构建

建议在采集器初始化后、首个非空 RunId 建立时执行一次低频目录扫描：

1. 枚举 `ItemDatabase` 中的 prefab，读取 `itemID`、`UIData.itemName`、prefab 名称、`totalUses`、`usingTimePrimary`、交互能力和 `itemTags`。
2. 对 prefab 的 `ItemActionBase` 子组件保存类型名、启用状态和触发器位；只保存参数摘要，不保存 Unity 对象引用。
3. 对 `ItemComponent` 保存白名单组件类型，例如 `JetpackItem`、`Lantern`、`RopeSpool`、`RopeShooter`、`AmuletBase`、`Glider`、`Snowball`、`Mandrake` 等。
4. 对 `ItemCooking` 保存 `disableCooking`、`wreckWhenCooked`、`ignoreDefaultCookBehavior`、`ignoreDefaultPoisonBehavior` 和额外 CookingBehavior 类型名。
5. 目录条目按 prefab 名称和游戏版本缓存；物品实例只引用 `definitionKey`，不在每个快照重复写完整定义。

扫描必须避免调用会改变 prefab 状态的方法，也不能为了生成说明而执行 Action。定义扫描是只读元数据读取；动态的烹饪规则只做参数/类型描述，最终效果仍由实例 `CookedAmount` 和实际结果判定。

## 实际效果判定算法

### 1. 预处理

- 保留原始 `time`，另建 `analysisTime`；时间倒退切分 segment，并记录 `TimeNonMonotonic`。
- 快照按 `playerIndex -> time` 排序；同一时间的重复快照保留原始顺序并标记重复。
- 位置过滤有限数值、死亡对象和接近 `(0, 5000, -5000)` 的哨兵位置；原始距离不删除，只在派生视图排除。

### 2. 实例轨迹与槽位流转

对每个玩家的相邻快照建立槽位状态，并以 `(playerIndex, guid)` 建立实例轨迹：

```text
same guid + same slot       -> state update
same guid + different slot  -> Move
old guid absent + new guid  -> Add/Replace candidate
old guid absent everywhere  -> Remove candidate
```

变化类型按字段独立产生：

```text
uses decreases               -> UsesDecreased
fuel decreases               -> FuelDecreased
useRemaining decreases       -> RemainingDecreased
cookedAmount increases       -> CookingProgress
used false -> true            -> OneShotActivated
powerEnabled changes          -> PowerToggled
petterItemUses decreases      -> RopeAmmoDecreased
flareActive changes            -> LightToggled
```

### 3. 置信度规则

建议先使用规则系统，暂不使用不可解释的机器学习分类：

```text
Certain:
  同 GUID 的明确资源字段下降/上升；
  或事件带同 GUID 且前后值与快照差分一致。

Likely:
  物品 Action 完成事件后短窗口内出现对应体力/状态变化，
  且没有同窗竞争物品或时间质量异常。

Ambiguous:
  只有物品从槽位消失；
  只有槽位被另一个物品替换；
  只有远端事件而无对应本地资源变化；
  或期间存在死亡、传送、断线、时间倒退。
```

窗口初值建议：Action/资源变化前后各 `1.5s`，对延迟消耗再扩展到 `3s`；窗口应配置化并在输出中记录。一个窗口内多个候选物品时不强行唯一归因，输出候选列表或降低置信度。

### 4. 理论作用和观察结果的关联

定义层给每个物品输出 effect predicates，例如：

```text
restoresHunger
givesExtraStamina
modifiesStatus(Poison, delta)
consumesFuel
reducesUses
teleportsOrLaunches
requiresTarget
isPassiveWhilePocketed
```

观察层只检查对应信号是否存在：

```text
food candidate:
  consumed/uses change + stamina or status improvement in window

fuel item:
  Fuel decreases + rocket/light/rope state is active nearby

amulet candidate:
  PowerEnabled true + item remains in pocket + matching player state

rescue candidate:
  uses/PetterItemUses decrease + target/subject distance changes

random mushroom:
  consumed + one or more status changes; effect type remains uncertain
```

输出“关联”而不是“因果”。例如食物之后体力上升可以显示为 `Likely`，不能声称所有上升都由该食物造成。

## 面板第一版建议

单局详情先实现四个可筛选视图：

1. **物品流水**：时间、玩家、物品、动作/资源变化、槽位流转、观察结果、置信度。
2. **物品定义**：理论作用、触发方式、烹饪变体、字段解释。
3. **体力关联**：物品事件上下文中的体力/状态前后变化，不做因果评分。
4. **队伍关系**：物品事件发生时的队友距离、救援距离和有效样本质量。

默认视图只显示 `Certain` 和 `Likely`，提供开关查看 `Ambiguous`；低置信度记录不能伪装成“使用成功”。

## 暂不做

- 不复制 BetterItemInfoDisplay 的本地化 UI 文案生成器。
- 不在分析阶段执行 Action 或依赖 UI 文案推断物品作用；定义目录只读取 prefab 元数据。
- 不为第一版监听所有 Action 或扫描地面物品。
- 不把 `ItemUses` 存在就显示为可消耗次数。
- 不把 `CookedAmount` 存在就显示为已烹饪。
- 不把物品消失直接统计为使用。
- 不用 `StateChanged` 次数推断物品效果。
- 不做综合玩家评分、最佳队友或复杂战术因果评级。
