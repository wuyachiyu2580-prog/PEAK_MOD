# WhySoLaggy 2026-09-21 接续摘要

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 1.0.5。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhySoLaggy/发行/1.0.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 语言监听、配置归属和补丁警告；重新核对诊断配置及日志说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

- 开发/测试版本 `1.0.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。修正 Action 字段订阅、MOD 归属与测试执行链。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。
- 新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。
- DLL `1.0.5.0`，SHA-256 `946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550`。
- 23 项测试属于 WhySoLaggy.Tests，四项目构建全部 0/0；用户首次构建启动日志不能充当最终 UI 验收。
- 下一步实机按 `../../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，新版发行目录 `1.0.5` 已备齐、未打 ZIP；旧版目录与 ZIP 未改。

## 2026-08-31 历史状态

- 1.0.4 发行目录和 ZIP 已建立，PEAK 兼容基线为 `2.3.a`。
- 发行 DLL/profile DLL 均为 `1.0.4.0`、`157184` 字节，SHA-256 `CC9FCC52E825088AE254754BF30BF5E58A6A1FA8FA78C5E3DDA0A121B3B71431`；ZIP SHA-256 `A2B6960F9FA5893EBC3434D391306A4142DAE422F744325D047327F369A0C809`。
- ZIP 内 DLL、README、CHANGELOG、manifest、icon 五个文件均与发行目录对应文件逐项一致。
- 当前测试源码包含 18 个 `[TestMethod]`，最近增加的 ModConfig 分类测试尚未重新执行；双客户端归因和卸载重载仍待实机验收。
