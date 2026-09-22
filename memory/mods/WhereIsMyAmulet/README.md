# WhereIsMyAmulet

## 2026-09-21 发行文件准备完成

当前版本 `1.0.4` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsMyAmulet/发行/1.0.4`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：19→3 TMP、共享材质和刷新缓存、20000 排序、投影 Z/固定字号修正、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前更新

开发/测试版本 `1.0.4`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。恢复新版菜单跟踪并修正 UI 归属、枚举显示和刷新清理。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.4` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

构建/测试/产物详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。下方旧日期发布和 hash 为历史记录。

WhereIsMyAmulet 是 PEAK 护符定位 MOD，扫描后显示掉落护符、装有护符的背包、普通雕像手中的碎片和 Scout Statue 上的碎片位置。

当前发布版本：`1.0.3`。

1.0.4最新测试补丁：屏幕标签不再带入相机投影深度，TMP自动字号显式关闭，用于排查用户反馈的远处文字变大；实机效果待确认。

当前开发/测试版本 `1.0.4`：TMP优化已实现；2026-09-20用户对照确认Canvas排序30000不行、20000可以，已固定20000并部署普通测试版。诊断探针已移除，发行包未更新。最新进度以 `STATUS.md`、`temp/current.md` 为准。

1.0.3 已修正 Scout Statue 碎片名称的槽位/类型映射，并将距离文字锚点下移以适配双行标题；启动日志也已降噪。接手时先读 `RECENT.md`、`DECISIONS.md` 和 `FILES.md`。源码位于 `MOD开发/WhereIsMyAmulet/WhereIsMyAmulet`，发行包位于 `MOD开发/WhereIsMyAmulet/发行/1.0.3`。

当前仍未完成惊喜模式需求，也未完成 1.0.3 的游戏内定时清理和多人显示验收。
