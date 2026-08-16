# WhereIsThing Recent

更新时间：2026-08-14

## 当前阶段

- 新建 `WhereIsThing` 0.1.0 工程，目标基线为 PEAK 2.1.a。
- 已复查 2.1.a 反编译：`Item.itemID`、`Item.itemState`、`Item.UIData.itemName`、`Item.data`、`BackpackData.itemSlots`、`ItemSlot.prefab` 和 `LocalizedText.GetText` 均可用于本 MOD。
- 已实现 `ThingCatalog`，从 `SingletonAsset<ItemDatabase>.Instance.itemLookup` 动态读取物品定义，按 ID 去重。
- 已实现 `ThingCatalog` 的显示名合并：同名物品组保存多个 itemID，选择一次覆盖所有变体，并显示变体数。
- 兼容旧选择配置：目录首次加载时，旧配置中命中的单个变体会扩展为整个同名组。
- 已实现 `WhereIsThingPlugin`：默认 `C` 扫描，常驻/计时显示，0.5 秒补扫一次，清理已拾取或失效目标；支持地面、手持、背包、行李箱范围。
- 已接入 `Luggage.ALL_LUGGAGE`，未打开行李箱作为独立场景目标显示，打开后由游戏生成的 Item 继续走物品扫描。
- 已实现 `ThingLabel`：世界坐标标签、距离、屏幕外方向箭头和游戏字体。
- 已实现 `ThingSelectionWindow`：Overlay Canvas、深色 ItemBrowser 风格面板、搜索、类别轮换、语言轮换、范围复选框、批量选择和分类内自适应多列网格。
- 窗口打开期间在 Update/LateUpdate 持续设置鼠标可见和解锁，关闭、应用、取消、Escape 或场景重载时恢复原状态。
- Release 构建通过：`0 warnings / 0 errors`，DLL 输出到 r2modman `2.0.a` profile 的 `BepInEx/plugins/WhereIsThing.dll`。

## 2026-08-15 光标修复

- 发现 2.1.a 的 `CursorHandler` 会根据 `GUIManager.windowShowingCursor` 每帧重新锁定和隐藏光标；单独 Overlay Canvas 的 `Cursor.visible` 设置会被覆盖。
- `ThingSelectionWindow` 现在创建一个保持 inactive 的 `MenuWindow` 代理，并在窗口打开期间注册到 `MenuWindow.AllActiveWindows`，关闭、应用、取消、Escape、场景重载和销毁时移除。
- 注册或移除代理后主动调用 `GUIManager.UpdateWindowStatus()`；窗口更新期间若被其他系统移除，会自动重新注册。
- 修复后重新构建通过：`0 warnings / 0 errors`，产物为 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`。

## 2026-08-15 行李箱细分

- 行李箱选择从单一 `SelectedLuggage` 布尔值扩展为 `SelectedLuggageTypes` 类型集合，窗口可分别选择复活箱、海滩、丛林、苔原、火山口、攀登、远古、诅咒、台地、根系、阴郁、城塞、小丑和其他行李箱。
- 2.1.a 中 `Luggage` 继承 `Spawner`，分类依据实例的 `spawnPool`；`RespawnChest` 单独识别，`LuggageCursed`/诅咒池和 `ClownLuggage` 标签/小丑池优先识别。
- 扫描仍使用 `Luggage.ALL_LUGGAGE`，只显示未打开且属于当前选择类型的行李箱；标签名称继续优先使用游戏原本翻译。
- 旧配置 `SelectedLuggage=true` 首次加载时迁移为全选新行李箱类型；窗口应用后同时写回新配置和旧布尔配置，保持兼容。
- 行李箱细分版本已构建到 r2modman `2.0.a` profile：`WhereIsThing.dll`，构建结果 `0 warnings / 0 errors`。

## 2026-08-16 行李箱跟随游戏语言修复

- 修复行李箱分类名在 `NameLanguage=Game` 时总是落入英文分支的问题。
- 跟随游戏模式现在读取 2.1.a 的公开字段 `LocalizedText.CURRENT_LANGUAGE`；简体中文和繁体中文显示中文分类名，其余语言使用英文分类名。
- 显式选择 `English` 或 `SimplifiedChinese` 时行为不变；Release 重新构建为 `0 warnings / 0 errors` 并覆盖 r2modman profile 中的 DLL。

## 2026-08-16 ModConfig 中英文本地化

- 新增仅作用于 `com.wuyachiyu.WhereIsThing.cfg` 的 ModConfig 本地化补丁，分组、配置项名称、说明和枚举选项会跟随游戏中英文切换。
- `ScanMode` 和 `NameLanguage` 保持 ModConfig 枚举下拉菜单，并分别显示“常驻/定时”和“跟随游戏/English/简体中文”。
- `LocationScopes` 是可组合的标志值，主要通过 `Alt+C` 窗口里的地面、手持、背包、行李箱复选框修改；ModConfig 说明中明确这一点。
- 选择 ID、旧行李箱布尔值和行李箱类型字符串标明为窗口维护/兼容配置，避免用户误以为需要手填。
- 监听 `LocalizedText.OnLangugageChanged` 更新说明并刷新 ModConfig 缓存；Release 构建通过 `0 warnings / 0 errors`，DLL 已覆盖到 r2modman `2.0.a` profile。

## 2026-08-16 已选筛选与场景目标调研

- 选择窗口新增“Selected only / 仅显示已选”复选框，可与类别和搜索组合筛选；在该模式下取消目标勾选后，目标会立即从列表消失，便于批量整理当前选择。
- 2.1.a 调研确认森蕈僵尸可通过 `ZombieManager.Instance.zombies` 跟踪，并用 `MushroomZombie.currentState != Dead` 判断有效性。
- 甲虫可通过 `MobManager.instance.mobs` 过滤 `Beetle` 跟踪；风滚草没有全局管理列表，但有独立 `TumbleWeed` 组件，可在现有 0.5 秒补扫中使用 `FindObjectsByType<TumbleWeed>`。
- GhostBall 可优先通过 `Peak.GhostBallSpawner.Instance.currentGhostBall` 跟踪；生成器保证同一时间只维护一个活动球，Photon 实例销毁后引用自动失效。
- 雾沼五座钟塔的稳定运行时锚点是 `GhostFire`：它继承 `GloomSafeZone`，注册到 `GloomSafeZone.ALL_GLOOM_SAFE_ZONES`；原版 `BellringerBadge` 逻辑正是统计 5 个已点亮的 `GhostFire`。
- 动态危险与钟塔本轮仅完成可行性调研，尚未加入目录、配置或扫描；Release 构建通过 `0 warnings / 0 errors` 并覆盖 r2modman profile DLL。

## 2026-08-16 场景目标接入

- 已新增独立 `ThingSceneTargetType` 目录和 `SelectedSceneTargetTypes` 配置，窗口可在“危险”分类选择森蕈僵尸、甲虫、风滚草、鬼球，并在“地标”分类选择雾沼钟塔。
- 场景目标与 `Item`、`Luggage` 使用独立的选择集合和字符串配置持久化，不分配虚构 itemID；“仅显示已选”、搜索、类别筛选、批量选择/清除均已支持。
- 森蕈僵尸从 `ZombieManager.Instance.zombies` 跟踪并排除 `State.Dead`；甲虫从 `MobManager.instance.mobs.OfType<Beetle>()` 跟踪；风滚草每 0.5 秒使用 `FindObjectsByType<TumbleWeed>` 补扫；GhostBall 使用 `GhostBallSpawner.Instance.currentGhostBall`。
- 雾沼钟塔从 `GloomSafeZone.ALL_GLOOM_SAFE_ZONES.OfType<GhostFire>()` 跟踪，标签使用游戏名称优先并追加“已点亮/未点亮”状态。
- ModConfig 增加 `SelectedSceneTargetTypes` 的中英文名称和说明，明确该配置由 `Alt+C` 窗口维护。
- Release 构建通过：`0 warnings / 0 errors`；产物为 r2modman `2.0.a` profile 中的 `WhereIsThing.dll`。

## 2026-08-16 第一批危险目标接入

- 场景目录新增“危险生物”“自然危险”“机关陷阱”分类：危险生物为森蕈僵尸、甲虫、蝎子、蜘蛛、蜂群、Scoutmaster；自然危险为风滚草、GhostBall、地刺、蚁狮、捕蝇草、龙卷风和催眠浆果幻象球；机关陷阱为箭矢发射器、移动锯刃、滚刺机关、摆斧机关。
- 甲虫和蝎子共用 `MobManager.instance.mobs` 的一次遍历；其余目标仅在用户勾选对应类型时，以其 2.1.a 明确组件类型在现有 0.5 秒刷新周期扫描。
- 目标类型已加入 Alt+C 目录、搜索、仅显示已选、配置持久化和 ModConfig 中英文枚举显示；场景目标仍不受地面/手持/背包/行李箱范围开关限制。
- 用户明确要求本批不加入青蛙舌，故没有枚举、窗口项目或扫描分支；未加入全局环境状态、易刷屏静态地形，以及已由普通物品追踪覆盖的危险物。
- Release 构建通过：`0 warnings / 0 errors`；`WhereIsThing.dll` 已输出至 r2modman `2.0.a` profile，大小 `75776` 字节。

## 2026-08-16 同名物品与场景目标去重

- 修复蝎子等对象在窗口中同时出现“物品组”和“危险生物”两个同名入口的问题；目录现在会把英文游戏名相同的物品组与场景目标合并成一个联合条目，并使用场景行为分类。
- 联合条目勾选/取消时同步更新 itemID 和 `ThingSceneTargetType`；旧配置中任一侧已选都会在目录加载时补齐另一侧并持久化。
- 地面 `MobItem` 在对应场景目标已选时跳过普通物品标签，由 `MobManager` 统一显示；手持和背包形态仍走原物品范围逻辑，解决同一只蝎子叠加两层文字的问题。
- Release 构建通过：`0 warnings / 0 errors`，DLL 已覆盖到 r2modman `2.0.a` profile。

## 尚未完成

- 尚未在 2.1.a 实机确认物品数据库加载时机、标签遮挡、背包内容位置和大量标签性能。
- 尚未确认所有物品 prefab 的 `itemState` / `data` 生命周期，尤其是手持和嵌套背包情况。
- 尚未实机确认动态目标在多人非主机端的管理器列表同步、钟塔标签的视觉锚点，以及同时显示多只风滚草时的性能。
- 尚未制作 `发行/0.1.0`、manifest、README、CHANGELOG、icon 和 zip。
- 暂未接入原版 `MenuWindow` 类型，因为其 `Open`/`Close` 生命周期有 internal 边界；当前窗口保持独立 Overlay，并手动管理鼠标状态。
