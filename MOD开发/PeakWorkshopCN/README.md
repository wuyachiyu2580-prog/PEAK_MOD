# PeakWorkshopCN

## 当前统一修复状态（2026-10-07）

资源显示修订版已完成独立构建：版本 `0.1.0`，0 警告、0 错误，51 项离线测试通过。当前 DLL 在 `C:/Users/Administrator/Desktop/MOD/PEAK/.build/peakworkshopcn-unified/candidate/PeakWorkshopCN.dll`，大小 2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`。

本轮统一补齐固定、动态和直接显示入口，嵌入 4,246 条译文；接入官方术语、构件/材质中文显示与中英文搜索，并修复字段 getter、回调、泛型 Strip、模板参数、MarkerRef、字体和配置缓存路径。新增 `Basalt Cluster`、`CliffPillar`、`LavaPillar`、`BlueRock`、`DesertRock`、`Rock Volcano` 等显示映射，并接入 `RockLook.Label`。原 PeakWorkshop DLL 已从 `.old` 备份恢复；Unity 实机页面、联机及性能采样待重启后完成。

版本：`0.1.0`。日期：2026-10-05。

当前阶段：第一版试用补丁已构建并部署，完整中文覆盖和 Unity 实机验收待完成。用户在调研后已授权“先执行，我看看效果”。

最新接续调研：[术语一致性与性能报告](TERMINOLOGY_PERFORMANCE_RESEARCH.md)。已从游戏安装包提取官方中文表，SHORE按用户确认采用“海岸”，并核实雨林/森蕈/雪山/方山等；已定位字体重复遍历、字段getter和MarkerRef缓存问题，给出优化顺序及验证标准。本轮未修改插件或部署，未做实机性能测量。

2026-10-05 最新全量复核：[AUDIT_RECHECK.md](AUDIT_RECHECK.md)。新日志已确认补丁初始化完成，但固定位置仍有 2,197 个无映射，764 个非固定调用需逐条审核；另有资源名称、回调来源、跳转边界、字段错配和缓存问题。构件及材质的玩家可见名称包含在中文化范围内，原资源键继续用于保存和引用。本轮只审核，没有更新插件或部署。

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

构建：`dotnet build MOD开发/PeakWorkshopCN/PeakWorkshopCN/PeakWorkshopCN.csproj -c Release`。测试项目位于 `tests/`，静态 DLL 审计位于 `tools/Audit/`。当前修订版 51 项测试通过，主项目构建 0 警告、0 错误；这些结果不能代替实机检查。


