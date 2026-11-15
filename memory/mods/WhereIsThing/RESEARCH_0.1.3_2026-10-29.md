# WhereIsThing 0.1.3 更新前调研

日期：2026-10-29（Asia/Shanghai；PowerShell Get-Date 已核对）。

## 21:36 补充：3.0.1 反编译已核对

用户已提供 `引用参考代码/反编译/3.0.1/Assembly-CSharp/`，原 3.0.0 现位于 `引用参考代码/反编译/作废/3.0.0/Assembly-CSharp/`。后续 WhereIsThing 0.1.3 以 3.0.1 为当前反编译依据。

已对照 12 个与本次需求直接相关的文件：Item、FakeItem、FakeItemManager、LocalizedText、Luggage、Spawner、MapHandler、Campfire、RespawnChest、Peak/TrickLuggage、Peak/PhotonCleanupHelper、Peak/PropSpawner_AmuletStatues。忽略空白/整行注释后的源码均一致，因此本报告关于名称语言、真实拾取身份、箱子与关卡过滤的结论保持不变。当前安装 DLL 复核仍为 1647616 bytes、SHA256 E526A5234EA2289EA55297FBD6FB7626C75E86E6A0A26A6B87D82ED8F44CD168。

本次补充仅核对相关反编译并同步 memory，不是全游戏/全 MOD 兼容审核；仍遵守“先调研，不改代码”，插件保持 0.1.2，未构建或部署。下方最初调研时“只有 3.0.0 参考”的时间背景由本节补齐。

## 范围与状态

用户要求：下一版 0.1.3；重查截图中的国际化；参考 LengSword-BetterItemInfoDisplay-0.1.5 识别地形中的曼德拉草/药用根茎；提出箱子整理方案；解决无限距离时上一段/下一段仍显示。用户明确“先调研，不改代码”。

本轮只读源码、反编译、资源、配置和日志，保存调研与 memory。没有修改插件源码、工程、版本、配置、安装 DLL 或发行文件；没有构建、部署、启动游戏或实机验收。**0.1.3 是目标，当前仍为 0.1.2。**

## 当前实物基线

- 源码：MOD开发/WhereIsThing/WhereIsThing；PluginVersion、csproj Version 均 0.1.2，AssemblyVersion/FileVersion 为 0.1.2.0。
- 当前安装 DLL 实际在 profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhereIsThing/WhereIsThing.dll，164352 bytes，SHA256 CF1C9043F74E843032CB65F972137EA135297D690EA9E397929977AAB889BF63。旧 FILES/default OutputPath 指向 plugins 根目录，但根目录当前没有该 DLL；后续部署避免制造两份插件。
- 当前读取的配置为 NameLanguage=Game、MaxDistance=0、ShowOffscreenDirection=true；不宣称该配置一定等于截图拍摄时设置。
- 2026-10-29 的 LogOutput.log 含 WhereIsThing 0.1.2 加载记录和 TFA 启动时 game language en。该日志是旁证，不代表截图时所有语言状态已被捕获。
- 本机 Assembly-CSharp.dll 已不是 2026-10-23 审核的 3.0.0 DLL：当前 1647616 bytes，SHA256 E526A5234EA2289EA55297FBD6FB7626C75E86E6A0A26A6B87D82ED8F44CD168，文件时间 2026-10-24 00:10:03。globalgamemanagers 的 PlayerSettings.bundleVersion 读出 `3.0.1--logFile`（保留原始值）；不能把目录名 3.0.0 当作当前安装完全一致的证明。
- 已用 Mono.Cecil 只读复核当前 DLL 的 Item.isSecretlyOtherItemPrefab、FakeItem.realItemPrefab、Item.GetName、Item.RequestPickup、LocalizedText.GetText、MapHandler.CurrentSegmentNumber/GetCurrentSegment、Spawner.SpawnItems/GetSpawnPool 等关键成员。未做全量 3.0.1 兼容审核，旧构建结论不自动沿用。

## 1. 版本计划

实施时同步 WhereIsThingPlugin.cs:29、WhereIsThing.csproj:6、Properties/AssemblyInfo.cs:9-10，目标分别 0.1.3 / 0.1.3.0。本轮不提前改版本。发行目录、manifest、README/CHANGELOG 属于后续明确发行步骤，保留历史 0.1.2 与既有 ZIP。

## 2. 国际化：已确定的逻辑缺陷

截图显示 SCOUT'S GENEROSITY + 雕像上、SHELF FUNGUS、WARP FUNGUS。

- ThingTypes.cs:449-477：Game 模式调用 Item.GetName()，按游戏语言取名。
- ThingTypes.cs:885-892：背包/雕像后缀只判断是否显式 English；Game 直接落入中文。
- ThingTypes.cs:193-201：变体计数同样把 Game 当成中文。
- 因此 Game + 英文游戏会稳定产生英文物品名配中文后缀。已有 UseChineseNames(language) 能正确处理 Game，但上述三处没有使用。
- 当前 resources.assets 的 SerializedTermsData 与 Localized_Text 均有完整条目：NAME_SHELF FUNGUS=踏板菇、NAME_WARP FUNGUS=曲迁菇、NAME_AMULET_CLONE=童军的盛情、NAME_MEDICINAL ROOT=药用根茎、NAME_MANDRAKE=曼德拉草。不是这些物品没有中文翻译；字体也不会把中文词自动换成英文。
- 外壳 UI/ModConfig 使用游戏语言，物品名称又有独立 NameLanguage；强制中文名但英文菜单可能是现有设计，而不是缺词。预设摘要 ThingPresets.cs:365 固定请求 Game，也需在统一语言策略时检查。

建议：统一“最终名称语言”解析，名称、后缀、变体数、分类、箱子/场景目标状态使用一致规则；保留 Game/English/SimplifiedChinese 的配置原值。Game 尊重实际游戏语言，不自动强制中文。若用户希望英文游戏中显示中文名称，使用明确中文覆盖。所有语言更新同时刷新已有标签、搜索/分类、可见窗口和字体。Game 名称路径也应校验空值/LOC 占位并提供可诊断回退，不只校验显式语言分支。

待验：截图当时的游戏语言/配置无法从图片单独确定；需要 Game+中/英/繁、强制英文/简中、开着标签切语言、多 MOD 共存的实机矩阵。

## 3. 地形植物：真实拾取身份与显示载体分开

参考文件：引用参考代码/反编译/BepInEx/plugins/LengSword-BetterItemInfoDisplay-0.1.5/BetterItemInfoDisplay/ItemInfoController.cs:151-181。该 MOD 做瞄准/手持信息展示，不是全地图定位器。

参考有两条独立解析路径：

1. FakeItem → realItemPrefab，作为静态预览。
2. Item → isSecretlyOtherItemPrefab（若有），同时保留原 Item 作为实例状态来源。

本机 resources.assets 的只读资源核验发现：

- Mandrake_Hidden 挂的是 Item，UIData.itemName 为 Medicinal Root，isSecretlyOtherItemPrefab 指向 Mandrake。
- Mandrake 的 UIData.itemName 为 Mandrake，真实药用根茎 MedicinalRoot 为 Medicinal Root，两者重定向为空。
- 当前 Item.RequestPickup 按 isSecretlyOtherItemPrefab.itemID（若存在）放入物品栏，证明该引用是拾取身份，不是仅供展示的猜测。
- WhereIsThing 当前 SyncNetworkItem（Discovery.cs:360 起）按载体 item.itemID 和载体名字筛选；ThingTypes.Load/GetMergeKey 按英文显示名分组。隐藏曼德拉草与真正药用根茎存在被同名归组/误显示的路径，不能只加 FakeItem 扫描就宣称修好。
- WhereIsThing 的 FakeItem 读取目前限于雕像碎片（SceneDiscovery.cs:243 起）。本轮没有完整解包约 2GB/包的所有地图资源，不能声称所有地形植物形态均是 FakeItem 或已逐关验证。

推荐方案：

- 新增统一的只读“拾取身份解析”，先按组件的真实引用解析；有跳转的 Item 取真正拾取物，无跳转保留原 Item；FakeItem 取 realItemPrefab。
- 名称/分类/选择按真实拾取身份；标签位置始终使用地形实例，不能把 prefab 的位置当现场位置。
- 曼德拉草与药用根茎各一个独立选择项，未拔/已拔归同一真实类型。识别仅限用户选中的这两类，避免无意扩展到整张地图所有 FakeItem、护符或果实。
- 地形形态可标注“未采摘/在地里”；只有能确认地形状态时才加，不按名字或“未被拿起”推断所有地面 Item 都在地里。
- 遵守 Ground 范围；采摘/失活/pickedUp 时删除旧标签，再由普通 Item/手持范围接管，按实际实例去重。
- 延用分帧发现/缓存；禁止每帧全图 FindObjects，也不触发采摘、生成、销毁或额外 RPC。
- 修正分组会影响旧“药用根茎”选择及生存预设，需要显式迁移，避免旧合并选择静默扩张/丢失。

## 4. 箱子：按玩家能理解的种类分组

确认的混乱来源：

- 当前 ThingLuggageType 有 14 类，混合复活箱、场景掉落池、攀登/远古池、诅咒/小丑等语义（ThingTypes.cs:49、262）。
- 分类使用 GetEffectiveLuggageSpawnPool→Spawner.GetSpawnPool；后者会随位置 z 和当前 biome 选择掉落池（Spawner.cs:275-288）。这不是稳定外形/功能身份；多池命中又按固定顺序取第一个。
- 选择窗口用自定义细分类名，世界标签用 luggage.GetName()（ThingTypes.cs:625、729），用户勾选名与看到的名字可能不一致。
- 普通箱子用 luggage:实例ID，陷阱箱又作为 scene:TrapChest:实例ID 建标签，两个入口无同一实体归并。两类同时选中时存在双标签路径；陷阱分支只判 active/选中，不判箱子已打开（SceneDiscovery.cs:10、180；Plugin.cs:821）。这是静态代码路径结论，未实机确认某一箱子已重复。

建议 UI：默认只展示“普通行李箱、远古箱、诅咒箱、小丑箱、陷阱箱”；“复活雕像”单列，不与普通行李箱混合。地区/掉落池放入可展开高级筛选，未知箱子保留通用入口。具体术语优先游戏官方名称。

识别优先级按组件/明确标记：复活、陷阱、诅咒等特殊身份优先，普通池只作辅助；同一个箱子一个实体键和一个主标签，名称+特殊属性/风险提示在同条显示。原有“机关陷阱”入口可联动相同实体选择并去重，不擅自删功能。

打开后的“箱子定位”和仍在生效的“危险状态”要分开：已打开不再当未开箱指引；若陷阱仍有效，风险提示按实际组件状态保留，不能一起强删。

迁移建议保留旧枚举 token 与已分享预设；高级筛选保存旧细分能力，新分组勾选映射到内部集合，支持部分选中。新类型、陷阱入口归并及跨版本共享行为需单独测试，不直接把旧枚举删除或重排。此方案是建议，尚未实施，也未记为用户已选定。

## 5. 无限距离与关卡范围是两回事

- ThingLabel.cs:122-170 仅检查距离、屏幕内外；MaxDistance<=0 时距离始终放行。屏幕外只显示距离与箭头，因此截图底部一堆数字也可能是这个正常呈现分支，不等于名字翻译丢失。
- Plugin.cs:432 只在 Unity SceneLoaded 清理，关卡段切换并不必然换 scene。全局 PhotonView/场景扫描还会再次发现未销毁的旧目标；单纯 ClearLabels 不足。
- MapHandler.GoToSegment 在同一地图切换 segment。3.0.0 参考源码还保留玩家 60m 内的一部分旧网络物品，不能把“旧段目标存在”等同于游戏删除失败。
- 截图 Current Level: Level_4 是地图 scene 标识；ClimbInfoTracker.cs:441-452 来自 SceneManager.GetActiveScene().name，不等于当前是第 4 个地形段，也不能据此认定无限攀登模式。

建议增加独立扫描范围：“当前关卡（默认建议，包含当前入口营地）/整张地图”。无限距离只表示该范围内不限米数。

实现分层：

1. 当前段读取 MapHandler 的运行时状态，等待地图初始化；静态箱子、雕像、地形植物优先映射实际 segmentParent/segmentCampfire，处理变体，不按海滩/丛林名字硬编码。
2. 动态 Item/玩家放置物不保证挂在段根下面。Spawner.SpawnItems 使用 Photon 实例化，必须结合可用生成来源、实时空间范围/状态判断；手持和带到下一段的背包/道具要跟随当前位置，不能永久锁死出生段。
3. “离哪个营火最近”不是精确关卡边界；Campfire.GetClosestCampfireNextSegment 只是最近距离。高低重叠、跨界平台、自定义地图要有未知归属与回退策略，不能承诺简单距离法全准确。
4. 在发现/标签资格阶段过滤，并在段切换、目标移动或范围变更时刷新归属；显示层统一隐藏整条（含箭头/距离）。旧目标从可见集合移出后不能被下一次全图扫描无条件加回。
5. 原版地图先做可验证实现；PLL/自定义地图、TFA 如确实改变段结构则按运行时根/提供的结构适配，当前没有证明完整兼容。

待验：普通距离/无限距离，进段/返回/载入/复活/晚加入，上一段和下一段、营地交界、携带跨段后丢下、雕像/FakeItem/箱子/玩家放置物、房主与客机、自定义地图。无实机结果前不写“已解决”。

## 建议实施顺序与边界

1. 用当前安装 DLL 建立只读基线，再同步开发版本到 0.1.3。
2. 修语言解析并覆盖三个已确定分支。
3. 统一两种真实拾取身份与选择迁移，再补地形目标。
4. 增加关卡范围过滤，避免无限距离把全图候选全部显示。
5. 按确认的箱子 UI 方案整理分组/去重，保留旧选择与预设。
6. 适当的纯逻辑回归、隔离 Release 构建、然后按用户后续安排实机；准备发行另行处理。

本轮结论基于本地一手代码/资源，不包含线上版本推断。未声称写入外部 memory 服务；当前环境无专用 update_memory 工具，本地 Markdown 为接续依据。

## 收尾校验

Git 检查确认开发区无差异，安装 DLL SHA256 仍为 CF1C9043…BF63，仅 memory 有本轮修改；git diff --check 无空白错误。包含外部 cfg 的汇总哈希前后不同，后续核查发现该 cfg 在 2026-10-29 21:24:38 被外部更新；本轮未向 cfg 写入，未恢复或覆盖。复读 NameLanguage=Game、MaxDistance=0、ShowOffscreenDirection=true 仍一致，其余配置变化未完整比对，因此不能声称整个 profile 前后字节完全不变。
