# PlayersInfo 待办

## 2026-10-23 3.0.0 专项待办

- 建议调整 SafeGetName：优先游戏处理后的 characterName，并处理异步就绪；属于既有显示一致性维护，不宣称升级导致崩溃。
- 补 3.0.0 主客机、晚加入/重连、复活/观战、石化/无限体力、物品/燃料和 ModConfig 实机验证；资源层级未验。
- 若使用 terrain profile，先处理其 0.1.1 旧 DLL；2.0.a 已有 0.2.5。
- [专项证据](COMPATIBILITY_3.0.0_2026-10-23.md)：本轮未实施源码修复或部署。

- 发行文件准备已完成：`MOD开发/PlayersInfo/发行/0.2.5`；未生成 ZIP、未上传。待办仍以以下实机项为准。

- ModConfig 源码修复及 profile 部署已完成；按 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md` 验收最终 DLL 的中英繁切换、下拉/DEFAULT、搜索/筛选、多 MOD 隔离、无依赖启动及销毁恢复。

- 完成 `AfflictionIconDisplayMode`、死亡/晕倒距离和观战边界的多人验收。
- 验证零体力倒计时、字体、刷新频率和 DebugLogging 安静模式。
- `发行/0.2.5` 文件已备齐；完整验收前不标记为已正式发布，旧 `0.2.4` 试发行保留。
