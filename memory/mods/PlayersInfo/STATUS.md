# PlayersInfo 当前状态

## 2026-10-23 PlayersInfo 专项复核补充

补查实际开发基线 2.4.b → 3.0.0：0.2.5 暂未发现必须修改的核心读数/API 断点，不代表完整实机兼容。昵称优先读原始 Owner.NickName 是既有一致性问题，建议改用游戏处理后的 characterName；复活同步、跨平台、资源层级和多人仍待验收。terrain 中 0.1.1 需更新安装版本。本次只记录，未改源码/配置/版本/发行/安装 DLL。

详见 [专项复核](COMPATIBILITY_3.0.0_2026-10-23.md)。

## 2026-10-23 PEAK 3.0.0 兼容性审核

0.2.5隔离Release 0/0，当前引用和2个静态Harmony目标通过；terrain profile另有0.1.1旧DLL，含失效背包/物品槽成员，未覆盖。

完整结果：[3.0.0审核](../../COMPATIBILITY_3.0.0_2026-10-23.md)。本轮未改源码/版本或部署DLL，构建通过不代表实机兼容已验收。

## 2026-09-21 发行文件准备完成

当前版本 `0.2.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/PlayersInfo/发行/0.2.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 新菜单与语言刷新、简中/繁中识别；沿用完整队友 HUD 使用说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

开发/测试版本 `0.2.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。移除全局配置重注册并修正简中/繁中检测和语言通知。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.2.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

结果：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

## 更新前记录（历史）

版本 `0.2.4`，目标 PEAK `2.4.b`。profile 测试 DLL 和试发行包已生成；多人、观战、三档图标和倒计时仍待干净会话验收。

最新摘要见 `temp/current.md`，正式历史见 `RECENT.md`。
