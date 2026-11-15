# WhereIsThing 接续摘要

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

用户确认版本 0.1.3（1.0.3 为笔误）。发行/0.1.3 五件套已完成：英文文案保留 0.1.2 图片、章节、历史和依赖，新增本版功能及实机待验说明；DLL 180224 bytes / 0.1.3.0 / SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物及 plugins 根目录相同。旧版六文件 hash 未变；无新 ZIP、未上传。当前任务已完成，游戏实测仍待用户执行；不再重复构建或重做发行文件。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

用户已授权实施，且要求国际化最后统一修。0.1.3/0.1.3.0 已完成植物真实身份与迁移、箱子折叠筛选/单实体标签、当前段/全图范围、国际化及窗口刷新；71 项逻辑回归通过，PEAK 3.0.1 隔离 Release 0 警告 0 错误，已部署原 profile 的 WUYACHIYU-WhereIsThing 子目录。未启动游戏，主客机/自定义地图/中文字体与布局待实机。历史发行与 ZIP 未改。 当前最终 DLL hash 4D99F461…8B522；备份 `.build/whereisthing-0.1.3/backup-20261029-220734/`；源码和测试均已保存。下面“实施中/仅调研”段为历史。接续先读 [实施记录](../IMPLEMENTATION_0.1.3_2026-10-29.md)，不要重做已完成工作或声称实机通过。

## 2026-10-29 0.1.3 实施中（覆盖下方只调研状态）

用户已授权“开始修改”，并要求国际化最后统一处理。版本已升 0.1.3；已加入拾取身份解析、植物分帧发现/选择迁移、普通箱折叠高级筛选、陷阱同实体标签，以及当前段/全图开关。首轮隔离 Release 构建 0 警告 0 错误，输出 `.build/whereisthing-0.1.3/`；未部署。当前继续检查段归属与选择回归，然后统一国际化（包含新增功能）、测试、最终构建及文档同步。尚无实机验证，不将中间状态当完成。

## 2026-10-29 21:36 新基线已接续

用户已反编译 3.0.1；当前参考 `引用参考代码/反编译/3.0.1/Assembly-CSharp/`，旧 3.0.0 在 `作废/3.0.0/`。已核对本需求相关 12 个类，去空白/整行注释后均一致，五项调研结论保留。游戏 DLL hash 仍 E526A523…CD168。仍只调研、不改代码，目标 0.1.3 未实施；不要再次要求用户提供 3.0.1 反编译，也不把此次比对当全量兼容验证。

## 2026-10-29 0.1.3 调研停点

- 用户明确先调研、不改代码；源码/安装仍 0.1.2，目标 0.1.3。
- 最新报告：`../RESEARCH_0.1.3_2026-10-29.md`。国际化已确定三处 Game 错误落中文（雕像/背包后缀、变体数）；官方表存在截图三物品中文。当前 cfg=Game/无限距离/屏幕外箭头，日志启动语言 en，不能当作截图时状态的完整证明。
- 重要纠正：不能只加 FakeItem。当前资源 Mandrake_Hidden 是 Item，名称 Medicinal Root，isSecretlyOtherItemPrefab 指向 Mandrake；真实药用根茎无重定向。参考 MOD 同时处理 FakeItem 与秘密替身，定位需把真实身份和现场载体分开。
- 箱子建议默认功能种类+高级地区池筛选，同一实体去重，保留旧选择/预设；未获实施确认。
- 段范围建议与 MaxDistance 分离；段切换不一定换 scene，动态物品可能无段父节点。不能只 ClearLabels 或把出生段永久固化。截图 Level_4 是地图 scene 名，不据此认定 TFA 无尽关卡。
- 本机游戏 DLL SHA256 E526A523…CD168，1647616 bytes；资源 bundleVersion 原文 3.0.1--logFile，异于旧 3.0.0 审核。关键成员已只读确认，未全量审核。
- WIT 实际安装位于 plugins/WUYACHIYU-WhereIsThing/；默认构建输出根目录需防重复。未构建/部署/改源码、配置、版本或发行，仅保存 memory。
- 收尾：开发区 Git 无差异，DLL hash 不变；cfg 在 21:24:38 被外部更新，保留外部变化。三项已读显示配置仍一致，不声称整个 profile 字节未变。

## 2026-09-21 玩家名声明

发行 0.1.2 README Notes 已用英文声明玩家名仅供娱乐，不作为放置者的可靠证据；中途加入可能缺少此前放置物的玩家名。准确语义为“部分目标仅显示物品名称和距离，无玩家名”，不是“仅显示玩家名”。踏板菇/弹力菇/云雾菇依赖本机观察到的唯一近期投掷记录，缺失或歧义时留空；魔豆不显示种植者。CHANGELOG 同步，仅改文档，不改 DLL、不打 ZIP。

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 0.1.2。

## 2026-09-21 发行文件准备完成

当前版本 `0.1.2` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsThing/发行/0.1.2`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：放置物及 owner 识别、玩家放置预设、分类校正、分帧发现和标签优化、20000 排序、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

- 开发/测试版本 `0.1.2`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。补齐可见菜单刷新并限制配置 UI 翻译范围。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.1.2` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。
- 新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。
- DLL `0.1.2.0`，SHA-256 `CF1C9043F74E843032CB65F972137EA135297D690EA9E397929977AAB889BF63`。
- 23 项测试属于 WhySoLaggy.Tests，四项目构建全部 0/0；用户首次构建启动日志不能充当最终 UI 验收。
- 下一步实机按 `../../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，新版发行目录 `0.1.2` 已备齐、未打 ZIP；旧版目录与 ZIP 未改。

## 2026-09-20 历史状态

- 最新：用户要求沿用WIMA排序修正，Canvas已32700→20000；保留交互窗口所需GraphicRaycaster，不改字体、扫描、owner或协议。
- 普通Release构建0警告0错误、IL验证20000，版本0.1.2.0；已输出2.0.a/BepInEx/plugins/WhereIsThing.dll，SHA256 2ACB0BEC44002415B9EF4A29D149F22E27064C6B3582633E39A52286BFB7F0DE。WIT/TFA共存实机待复测，未打包。

## 2026-09-06 历史分类修正

- 当前基线：PEAK `2.4.b`，WhereIsThing `0.1.2` / `0.1.2.0`，测试阶段。
- 已参考 ItemSpawnerEnhanced `1.3.0` 的分类代码完成审计；该参考插件自述为 PEAK 2.1.a，但其 137 个审阅 prefab 和 37 个隐藏 prefab 在当前 2.4.b `resources.assets` 中仍全部存在。
- WhereIsThing 保留互斥用途分类，在 `ThingTypes.cs` 新增 51 项精确 prefab 覆盖；不采用多标签 UI，不新增 Deployable 分类，不直接隐藏内部/Variant/Prop 物品。
- 重点分类修正：Jetpack/Rocketpack→移动，Heat Pack/Healing Dart/Healing Puff Shroom→医疗，神秘变体→神秘，EarlyWorm/Beehive→生物，Honeycomb→食物，Scorpion→危险生物，棋子→玩具，放置工具按生存/武器/攀爬用途归类。
- Release 已覆盖 r2modman `profiles\\2.0.a\\BepInEx\\plugins\\WhereIsThing.dll`，`0 warnings / 0 errors`；DLL 大小 `158720` 字节，SHA-256 `AC1D98407D52BAA61E1B7B4EF33F5AB4B09CEC168E28D65E019587B048DCEFA9`。
- 未更新 `MOD开发\\WhereIsThing\\发行\\1.0.3`，未创建 ZIP。下一步是实机验证分类窗口以及既有 owner/标签正反例。
