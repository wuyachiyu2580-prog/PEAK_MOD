# WhereIsMyAmulet 2026-09-21 接续摘要

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 1.0.4。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.4` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsMyAmulet/发行/1.0.4`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：19→3 TMP、共享材质和刷新缓存、20000 排序、投影 Z/固定字号修正、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

- 开发/测试版本 `1.0.4`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。恢复新版菜单跟踪并修正 UI 归属、枚举显示和刷新清理。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.4` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。
- 新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。
- DLL `1.0.4.0`，SHA-256 `70C24B097F1B943C80746DF64C8B6AD28372758238B915FA942A2EFE10860F04`。
- 23 项测试属于 WhySoLaggy.Tests，四项目构建全部 0/0；用户首次构建启动日志不能充当最终 UI 验收。
- 下一步实机按 `../../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，新版发行目录 `1.0.4` 已备齐、未打 ZIP；旧版目录与 ZIP 未改。

## 2026-09-20 历史状态

- 当前测试1.0.4.0，发行仍1.0.3；TMP 19→3、共享材质、相机/文本缓存、每帧ToList移除已实现，性能/视觉未验收。
- 用户最终对照结果：30000不行、20000可以。Manual mode自身禁用行为与排序冲突分别判断，不再将“只是Manual mode”作为完整结论。
- 已固定Canvas排序20000（继续无Raycaster），添加TMP popup=30000/TFA抬高窗口的原因注释；临时探针已删除，普通Release构建0警告0错误。
- 最新用户报告越远文字越大：发现WorldToScreenPoint的相机深度Z直接写入Overlay位置。已将Z清零，并统一创建/样式刷新时关闭标题、距离、箭头自动字号；实际视觉效果待确认，不将代码修正当成实机验收。
- 测试输出仍2.0.a/BepInEx/plugins/WhereIsMyAmulet.dll，程序集1.0.4.0，SHA256 DA005FAF81403620415FDA735F623C3F78F506BFEB46DF2026D4E57F4CAAE74E；构建0警告0错误，IL验证Z=0/AutoSizing=false，排序保持20000、无探针类型。
- 下拉排序已获用户对照验证，后续关注标签视觉/性能、多MOD共存。未改TFA/WIT、发行目录或ZIP；既有诊断报告未删除。

## 2026-08-31 历史发布记录

- 当前版本为 `1.0.3`；Scout Statue 已按 `hasAmulets[slot] -> amuletObjects[type]` 映射并在标签有效性检查中复核 slot/type/object。
- 1.0.3 发行目录和 ZIP 已存在，发行/profile DLL 均为 `1.0.3.0`、`40960` 字节，SHA-256 `77AA45DA6A90664910172596649F8E567F3369961D4EC19D7A5634E5FBDC7984`。
- 双行标签距离文字锚点为 `-40`；启动普通 Info 日志已删除。
- 尚未完成游戏内定时清理、多人显示验收；惊喜模式不在当前源码，不能记录为已实现。
