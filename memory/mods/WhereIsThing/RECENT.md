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

## 尚未完成

- 尚未在 2.1.a 实机确认物品数据库加载时机、标签遮挡、背包内容位置和大量标签性能。
- 尚未确认所有物品 prefab 的 `itemState` / `data` 生命周期，尤其是手持和嵌套背包情况。
- 尚未制作 `发行/0.1.0`、manifest、README、CHANGELOG、icon 和 zip。
- 暂未接入原版 `MenuWindow` 类型，因为其 `Open`/`Close` 生命周期有 internal 边界；当前窗口保持独立 Overlay，并手动管理鼠标状态。
