# WhereIsThing Recent

## 2026-11-16 WhereIsThing 审查修复已实施，未部署

用户已授权开始修改。共享会话/发布失败重试、目录自动就绪补发、无关属性过滤、扫描计时与菜单生命周期已修；有效选择缓存、标签绑定/分类复用、距离与设置局部更新及确定残留清理已完成。184/184 helper + 97/97 生产流程回归，真实依赖 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.fixes，未部署/实机/发行/提交。

新增生产 State.cs 与正式 Tests/StateFlow 项目；真实主 Update/重试/绑定/Picker.Tick/文本方法构建时提取测试。修复发送、初始化、计时和菜单的跨流程缺口，共 281 项逻辑检查通过。

[实施与验收记录](IMPLEMENTATION_Fixes_2026-11-16.md)。仅同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 修复方案已调研，未实施

用户要求先调研如何修。确定共享按 Room 会话/actor/模式去重、目录就绪自动补发、无关/重复属性过滤、内容刷新与计时分离；保留协议 2、旧配置、默认客机限制和三秒房主过渡。隔离候选 47/47 检查通过，不能当作生产修复或实机通过。

补查本地 Photon 提交 bool 与回显区别、首次目录懒加载、完整属性快照、房主宽限、配置与窗口循环。原型覆盖失败节流、同名新房、空库与自动加载、无关回调、宽限到期、扫描模式切换。

[修复方案与验证边界](RESEARCH_Fixes_2026-11-16.md)。只同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 整体审查完成，问题未修复

确认四类状态流问题：相同 payload 跨房/模式漏发布；目录解析后未重新发布；无关房间属性续时并重建窗口；外部常驻→定时保留无穷截止。现有 184/184 回归仍通过，新增隔离审计使用真实方法体和测试替身，未做实机。生产源码未改，问题尚未修复。

已审主插件、发现/标签生命周期、目录身份与预设、UI、ModConfig 集成。主要缺口在跨流程状态；184 项 helper 回归通过不能覆盖这些缺口。

[完整审查报告](REVIEW_2026-11-16.md)。仅同步本地 memory；本次没有可用的外部 update_memory 接口。

## 2026-11-16 晚安莓统一标注已实施，未部署

用户“开始”已授权实施：食物分类中的单一晚安莓条目覆盖未摘/已摘；未摘锚定果实，隐藏/采摘/更换后旧标签失效，空株不再标记。Ground控制未摘，已摘沿用地面/手持/背包规则；旧itemID或场景token预设兼容，取消勾选清除两者，共享协议不变。184/184逻辑回归、Release 0警告0错误。开发标识0.1.3-dev.20261116.napberry；仅隔离产物，未部署/改cfg/实机/发行。

此条覆盖前一阶段仅方案状态；不将逻辑回归视为实机通过。

[实施与验收记录](IMPLEMENTATION_Napberry_2026-11-16.md)。仅维护本地memory。

## 2026-11-16 晚安莓统一标注可行（仅方案）

用户追问统一标注可行性：可用单一“晚安莓”入口/标签，未摘定位果实，采摘后撤掉植株标签并沿用物品扫描；内部仍分FakeItem/Item两条发现路径，旧物品ID/场景选择应兼容。沿用先调研要求，未改生产代码、配置、构建或部署。此前空株误标缺口仍未修复。

[方案与边界](RESEARCH_Napberry_2026-11-16.md)。

## 2026-11-16 晚安莓植株扫描调研（未改代码）

只选普通晚安莓不会自动扫描植株；需另选自然危险中的“未摘下的晚安莓”。实际扫描NapBerrySpawn植株对象，未检查fakeBerry采摘/激活状态，采摘后若植株仍激活会残留标签。已用3.1源码与sharedassets4资源交叉核查，未改生产代码/配置、未构建部署或实机。该旧缺口未修复，也不在此前143项回归覆盖内。

[调研证据与修复边界](RESEARCH_Napberry_2026-11-16.md)。只维护本地memory。

## 2026-11-16 客机配置与改版整合完成，额外识别仅调研

用户认可的改动已整合到开发源码：客机 RestrictClientPresets 默认 true（本地偏好），本地多预设并集、放置物门控、蘑菇毒/无毒品种及 Clone 识别、地面过滤与标签范围优化。143/143 逻辑回归，当前 PEAK 3.1.0 隔离 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.presets；未部署、未实机、未改发行。

新增72项逻辑检查；182个 resources Item/Backpack 资产中35个相关条目保留证据，不是可获取物品总数。

[实施与额外能力调研](IMPLEMENTATION_PresetsAndTraits_2026-11-16.md)。此条覆盖下方只对比、未授权移植状态；旧记录保留为历史。

## 2026-11-16 新增反编译静态对比

对照 renlei 0.1.3.1 与开发版0.1.3：确认多预设并集、客机自由本地预设、放置物门控、六种蘑菇prefab毒性分组、地面不可交互/点燃信号棒过滤、范围标签优化。版本日志未同步，Clone后缀与距离边缘为待实机风险。仅本地memory更新，详见 [报告](COMPARISON_Renlei_0.1.3.1_2026-11-16.md)。

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

按用户要求沿用 0.1.2 图片/章节/英文排版准备发行/0.1.3；README 新增本版六项更新、范围开关与 40m 回退说明，CHANGELOG 与开发版一致且保留旧历史，manifest 仅改版本。复用已验证 DLL 和旧图标；五文件及版本/hash 校验通过，旧版原样保留，未打 ZIP/上传。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

用户已授权实施，且要求国际化最后统一修。0.1.3/0.1.3.0 已完成植物真实身份与迁移、箱子折叠筛选/单实体标签、当前段/全图范围、国际化及窗口刷新；71 项逻辑回归通过，PEAK 3.0.1 隔离 Release 0 警告 0 错误，已部署原 profile 的 WUYACHIYU-WhereIsThing 子目录。未启动游戏，主客机/自定义地图/中文字体与布局待实机。历史发行与 ZIP 未改。 新增 Tests 回归项目以及 PickupIdentity/Plants/Luggage/SegmentFilter 文件；最后统一语言分支、新配置翻译和打开窗口刷新。

## 2026-10-29 0.1.3 只读调研

核对截图、源码、BetterItemInfoDisplay 0.1.5、当前配置/日志/资源和 DLL 成员。确定 Game 模式“雕像上/背包内/变体数”错误落入中文；官方表含截图物品中文。资源证明 Mandrake_Hidden 显示 Medicinal Root 但指向真实 Mandrake；现有扫描/分组未解析该引用。箱子按掉落池与特殊身份混分，陷阱存在双入口标签路径；无限距离缺段过滤。完整方案及证据：[RESEARCH_0.1.3_2026-10-29.md](RESEARCH_0.1.3_2026-10-29.md)。插件仍 0.1.2，未改代码/配置/版本/发行或部署；当前游戏 DLL 与 3.0.0 审核不同，未做全量新版本兼容验收。

## 2026-09-21 玩家名声明

发行 0.1.2 README Notes 已用英文声明玩家名仅供娱乐，不作为放置者的可靠证据；中途加入可能缺少此前放置物的玩家名。准确语义为“部分目标仅显示物品名称和距离，无玩家名”，不是“仅显示玩家名”。踏板菇/弹力菇/云雾菇依赖本机观察到的唯一近期投掷记录，缺失或歧义时留空；魔豆不显示种植者。CHANGELOG 同步，仅改文档，不改 DLL、不打 ZIP。

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 0.1.2。

## 2026-09-21 发行文件准备完成

当前版本 `0.1.2` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsThing/发行/0.1.2`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：放置物及 owner 识别、玩家放置预设、分类校正、分帧发现和标签优化、20000 排序、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 ModConfig 集成更新

开发/测试版本 `0.1.2`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。补齐可见菜单刷新并限制配置 UI 翻译范围。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.1.2` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。

WhySoLaggy.Tests 实际 23 项通过/0 跳过；四项目构建通过。完整 UI 验收未完成，详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-09-06

## 2026-09-20 Canvas降至20000

- 按用户要求排序32700→20000，保留GraphicRaycaster及交互逻辑；开发CHANGELOG已记录，版本仍0.1.2。
- 普通Release构建0警告0错误，IL验证排序20000；输出2.0.a/BepInEx/plugins/WhereIsThing.dll，程序集0.1.2.0，SHA256 `2ACB0BEC44002415B9EF4A29D149F22E27064C6B3582633E39A52286BFB7F0DE`。
- WIT/TFA共存实机效果待复测；未更新发行目录、未创建ZIP。

## 2026-09-06 ItemSpawnerEnhanced 分类审计与正式测试目录构建

- 新增参考源码 `引用参考代码\反编译\BepInEx\plugins\ItemSpawnerEnhanced`；其 `1.3.0` 自述基线仍为 PEAK `2.1.a`，不能整套当作 2.4.b 分类真值。
- 使用 PEAK 2.4.b `resources.assets` 只读核对：ItemSpawnerEnhanced 审阅的 137 个 prefab 名称及 37 个默认隐藏名称目前仍全部存在。
- WhereIsThing 保留互斥细分类，不改成 Food/Consumable/Equipment/Deployable/Mystical 多标签 UI；在 `ThingTypes.cs` 增加 51 项按 prefab 名精确匹配的分类覆盖，执行顺序为特殊标签、神秘标签、精确覆盖、食物标签、关键词、Misc。
- 主要修正：Jetpack/Rocketpack 归移动装备；Heat Pack、Healing Dart、Healing Puff Shroom 归医疗；神秘变体归神秘物品；EarlyWorm/Beehive 归生物；Honeycomb 归食物；Scorpion 归危险生物；棋子归玩具；三种放置蘑菇、魔豆、检查点旗、便携炉归生存工具；链条发射器/童子军大炮归武器；绳索/岩钉归攀爬装备。
- `Deployable` 仅作分类审计概念，不新增 UI 分类、不等同于可显示 owner；不直接采用 ItemSpawnerEnhanced 的 37 项隐藏策略，不改变玩家名、放置来源、预设或共享协议。
- Release 已成功覆盖到 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`，`0 warnings / 0 errors`；DLL 为 `0.1.2.0`、`158720` 字节、SHA-256 `AC1D98407D52BAA61E1B7B4EF33F5AB4B09CEC168E28D65E019587B048DCEFA9`。
- 本轮仍未更新 `MOD开发\WhereIsThing\发行\1.0.3`，未创建 ZIP；分类结果和既有 owner/标签功能仍需实机回归。

## 2026-09-04 PEAK 2.4.b 兼容修复（待实机验收）

- 当前游戏基线已确认为 `C:\SteamLibrary\steamapps\common\PEAK` 的 `2.4.b`；`Assembly-CSharp.dll` SHA-256 为 `21CBF3A6585A72778A2E5FB007E36CA246002DA5AE8209A6D998673633D82759`。
- 版本已调整为 `0.1.2` / `0.1.2.0`，本轮仍是测试阶段：只编译到现有 r2modman `profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`，没有更新 `发行/1.0.3`，没有新建 ZIP。
- 新增 `WhereIsThingPlugin.PlacedSources.cs`：从物品目录中的 `VineShooter.vinePrefab`、`RopeShooter.ropeAnchorWithRopePref`、`RopeTier.anchorPrefab`、`ShelfShroom/CloudFungus.instantiateOnBreak`、`ClimbingSpikeComponent.hammeredVersionPrefab` 和 `Constructable.constructedPrefab` 建立放置体正向来源索引。
- 绳索必须同时命中已知绳索物品锚点 prefab 与玩家创建的锚点 PhotonView；明确排除 `isHelicopterRope`、`TempleEntranceRope`、`BreakableRopeAnchor`、`PeakSequence` 绳索和 room/system view。
- 锁链发射器必须命中 `VineShooter` 的生成 prefab，且继续排除 `BreakableBridge`，避免海滩桥被识别为锁链发射器。
- 蘑菇 owner 保持纯客户端：订阅 `GlobalEvents.OnItemThrown`，读取游戏已写入的 `Item.lastThrownCharacter`，仅当物品、生成 prefab、2 秒窗口、12 米距离均匹配且候选唯一时显示玩家名；野生、超时或歧义实例保留标签/距离，不显示 owner。
- 场景载入后重新订阅 `OnItemThrown`，因为原版 `GlobalEvents.ResetAllRunEvents()` 会清空事件；切场景同时清理投掷证据和发现缓存。
- 删除按 `rope/piton/climbing` 对象名决定是否重试的旧路径；未分类非 room PhotonView 在 0.05/0.15/0.30/0.50/1.0/2.0 秒有限重试，每帧最多 32 个或 0.25ms。
- 已移除任意父子层级“第一个玩家 PhotonView” owner 回退。构建程序集不包含 `PhotonNetwork.Destroy`、`Player.EmptySlot`、`ConsumeDelayed`、RopeShooter 消耗补丁或 Constructable 清理补丁。
- `dotnet build MOD开发\WhereIsThing\WhereIsThing\WhereIsThing.csproj --configuration Release` 通过，`0 warnings / 0 errors`；profile DLL 版本为 `0.1.2.0`，大小 `156672` 字节，SHA-256 为 `20298B319CC160337D78842165BADBCDC5E9B48D7E9636CE57EFC2415A6E3C55`。
- 尚未完成房主/客户端实机验收；当前不得记为已发布或已全量验证。

## 2026-08-20 发布 0.1.1

- 完成 `发行/0.1.1` 散装发行目录，包含 `WhereIsThing.dll`、`README.md`、`CHANGELOG.md`、`manifest.json` 和 `icon.png`；按用户要求未创建 ZIP。
- 0.1.1 发行说明覆盖护符雕像碎片扫描范围和 ModConfig 安全本地化修复。
- Release 构建通过 `0 warnings / 0 errors`；发行 DLL 为 `0.1.1.0`、`126464` 字节，SHA-256 与 profile DLL 一致。

## 2026-08-19 ModConfig 安全本地化修复

- 移除 WhereIsThing 对 PEAKLib.ModConfig 全局缓存的清空和重新注册，不再调用 `ProcessModEntries`、`LoadModSettings` 或同类私有注册流程，避免切换语言时所有 MOD 的配置项复制。
- 配置项显示名改为按稳定的 `Definition.Section + Definition.Key` 解析；中文只作为界面文本，不写入配置 section/key 或枚举序列化值。
- 启动和语言切换的本地化请求合并为单个延迟协程，只更新 WhereIsThing 自己的描述和当前可见 UI。
- Release 构建通过 `0 warnings / 0 errors`，已部署到 `2.0.a` profile；PlayersInfo、LanternShootZombiesNight 及其他 MOD 仍需按 `memory/common/08_ModConfig本地化与安全集成规范.md` 迁移。

## 2026-08-19 护符雕像范围

- 新增 `ThingLocationScope.Statue`，并在 `Alt+C` 预设窗口的扫描范围行加入 `Statue / 雕像` 复选框；这是扫描范围，不是新的目标类别或新的“雕像碎片”可选目标。
- 当当前预设已选择四个护符中的任意一个时，开启 `Statue` 范围会扫描 `Peak.PropSpawner_AmuletStatues`，识别第 1~4 关场景雕像手中显示但未拾取的护符碎片，并用已选护符的名称显示标签。
- 行李箱逻辑不变：`Luggage` 位仍由已选行李箱类型自动维护，窗口不恢复行李箱范围复选框。
- 本次不修改版本号，仍为 `0.1.0`；`dotnet build MOD开发\WhereIsThing\WhereIsThing.slnx -v:minimal` 通过，`0 warnings / 0 errors`，DLL 已覆盖到 r2modman `2.0.a` profile。

## 2026-08-18 发布完成

- 用户确认 WhereIsThing 已完成发布；本地发行目录已有 `MOD开发/WhereIsThing/发行/0.1.0/icon.png` 和 `wuyachiyu-WhereIsThing-0.1.0.zip`。
- 发布前素材准备记录已经用完，不再作为后续待办或设计约束保留。

## 当前阶段

- 新建 `WhereIsThing` 0.1.0 工程，目标基线为 PEAK 2.1.a。
- 已复查 2.1.a 反编译：`Item.itemID`、`Item.itemState`、`Item.UIData.itemName`、`Item.data`、`BackpackData.itemSlots`、`ItemSlot.prefab` 和 `LocalizedText.GetText` 均可用于本 MOD。
- 已实现 `ThingCatalog`，从 `SingletonAsset<ItemDatabase>.Instance.itemLookup` 动态读取物品定义，按 ID 去重。
- 已实现 `ThingCatalog` 的显示名合并：同名物品组保存多个 itemID，选择一次覆盖所有变体，并显示变体数。
- 兼容旧选择配置：目录首次加载时，旧配置中命中的单个变体会扩展为整个同名组。
- 已实现 `WhereIsThingPlugin`：默认 `C` 扫描，常驻/计时显示，0.5 秒补扫一次，清理已拾取或失效目标；支持地面、手持、背包、雕像护符碎片范围，行李箱范围由已选行李箱类型自动启用。
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
- `LocationScopes` 是可组合的标志值，主要通过 `Alt+C` 窗口里的地面、手持、背包复选框修改；行李箱位由已选行李箱类型自动维护，ModConfig 说明中明确这一点。
- 选择 ID、旧行李箱布尔值和行李箱类型字符串标明为窗口维护/兼容配置，避免用户误以为需要手填。
- 监听 `LocalizedText.OnLangugageChanged` 更新说明并刷新 ModConfig 界面文字；旧版曾刷新 ModConfig 缓存，现已按 2026-08-19 安全修复移除该行为。Release 构建通过 `0 warnings / 0 errors`，DLL 已覆盖到 r2modman `2.0.a` profile。

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

- 场景目录新增“危险生物”“自然危险”“机关陷阱”分类：危险生物为森蕈僵尸、甲虫、蝎子、蜘蛛、蜂群、Scoutmaster；自然危险为风滚草、GhostBall、地刺、蚁狮、捕蝇草、龙卷风和未摘下的晚安莓；机关陷阱为箭矢发射器、移动锯刃、滚刺机关、摆斧机关。
- 甲虫和蝎子共用 `MobManager.instance.mobs` 的一次遍历；其余目标仅在用户勾选对应类型时，以其 2.1.a 明确组件类型在现有 0.5 秒刷新周期扫描。
- 目标类型已加入 Alt+C 目录、搜索、仅显示已选、配置持久化和 ModConfig 中英文枚举显示；场景目标仍不受地面/手持/背包/行李箱范围开关限制。
- 用户明确要求本批不加入青蛙舌，故没有枚举、窗口项目或扫描分支；未加入全局环境状态、易刷屏静态地形，以及已由普通物品追踪覆盖的危险物。
- Release 构建通过：`0 warnings / 0 errors`；`WhereIsThing.dll` 已输出至 r2modman `2.0.a` profile，大小 `75776` 字节。

## 2026-08-16 同名物品与场景目标去重

- 修复蝎子等对象在窗口中同时出现“物品组”和“危险生物”两个同名入口的问题；目录现在会把英文游戏名相同的物品组与场景目标合并成一个联合条目，并使用场景行为分类。
- 联合条目勾选/取消时同步更新 itemID 和 `ThingSceneTargetType`；旧配置中任一侧已选都会在目录加载时补齐另一侧并持久化。
- 地面 `MobItem` 在对应场景目标已选时跳过普通物品标签，由 `MobManager` 统一显示；手持和背包形态仍走原物品范围逻辑，解决同一只蝎子叠加两层文字的问题。
- Release 构建通过：`0 warnings / 0 errors`，DLL 已覆盖到 r2modman `2.0.a` profile。

## 2026-08-16 行李箱范围自动化与杂项审计

- 移除选择窗口中的“Luggage / 行李箱”范围复选框，保留 Ground、Held、Backpack 三项手动范围。
- 行李箱扫描入口和标签有效性不再读取手动 `LocationScopes.Luggage` 位；只要 `SelectedLuggageTypes` 非空就扫描，全部取消后自动停止。
- 应用窗口配置和加载旧配置时都会自动同步 `LocationScopes.Luggage` 位；该枚举值及旧 `SelectedLuggage` 配置仍保留，避免旧配置失效，但不再作为新的手动入口。
- ModConfig 的 `LocationScopes` 中英文说明已改为提示：行李箱范围由已选行李箱类型自动控制。
- 使用 UnityPy 读取 2.1.a `resources.assets` 和 `SerializedTermsData`，发现当前目录按英文显示名合并后有 33 个 `Misc` 组（194 个物品 ID）；已将完整清单和建议写入 `README.md`，当前未未经用户确认直接重分类。
- Release 构建通过：`0 warnings / 0 errors`；DLL `76800` 字节，已覆盖 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`。

## 2026-08-16 A1+A2+B1+B2+B3

- 森蕈僵尸标签不再使用僵尸根节点位置，改为通过同一 GameObject 上的 `Character.Center` 提供躯干坐标。
- 普通行李箱标签改用 `Luggage.Center()` 的 bounds 中心；行李箱分类通过反射调用游戏 `Spawner.GetSpawnPool()`，正确处理 `LuggageBig`/`LuggageSmall` 的高度池，而不是直接读取始终为 `None` 的序列化 `spawnPool`。
- 行李箱额外按 prefab 名称识别 Ancient、Cursed、Clown，保留 RespawnChest 类型判断；无法识别的运行时对象仍归入 Other，避免漏标。
- 分类新增“生存工具”，并完成 B1/B2/B3：早起虫儿、生物蜂巢、医疗、武器、棋类/运动、食物/菌类和生存工具均脱离 Misc；AK 使用完整名称判断，避免与 Snake 的字串重叠。
- 2.1.a 资源级回归：194 个物品 ID，分类后 `Misc=0`，同名变体仍保持合并。
- Release 构建通过：`0 warnings / 0 errors`；DLL 已覆盖到 `2.0.a` profile。

## 2026-08-16 晚安莓目标名称修正

- `NapberryHypnoOrb` 的中文显示名改为“未摘下的晚安莓”，英文改为 `Unpicked Napberry`，名称依据 2.1.a 本地化表中的 `NAME_NAPBERRY = NAPBERRY / 晚安莓`。
- 确认它与 `GhostBall` 是两个独立对象：前者由 `OrbThatMakesYouSleepy` 表示，后者由 `Peak.GhostBall`/`GhostBallSpawner` 表示；英文 `Ghost Ball` 不用于晚安莓目标，避免两个自然危险入口混淆。

## 尚未完成

- 尚未在 2.1.a 实机确认物品数据库加载时机、标签遮挡、背包内容位置和大量标签性能。
- 尚未确认所有物品 prefab 的 `itemState` / `data` 生命周期，尤其是手持和嵌套背包情况。
- 尚未实机确认动态目标在多人非主机端的管理器列表同步、钟塔标签的视觉锚点，以及同时显示多只风滚草时的性能。
- 暂未接入原版 `MenuWindow` 类型，因为其 `Open`/`Close` 生命周期有 internal 边界；当前窗口保持独立 Overlay，并手动管理鼠标状态。
