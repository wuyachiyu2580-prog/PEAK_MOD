# WhereIsThing Decisions

更新时间：2026-09-06

## 物品分类决策

- WhereIsThing 保持一个物品只属于一个展示分类；不得直接改成 ItemSpawnerEnhanced 的可重叠过滤标签，否则会改变现有选择窗口语义。
- 分类优先级固定为：特殊物品游戏标签、神秘游戏标签、精确 prefab 覆盖、食物游戏标签、用途关键词、`Misc`。精确覆盖用于解决 `pack`、`dart`、`fungus` 等子串造成的误判。
- ItemSpawnerEnhanced 的审阅名单只作为分类证据和回归清单；其插件自述基线为 PEAK 2.1.a。当前 137 个审阅 prefab 虽已确认仍存在于 2.4.b 资源中，仍不得自动覆盖 WhereIsThing 的定位用途分类。
- 可食用性不覆盖定位用途：Early Worm、Beehive 归生物，Scorpion 与场景目标合并归危险生物，棋子归玩具，Honeycomb 归食物。
- `Deployable` 不新增为窗口分类，也不代表能可靠显示放置者；玩家放置目标继续按攀爬、生存、武器等用途分类，owner 仍由现有纯客户端证据决定。
- 不直接采用 ItemSpawnerEnhanced 的默认隐藏名单；Prop、Variant 和内部物品继续由同名合并与现有目录规则处理，避免漏掉真实场景实例。

## PEAK 2.4.b 放置体与 owner 决策

- 当前反编译/编译基线是 `C:\SteamLibrary\steamapps\common\PEAK` 的 `2.4.b`，不得回到 1.65.a 或 2.3.a 地址作为当前事实。
- 玩家放置体使用物品目录行为组件指向的生成 prefab 做正向识别，不得只看通用组件类型或对象名黑名单。
- 绳索 owner 的权威来源是已知物品锚点上的玩家 PhotonView；绳索本体只提供 anchored 状态和显示位置关系。系统、机场、神庙、`PeakSequence` 和可破坏锚点绳索一律排除。
- `JungleVine` 本身不足以证明来自锁链发射器；必须命中 `VineShooter.vinePrefab`，同时排除 `BreakableBridge`。
- 蘑菇的玩家名不增加 RPC/房间属性。仅使用本地收到的 `OnItemThrown` 与 `lastThrownCharacter` 证据做严格唯一匹配；无法确认放置者时显示标签但不显示玩家名。
- 魔豆仍然只显示名称和距离：`InstantiateRoomObject` 不保留可靠种植者，纯客户端条件下不得猜 owner。
- owner 只在首次分类、有限延迟重试或状态变化时解析并缓存；不得恢复每帧父子层级搜索。
- 未分类 PhotonView 只允许 2 秒内有限重试，并受每帧数量/时间预算限制；5 秒校验不得恢复成单帧全量重分类。
- WhereIsThing 必须是只读定位工具：不得为修标签或 owner 逻辑销毁道具、清空物品栏、补调消耗或拦截原版放置流程。
- `0.1.2` 仍处于测试收口；未经房主/客户端验收前，不更新发行目录、不重打 ZIP。

## 数据来源

- 物品名单必须来自当前游戏的 `ItemDatabase.itemLookup`，不维护容易过期的硬编码 ID 清单；早期规则最初在 2.1.a 建立，2.4.b 继续沿用动态目录原则。
- 地面和手持目标按真实 `Item` 实例跟踪；背包内槽位没有独立可用的地面 Transform，因此显示承载该物品的落地背包位置。
- 同一英文游戏显示名的多个 Item prefab 合并为一个选择组；组内保留全部 itemID，避免窗口出现大量重名项，也不丢失实际变体。
- 旧版 `SelectedItemIds` 若只命中组内一个 ID，首次加载目录时扩展为该组所有 ID，避免窗口合并但扫描仍只跟踪单一变体。
- `Luggage` 继承 `Spawner` 而不是 `Item`，不能塞入物品数据库；使用游戏公开的 `Luggage.ALL_LUGGAGE` 跟踪未打开行李箱，打开后等待普通 Item 生成。
- 名称优先走游戏 `LocalizedText.GetText(LocalizedText.GetNameIndex(item.UIData.itemName), language)`；跟随游戏模式走 `Item.GetName()`，英文/简体中文明确读取游戏名称表。

## 交互

- 默认 `C` 执行扫描，默认 `Alt+C` 打开选择窗口。按住 Alt 时不重复触发普通扫描。
- `ScanMode=Persistent` 保持标签；`ScanMode=Timed` 使用 `DisplayDurationSeconds` 自动隐藏。
- 选择窗口先编辑工作副本，点击 `Apply` 才写回选择 ID 和语言配置；取消或 Escape 放弃本次窗口修改。
- 标签只读显示，不改变物品、背包、网络状态或生成逻辑。
- 位置范围窗口提供 `Ground`、`Held`、`Backpack`、`Statue` 四个复选框；`Statue` 只影响已选护符在场景雕像手中的碎片显示，不新增目标列表项。`ThingLocationScope.Luggage` 保留用于旧配置兼容，但扫描是否包含行李箱完全由 `SelectedLuggageTypes` 是否为空自动决定。至少选择一种行李箱类型时自动加入该范围，全部取消时自动清除。

## UI

- 选择窗口采用独立 ScreenSpaceOverlay Canvas，面板按屏幕比例伸缩，深色面板、滚动列表和固定底部操作栏，视觉上参考原版 MenuWindow/ItemBrowser。
- 每个分类使用 GridLayoutGroup，按当前列表宽度和最小卡片宽度自动计算列数；不把窗口项目限制为一列。
- 打开窗口时保存 `Cursor.visible`/`Cursor.lockState`，窗口存续期间持续显示并解锁鼠标，关闭时恢复保存值。
- 所有 TMP 文本都必须显式指派游戏字体；优先 `AscentUI.text.font`，再按通用字体规范兜底。
- 类别是筛选辅助，不是游戏正式分类。能用游戏 `ItemTags` 时优先使用，名称关键词只作为补充，未命中的物品仍归入“其他”。
- 分类补充规则：`Peak.EarlyWorm` 组件优先归入生物；食物/可食用素材、医疗、武器、玩具与运动使用精确名称关键词；Checkpoint Flag、Conch、Magic Bean、Megaphone、Portable Stove、Stick、Stone 使用“生存工具”类别；AK 使用完整名称判断，避免命中 Snake 中的 `ak` 子串。

## 禁止回退

- 不要将动态 `ItemDatabase` 改回固定物品名单。
- 不要把背包槽位 prefab 当成真实场景物体直接创建或移动。
- 不要把 `Luggage` 强行伪装成 Item 或分配虚构 itemID；它是 Spawner，应走 `Luggage.ALL_LUGGAGE`。
- 不要用 MOD 自造名称覆盖游戏已有翻译；新增翻译只能作为游戏表缺失时的兜底。
- 不要为了窗口输入直接改动游戏网络或玩家控制逻辑；实机若出现输入穿透，再单独评估与原版 `MenuWindow`/`UIInputHandler` 的兼容接入。

## 光标兼容

- 选择窗口虽然使用独立 Overlay Canvas，但必须通过 inactive `MenuWindow` 代理接入 `MenuWindow.AllActiveWindows`；这是 2.1.a `GUIManager.UpdateWindowStatus()` 和 `CursorHandler.Update()` 识别“窗口正在显示光标”的正式状态来源。
- 代理只作为状态登记对象，不调用原版 `MenuWindow.Open`，避免其默认生命周期和自动选择逻辑影响自定义窗口。

## 行李箱分类

- 行李箱不进入 `ItemDatabase`，而是在选择目录中加入独立的 `ThingLuggageType` 定义；分类项固定为 `RespawnChest`、十个普通场景池、`Cursed`、`Clown` 和 `Other`。
- 分类顺序优先使用特殊运行时类型和标签，再使用 `Luggage.spawnPool`；无法识别的池保留为 `Other`，避免静默漏掉新版本行李箱。
- `SelectedLuggageTypes` 使用枚举名称逗号分隔持久化，不保存易变的显示文本；旧 `SelectedLuggage` 仅作为迁移和兼容状态保留。
- 选择窗口的勾选状态、批量选择/清除和扫描有效性都使用同一个类型集合，避免窗口显示已勾选但扫描仍显示全部行李箱。
- 行李箱类型名是 MOD 补充的细分类，`NameLanguage=Game` 时必须通过 `LocalizedText.CURRENT_LANGUAGE` 决定中英文，不能直接把 `Game` 当作英文；简体和繁体中文都使用中文分类名。

## ModConfig 本地化

- 保留 BepInEx 原始 section/key 和枚举值，避免现有配置失效；只通过 Harmony 和 ModConfig UI 文本替换修改玩家看到的名称。
- ModConfig `GetDisplayName` 后缀必须按配置文件名限制为 WhereIsThing，不能改写其他 MOD 的配置项名称。
- `ThingScanMode`、`ThingNameLanguage` 使用枚举配置，由 ModConfig 自动提供下拉菜单；中文只替换下拉显示文本，不改变序列化值。
- `ThingLocationScope` 是 `[Flags]` 多选值，不改成普通下拉菜单；正式入口是选择窗口中的 Ground、Held、Backpack、Statue 四个复选框，Luggage 由行李箱类型自动维护。

## 护符雕像碎片

- 第 1~4 关手里拿着碎片的场景雕像使用 `Peak.PropSpawner_AmuletStatues`，不是复活队友相关的 `ScoutStatue`。
- 雕像碎片不作为新的 `ThingTargetDefinition` 或独立目标类别出现；用户选择仍是四个护符本身，是否额外显示雕像上的碎片由 `ThingLocationScope.Statue` 控制。
- 扫描只读 `PropSpawner_AmuletStatues` 已生成的子物体和护符 prefab 定义，不执行生成逻辑，不拿起物品，不改变网络、场景或存档状态。
- 识别优先看雕像子物体名称中的护符关键词；只有 `props.Length == 4` 且 `statueIndex` 合法时才回退使用 `statueIndex`，避免在随机化表更复杂时误判。
- 游戏语言变化时只重新写入 WhereIsThing 自己的 `ConfigDescription` 并刷新当前可见 UI；禁止清空或重建 ModConfig 全局缓存，禁止调用 `ProcessModEntries` / `LoadModSettings`。ModConfig 未安装或初始化尚未完成时必须静默降级，不影响位置显示主体。
- 配置项本地化使用稳定的 `Definition.Section + Definition.Key`，中文只作为显示文本；配置文件中的 section/key 和枚举序列化值保持原样。

## 场景目标调研

- 僵尸、甲虫、风滚草、GhostBall 和 GhostFire 钟塔都不是普通 `Item`，后续必须新增独立场景目标定义，不能分配虚构 itemID 或塞进 `ItemDatabase`。
- 优先使用原版稳定注册表：僵尸用 `ZombieManager.zombies`，甲虫用 `MobManager.mobs`，GhostBall 用 `GhostBallSpawner.currentGhostBall`，钟塔用 `GloomSafeZone.ALL_GLOOM_SAFE_ZONES.OfType<GhostFire>()`。
- 风滚草没有原版全局列表，允许复用标签刷新周期执行 `FindObjectsByType<TumbleWeed>`；不要每帧全场景扫描。
- 钟塔以 `GhostFire` 组件识别，不依赖 `ClockTower` 对象名。后续可显示全部五座，并在标签中附加已点亮/未点亮状态；是否默认只显示未点亮塔由实机体验决定。
- 动态目标标签必须跟随各自真实 Transform，并以死亡、销毁、禁用或管理器移除作为失效条件；不改变 Photon 所有权、AI、生成和建筑交互状态。

## 选择整理

- “仅显示已选”是窗口工作态筛选，不写入配置；每次打开窗口默认关闭。
- 该筛选与类别、搜索取交集；筛选开启时取消勾选的项目应立即消失，点击 Apply 前仍遵守窗口工作副本语义。

## 杂项审计

- 初始审计中的 33 个 `Misc` 显示名组已按用户选择的 B1/B2/B3 方案重新分类；按英文显示名合并后，2.1.a 资源快照的 194 个物品 ID 当前剩余 `Misc` 为 0 组。
- 变体合并仍然以英文显示名为键，莓蕉皮、王莓和棋子不会因分类变化而重复出现；运行时名称表、标签和实机用途仍需验证。
- 资源中的 `itemTags` 对部分物品变体并不完整，杂项重分类不能只依据资源快照；最终修改应结合运行时 `ItemDatabase` 对象和实机显示用途验证。

## 已接入场景目标

- 场景目标配置键为 `SelectedSceneTargetTypes`，按 `ThingSceneTargetType` 枚举名保存；默认不自动选择，用户需在“危险”或“地标”窗口分类中勾选。
- 僵尸、甲虫和 GhostBall 使用原版管理器引用，避免全场景扫描；风滚草是唯一没有管理器的类型，限定为现有 0.5 秒刷新周期扫描。
- GhostFire 是雾沼钟塔的正式标记对象；标签锚定 GhostFire Transform，标题优先使用 `GhostFire.GetName()`，并额外显示亮灭状态。
- 场景目标不受物品的 Ground/Held/Backpack/Luggage 范围限制，因为它们始终是场景对象；取消其类型选择后，现有标签在下一次刷新立即移除。

## 第一批危险目标

- 场景目标按玩家面对的威胁形式分类：会追击或攻击的实体归“危险生物”，固定或自然形成的威胁归“自然危险”，明确的机械装置归“机关陷阱”；钟塔继续单列为地标。
- 蝎子与甲虫都继承 `Mob`，因此共用 `MobManager.mobs` 一次遍历；蜘蛛、蜂群、Scoutmaster 和各类自然/机关组件使用受用户选择门控的 `FindObjectsByType<T>`，不改变游戏 AI、机关或 Photon 状态。
- 用户明确排除青蛙舌，即使它在原版属于危险项，也不建立 `ThingSceneTargetType`、窗口条目或扫描分支。
- 全局天气/状态类危险没有单一可标记位置；高密度静态地形会造成标签刷屏；仙人掌球、炸药、曼德拉草和蜂巢可走普通物品追踪。因此以上目标不纳入本批场景危险。

## 物品与场景目标合并

- 同一概念同时存在普通 `Item` 和场景实体时，选择窗口只保留一个联合条目；当前按英文游戏显示名的标准化键匹配，不为蝎子、甲虫等对象显示两份同名入口。
- 联合条目使用场景行为分类，例如蝎子归“危险生物”，勾选一次同时维护组内全部 itemID 和对应 `ThingSceneTargetType`；取消时也同时清除两侧选择。
- 旧配置若只选择了 itemID 或场景目标枚举，目录加载时自动扩展为联合选择并写回两份配置，避免升级后只追踪一种形态。
- `MobItem` 落地时由场景实体扫描负责标签，普通 Item 扫描跳过同一对象，避免一只蝎子叠两层标签；手持或进入背包后改回物品扫描，并继续受 Held/Backpack 范围开关控制。
