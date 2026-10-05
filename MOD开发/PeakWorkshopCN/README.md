# PeakWorkshopCN

版本：`0.1.0`。日期：2026-10-05。

当前阶段：第一版试用补丁已构建并部署，完整中文覆盖和 Unity 实机验收待完成。用户在调研后已授权“先执行，我看看效果”。

2026-10-05 用户实机反馈大量英文后，已完成全量静态审核并进行第一轮补齐：[AUDIT.md](AUDIT.md)。当前仍有 3,122 个固定位置待分类和 749 个非固定调用待审；Boss 已改接真实 `DrawBosses`，并补入变量字段、泛型与部分裸写入口。完整实机复测待重启游戏。

独立的 Peak Workshop 中文显示补丁。原 MOD 继续安装并负责全部功能，PeakWorkshopCN 通过 Harmony 修改玩家可见的文字和必要字体。用户已确认范围包含菜单按钮、提示、编辑器标签和配置说明。

- 调研证据：[RESEARCH.md](RESEARCH.md)。
- 实施计划：[PLAN.md](PLAN.md)。
- 实现、动态文字证据及验证记录：[IMPLEMENTATION.md](IMPLEMENTATION.md)。
- 接续记录：`../../memory/mods/PeakWorkshopCN/temp/current.md`。
- 原 MOD 基线：PeakWorkshop `0.2.0`，程序集命名空间仍为 `PeakMapForge`。
- 游戏反编译基线：`../../引用参考代码/反编译/2.6.a/`。
- 已部署 DLL：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`。

不替换原 DLL，不复制原 MOD 功能，不修改地图格式、保存内容、网络协议或在线发布内容。上述界面翻译包含 MOD 自带固定描述；玩家自己填写的地图名称、介绍、NPC 对话、账号名等保留原文。

本文为开发入口，尚不是发行 README。

重启指定 profile 的游戏后查看工坊菜单和编辑器。补丁默认启用，独立配置为 `com.wuyachiyu.peakworkshopcn.cfg` 的 `General/Enabled`；原工坊仍须安装，原 auto/en/de 设置保持有效。关闭补丁时回退原语言。

构建：`dotnet build MOD开发/PeakWorkshopCN/PeakWorkshopCN/PeakWorkshopCN.csproj -c Release`。测试项目位于 `tests/`，静态 DLL 审计位于 `tools/Audit/`。目前 41 项测试通过，主项目构建 0 警告、0 错误；这些结果不能代替实机检查。
