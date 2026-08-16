# WhereIsThing Decisions

更新时间：2026-08-14

## 数据来源

- 物品名单必须来自 2.1.a `ItemDatabase.itemLookup`，不维护容易过期的硬编码 ID 清单。
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
- 位置范围通过 `ThingLocationScope` 控制：`Ground`、`Held`、`Backpack`、`Luggage`；默认启用地面、背包和未打开行李箱，手持可在窗口中开启。

## UI

- 选择窗口采用独立 ScreenSpaceOverlay Canvas，面板按屏幕比例伸缩，深色面板、滚动列表和固定底部操作栏，视觉上参考原版 MenuWindow/ItemBrowser。
- 每个分类使用 GridLayoutGroup，按当前列表宽度和最小卡片宽度自动计算列数；不把窗口项目限制为一列。
- 打开窗口时保存 `Cursor.visible`/`Cursor.lockState`，窗口存续期间持续显示并解锁鼠标，关闭时恢复保存值。
- 所有 TMP 文本都必须显式指派游戏字体；优先 `AscentUI.text.font`，再按通用字体规范兜底。
- 类别是筛选辅助，不是游戏正式分类。能用游戏 `ItemTags` 时优先使用，名称关键词只作为补充，未命中的物品仍归入“其他”。

## 禁止回退

- 不要将 2.1.a 动态数据库改回固定物品名单。
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
- `ThingLocationScope` 是 `[Flags]` 多选值，不改成普通下拉菜单；正式入口仍是选择窗口中的四个复选框。
- 游戏语言变化时重新写入 `ConfigDescription` 并刷新 ModConfig 缓存；ModConfig 未安装或初始化尚未完成时必须静默降级，不影响位置显示主体。
