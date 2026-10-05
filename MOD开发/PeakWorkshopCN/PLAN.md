# PeakWorkshopCN 0.1.0 实施计划

日期：2026-10-04。依据：[RESEARCH.md](RESEARCH.md)。

**当前状态（2026-10-05）：用户后续授权“先执行，我看看效果”，第一轮补齐已实现、构建并部署；41 项测试通过，Unity 实机待验收。最新实现以 [IMPLEMENTATION.md](IMPLEMENTATION.md) 为准。下文保留初始计划及完整验收目标，不代表全部完成。**

## 1. 目标与范围

新增独立中文补丁 PeakWorkshopCN `0.1.0`，与 PeakWorkshop `0.2.0` 一同安装，以 PEAK `2.6.a` 为本轮参考。

包含原 MOD 自带的菜单、按钮、说明、悬浮提示、编辑器标签、预设说明、分类名称、导入/构建/联机状态、固定报错外层文字、通行证交互提示及配置描述。每个功能页都建立覆盖清单，不能用“Tr 补丁已安装”代表全部中文化。

玩家/作者输入的名称、介绍、对话、聊天、许可证内容、文件名及服务器自由内容保持原文；它们外围的固定标签和内置提示翻译。资源名、ID、配置键值、序列化字段、RPC、Bridge 语言代码和 API 参数原样保留。翻译后的显示值不写回地图或上传内容。

已实现默认启用简体中文显示及独立开关，原德英选择继续决定回退文本和数字格式；中文开关独立于原 `Editor/Language`，不把 zh 写入原配置，也不扩展原 Toggle 或改 Bridge 的 lang。本版不额外承诺繁体译文。

## 2. 工程与依赖

- 源码目录：`MOD开发/PeakWorkshopCN/PeakWorkshopCN/`，SDK 风格项目；实际目标为 `net472`，与现有工程保持一致并解决 Harmony 的 mscorlib Label 引用兼容；不复制反编译工程。
- 程序集与插件名：`PeakWorkshopCN`；版本与 Assembly/FileVersion 为 `0.1.0` / `0.1.0.0`。
- 拟用插件 GUID：`com.wuyachiyu.peakworkshopcn`，实施前检查不存在冲突。
- BepInDependency：原 GUID `com.peakcode.peakworkshop` 的 HardDependency。仅针对核验的 0.2.0 基线宣称兼容；硬依赖加载顺序不等于所有后续版本自动兼容。
- 原内部类型通过 `AccessTools.TypeByName` 和明确参数签名解析，不对 internal 类型强行编译引用。一般不需要引用 PeakWorkshop.dll；不分发原 DLL、bundle 或功能源码。
- BepInEx/0Harmony 引用 `测试环境/BepInEx/core/`；游戏和 Unity 引用 `C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/Managed/`；Private=false，禁止复制依赖到输出。
- 唯一指定 DLL 输出：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`。profile 目录名 2.0.a 不表示本轮游戏参考版本降回 2.0.a。
- ModConfig 可选；没有它时中文 UI 仍正常工作。尚不创建 manifest 或发行包；未来发行明确声明 PeakCode-PeakWorkshop 依赖，并遵守英文文案/135 字符规范。

建议模块：Plugin 生命周期、补丁目标解析、TranslationCatalog、动态文本规则、语言/缓存协调、ImGuiFontHelper、TMP FontHelper、ConfigLocalization 和各类 Patches。只有实际重复复杂度出现时再抽公共接口。

## 3. 翻译目录与补丁选择

### 3.1 固定文本

`PeakMapForge.Loc.Tr(string de, string en)` 是主要补丁入口，优先 postfix 返回中文，不改变传入参数和原方法语言选择。

词表按审核过的德英文本对建立精确映射，记录源码位置、界面用途、来源和覆盖状态。不能只用短英文单词在全屏搜索替换。保留 `("de", "en")`、机器代码、路径、数字单位和无法确认属于固定 UI 的条目。

实施时用 C# 语法树等结构化方式整理字面量、常量数组、插值和变量调用来源，容忍反编译语法错误并标注无法解析的范围。输出去重条目数和待审清单；不用 4,979 当成最终词条数量。运行时只加载已审核资源，不读取源码或做在线翻译。

对传入作者/服务器自由文本的 Tr 调用，先追溯来源。若词条碰撞可能导致作者文本被译，应在对应调用点保留原分支，或仅在已确认内置调用点接入中文助手；不能只凭内容恰好命中词典断言安全。

### 3.2 动态文本

分为三类：完整固定文本、已拼接/格式化的内置模板、任意外部内容。动态模板需编号，保留姓名、数量、百分比、距离、时长、坐标、路径及富文本片段。

优先在明确的显示生成方法做参数/结果补丁；只有模板已混在大型绘制方法内部时，才使用该方法的有限 transpiler，把特定 Tr 调用改为携带来源的中文格式化助手。对字面量、调用位置、方法签名和匹配数量做校验，匹配不符跳过并记录兼容告警，不能按 IL 数字偏移硬改。

少量格式稳定的字符串可使用锚定模板匹配，但必须限定来源、参数边界、长度和执行成本。禁止逐帧尝试全部正则、用一个宽泛匹配器替换任意文字，或缓存无限种带玩家名/数值的结果。

例：`{name} wins!` -> `{name} 获胜！`，`{count} in the maps folder` -> `地图文件夹中有 {count} 张地图`。捕获值原样保留，单复数映射到同一中文模板；不改变数字解析文化。

### 3.3 绕过 Tr 的显示方法

| 区域 | 候选入口 | 处理要求 |
|---|---|---|
| 资源分类 | `PrefabCategories.Display` | 返回中文显示；保留 Of、内部分类及 DisplayIn 的生成/工具用途 |
| 地图分类/难度 | `WorkshopLibrary.DisplayCategory/DisplayDifficulty` | 已知内置值映射中文，未知作者分类原文回退 |
| 素材分类 | `WorkshopPanel.DisplayAssetCategory` | 只改显示结果，不改 CategoryColor 的原值 |
| 地形/危险/预设 | `HazardCatalog.Display/DisplayBiome`、`ScatterPresets.BiomeLabel`、`PlacementSetup.Label` 等 | 每个候选先检查调用者，禁止顺带改输出结构或存储值 |
| 音效/特效 | `ItemSounds.Entry.Label`、ForgeVfx、EditorUi 直接 En/De 选择 | 内置显示翻译，音效/VFX ID 不变 |
| 动态运动说明 | `ForgeMotion.Describe` 等 | 显示副本中文化，不修改物理或 MotionSpec |
| 可见诊断 | Editor/Workshop/Load 具体报告显示方法 | 翻译固定外层；原异常内容、BuildReport 列表和 Bridge warnings 保留 |
| 作者 NPC 内容 | `NpcDialogUi.Text`、ForgeInteractUi 等 | 原作者内容保留，只翻译内置交互标签 |

这张表是候选注册清单，不表示所有方法均应直接 postfix。特别是 DisplayIn 还用于生成预设文字；优先外围显示入口。对 Edit/Save/Filter 中使用显示文字做逻辑判断的情况，必须先以原值执行逻辑，仅在绘制和测量边界译文，防止中文筛选后写入中文 ID。

不补丁 Unity GUI.Label/GUILayout.Button/TMP_Text.text 等全局底层方法。不全屏扫描并按文字改写。

## 4. 字体与布局

### IMGUI

先做一个机场页面和一个编辑器说明块的字体验证，使用游戏中经过汉字覆盖验证的动态 Font；不足时尝试系统 Microsoft YaHei/SimHei/SimSun 动态 Font，检查创建和样本文字实际字形。本机有字体文件不等于验证通过。

在 EditorUi.FindFont/Styles、TeamUi.Style、WorkshopPanel.FindFonts/St/_fieldStyle、LaunchPanel.Styles 等 Workshop 专属入口更新。字号、颜色和布局沿用原设计；测量和绘制使用同一字体与译文。不要只在最后 GUI.Label 绘制时换字体，否则自动换行高度会错误。

更新已经建立的 GUIStyle、输入框样式和影子样式；备份并在禁用时恢复。禁止每帧全场查字体、修改 Unity 全局 GUI.skin 或整体重建原 Styles 中的纹理/图标。

### TMP

使用现有项目 FontHelper 模式获取中文能力，按 2.6.a 实际字体/fallback 检查是否覆盖本词表。分别处理 LoadOverlay.FontFor/NewText 和 MenuSignRig.MakeText/已创建的 Workshop 文本。

必要时创建仅供本插件持有的字体副本并添加游戏中文 fallback，保留标题原字形和原布局；不写共享字体全局 fallback，不自己烘焙图集。FontFor 改变时同步其材质引用与 Glyphs 缓存，避免字体/材质不匹配。

界面验收覆盖长中文说明、富文本、标点、数字及英文混排，1920x1080、1280x720 和原 UI 缩放选项。仅针对译文导致的溢出调整已定位控件的换行/可用高度，不重做 UI。

## 5. 配置与游戏词条

原 MOD HardDependency 已完成配置绑定。补丁启动后取得原插件 ConfigFile，对该实例的 section/key 建立词表，更新已有 ConfigDescription 的描述；新增绑定仅在原 MOD 生命周期的已定位入口中补齐。优先避开全局 ConfigFile.Bind hook。

先备份原描述，验证 Harmony AccessTools/Traverse 能安全更新该对象的 backing field；只修改非共享的目标描述，共享 ConfigDescription.Empty 必须特殊处理或跳过。失败保留原文并报告，不改变值、类型、Tags 或 AcceptableValues。补丁禁用时恢复原描述；本轮范围是运行时配置说明，不为更新 cfg 注释主动触发保存。

若有 ModConfig：复用现有安全适配器模式，仅按这个 ConfigFile/精确文件名 + section/key 翻译名称和说明；所有显示选项保留原 Value。反射入口和公共组件只按真实安装版本解析。不调用 RefreshCache/ProcessModEntries，不重新注册全部 MOD。

WorkshopPass 只允许 4 个固定 key：NAME_WORKSHOPPASS、WORKSHOPPASS_OPEN、WORKSHOPPASS_VIEW、WORKSHOPPASS_LOOK。首选明确白名单的 `LocalizedText.GetText` 返回补丁，核对调用使用的重载；这样不写共享本地化表，也不受 CustomItems 重注入覆写。

GetText 是游戏全局入口，必须先以 key 白名单和启用状态快速拒绝无关请求，保留显式语言重载的语义；初版建议只覆盖使用当前语言的调用，作者物品 NAME_/DESC_ 不按前缀扩展。若后续选中文槽注入方案，需要另行实现 AddText/Inject/LoadMainTable 重注入顺序和原表内容恢复，不同时混用两种方案。

## 6. 生命周期与缓存

1. Awake：绑定本补丁配置、核验原插件版本/程序集/方法签名，安装各模块补丁；核心 Tr 不存在则停止启用中文功能并说明原因，不继续半初始化。
2. 启动后：翻译原已有配置说明；清点启动前已经构造的内置标签/静态表，按白名单转换显示副本或改显示 getter。不得重跑原 Awake、Boot、静态构造或地图加载。
3. 游戏 UI 就绪：首次获取 IMGUI/TMP 字体，刷新当前已创建的 Workshop 自有样式/文本；不依赖 Awake 中 GUIManager 必然存在。
4. 游戏语言/补丁开关/场景变化：合并刷新请求。对 `LocalizedText.OnLangugageChanged` 使用 Action 字段的组合/移除方式，并保留其他监听。原 `Loc.Changed` 作为辅助信号，非唯一信号。
5. 刷新：显式处理 MarkerRef.Label 的 bool 语言缓存、PaletteDirty、Loc.Version 依赖列表、LoadOverlay 文本及样式缓存。可调用原 Bump 作通知，但不能认为通知本身覆盖所有缓存；逐项确定可安全失效的字段。
6. 恢复原显示/卸载：先停止刷新和收集，恢复描述、字体与已保存的固定显示缓存，再 UnpatchSelf/退订。只有本插件创建的字体副本等资源可销毁，不销毁游戏字体或其他插件资源。

缺失翻译仅 Debug 级计数或可选本地报告；设置上限和去重，不逐帧刷 Info，不记录玩家聊天、token、任意路径或作者长介绍。开发期覆盖统计不默认新增独立 Collector DLL。

## 7. 分阶段实施与交付

| 阶段 | 后续动作 | 达标条件 |
|---|---|---|
| A | 建立工程、依赖/签名探测、字体最小验证和翻译目录 | 原 DLL 未改；一个 Workshop 页和一个编辑器块真实显示中文，证据明确 |
| B | 完成固定词条、所有功能区域主入口和直接显示分支 | 词条来源/审核/遗漏可追踪；按钮操作、资源筛选与原行为一致 |
| C | 动态模板、原配置说明、通行证、已有缓存刷新 | 姓名/数字/路径保持原值；禁用恢复，重新开菜单/换语言不残留旧字 |
| D | 功能页遍历和布局/联机/数据回归 | 覆盖矩阵通过，原地图可读写、未启用补丁的客机可联机，无格式/协议变化 |
| E | 0.1.0 构建及测试部署 | 记录 DLL 版本/hash/输出路径；用户重启游戏后验收，发行包另行准备 |

构建已直接输出用户指定 profile。若游戏锁定 DLL，先报告并等待关闭游戏，不结束游戏进程或换输出路径绕过。试用交付已完成；阶段 A 的实机条件及 B-D 完整覆盖/回归仍待验收。

## 8. 验收矩阵

| 项目 | 必查场景/结果 |
|---|---|
| 机场/工坊 | 入口、地图/素材/物品/NPC列表、详情、搜索、筛选、导入、发布表单固定标签、长说明 |
| 编辑器 | 顶栏、帮助、场景树、调色板、属性、地形/散布、机关/危险、物品效果、声音/模型、NPC/队伍/天气等功能页 |
| 状态与错误 | 加载、下载、构建、等待主机/客机、失败重试、缺文件、Blender不可用；外层中文，具体数据/异常保留 |
| 动态数据 | 0/1/多项、负数、小数、百分比、距离、时长、长姓名与路径、富文本；参数不丢失、不重复 |
| 配置 | 本 MOD说明/可选菜单中文，其他 MOD描述不变；69行绑定补齐为实际section/key清单并校对范围/Tags/Value |
| 模式与缓存 | 原 auto/en/de 都能工作；中文开关至少5轮、重新开菜单、机场/山上/退出重进，原语言设置不变 |
| 字体/布局 | IMGUI和TMP均非方块；长段落、混排、输入法及原滚动区无截断/覆盖；开关后恢复原字体 |
| 身份/用户内容 | 中文分类筛选仍选中原ID；用户名称恰好为 Save/Other 等词也不被误译；NPC/地图/账号/聊天原文保持 |
| 互操作 | 中英文两端联机、加入/退出、Host与Client；不额外发RPC，不改Bridge lang等字段，不改变地图内容哈希（只读流程） |
| 原文件 | 原 DLL哈希不变；测试原文件用副本，不发布、不自动迁移数据、不改在线内容 |

自动测试重点是精确匹配与回退、参数保真/来源隔离、动态规则边界与成本、配置归属、委托订退和开关恢复；字体、布局、缓存和实际多人流程必须实机验证。静态词条完整率不能代替实机覆盖。

## 9. 当前交付与剩余不确定项

已交付源码、工程、指定 profile 的试用 DLL、41 项自动测试与静态审计工具；实现记录及 hash 见 IMPLEMENTATION.md。没有发行目录、ZIP、上传或实机通过声明。

尚需完成：余下去重词条、更多动态调用及直接语言分支、启动缓存清单、IMGUI/TMP 实际渲染与 ModConfig 可见界面验收。试用实现采用限定调用点替换而非全局 Loc.Tr postfix，配置采用限定实例 getter 而非修改 backing field。不能在实机和覆盖矩阵完成前承诺 100% 中文覆盖或 2.6.a 实机兼容。
