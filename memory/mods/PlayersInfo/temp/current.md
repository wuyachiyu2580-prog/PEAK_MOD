# PlayersInfo 2026-09-21 接续摘要

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 0.2.5。

## 2026-09-21 发行文件准备完成

当前版本 `0.2.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/PlayersInfo/发行/0.2.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 新菜单与语言刷新、简中/繁中识别；沿用完整队友 HUD 使用说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

- 开发/测试版本 `0.2.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。移除全局配置重注册并修正简中/繁中检测和语言通知。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.2.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。
- 新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。
- DLL `0.2.5.0`，SHA-256 `68D7B4BB25CD0C16F2DAE544BD683E174DE5E800D7F67528945C0A364B905FE0`。
- 23 项测试属于 WhySoLaggy.Tests，四项目构建全部 0/0；用户首次构建启动日志不能充当最终 UI 验收。
- 下一步实机按 `../../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，新版发行目录 `0.2.5` 已备齐、未打 ZIP；旧版目录与 ZIP 未改。

## 2026-09-07 历史状态

## 0.2.4 trial release

- Prepared `MOD开发\PlayersInfo\发行\0.2.4` as a trial release directory. It contains `PlayersInfo.dll`, `README.md`, `CHANGELOG.md`, `manifest.json`, `icon.png`, and `wuyachiyu-PlayersInfo-0.2.4.zip`.
- `CHANGELOG.md` keeps all previous version sections and prepends `0.2.4` with the dead-corpse teammate stamina bar exclusion. The note explicitly distinguishes true dead corpses from downed teammates.
- Release build passed with `0 warnings / 0 errors`. The release DLL is `0.2.4.0`, `98304` bytes, SHA-256 `973E9279F70ED5CC25CBC481673D2942394A35100001D4B027C3BD4E1E850BBB`; the zip SHA-256 is `C64A4154F7903EF4E1FA2CDBAC030AE11B8689C2BAD480987AB0DE2C330F95FA`.

## Book of Bones impact check

- PlayersInfo's 0.2.4 exclusion is keyed only on `CharacterData.dead`: `RefreshNearby()` removes dead roster entries and `TeammateBarDriver` hides a bound root when `Target.data.dead` becomes true.
- The PEAK publicized symbols expose skeleton state as a separate `CharacterData.isSkeleton` / `SetSkeleton` / `RPC_SyncSkeleton` path, with `Action_BookOfBonesAnim.BookOfBonesAnimRPC` as the Book of Bones route. PlayersInfo already treats `isSkeleton` separately in the local hunger countdown helper.
- Current conclusion: a teammate who is turned into a skeleton by Book of Bones should not be hidden by the 0.2.4 dead-corpse filter unless PEAK also sets `dead=true` for that character. Still verify once in-game because this was derived from publicized symbols and PlayersInfo source rather than a full runtime capture.
