# WhereIsMyAmulet 当前状态

## 2026-09-21 发行文件准备完成

当前版本 `1.0.4` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsMyAmulet/发行/1.0.4`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：19→3 TMP、共享材质和刷新缓存、20000 排序、投影 Z/固定字号修正、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

开发/测试版本 `1.0.4`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。恢复新版菜单跟踪并修正 UI 归属、枚举显示和刷新清理。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.4` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

结果：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

## 更新前记录（历史）

开发/测试版本 `1.0.4`，发行仍为 `1.0.3`。已实现 19→3 TMP、共享材质、相机/文本缓存及移除每帧 ToList；视觉和性能待实机验证。

最新字号反馈：用户报告越远文字越大。已将WorldToScreenPoint输出Z清零，明确关闭标题/距离/箭头自动字号；0警告0错误编译部署，实机效果待确认。Canvas仍20000。

2026-09-20最终对照反馈：用户确认30000不行、20000可以。已固定Canvas排序20000，保留无Raycaster与TMP优化，临时探针已移除；普通Release构建0警告0错误并部署原profile。Manual mode自身的禁用行为与排序问题需分别判断。
