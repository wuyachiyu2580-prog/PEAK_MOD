# WhereIsThing 当前状态

## 2026-09-21 发行文件准备完成

当前版本 `0.1.2` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsThing/发行/0.1.2`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：放置物及 owner 识别、玩家放置预设、分类校正、分帧发现和标签优化、20000 排序、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

开发/测试版本 `0.1.2`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。补齐可见菜单刷新并限制配置 UI 翻译范围。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.1.2` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

结果：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

## 更新前记录（历史）

版本 `0.1.2`，目标 PEAK `2.4.b`，处于房主/客户端实机验收阶段。测试 DLL 已编译，发行目录和 ZIP 未更新。

详细待办见 `TODO.md`，最新摘要见 `temp/current.md`。

2026-09-20：按用户要求将Canvas排序32700降为20000，沿用WhereIsMyAmulet已获用户验证的层级。保留选择窗口所需GraphicRaycaster；普通Release构建0警告0错误，0.1.2.0已部署原2.0.a profile，WIT与TFA共存效果待复测。
