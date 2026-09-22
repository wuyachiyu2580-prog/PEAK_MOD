# WhereIsThing 2026-09-21 接续摘要

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
