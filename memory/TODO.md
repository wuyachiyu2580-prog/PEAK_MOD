# 全局待办

## 2026-10-23 PlayersInfo 复核补充

[PlayersInfo 专项报告](mods/PlayersInfo/COMPATIBILITY_3.0.0_2026-10-23.md)：补查 2.4.b → 3.0.0，0.2.5 暂无确定的强制 API/核心读数适配项；新增昵称读取顺序维护建议，并保留 3.0.0 实机验收。terrain 中 0.1.1 仍需更新。仅更新审核记录，源码及部署未改。

## 2026-10-23 PEAK 3.0.0 兼容性审核

- 待用户后续安排实施：修 ItemInfoCN 的 Mandrake/StatusField；修灯笼整合 MOD 的背包和点灯 API/同步；TerrainCustomiserCN 建立 3.0.0 引用/依赖并迁移生成/光照。
- WhySoLaggy 测试目录需要从写死 2.4.b 更新；63 个 watched RPC 已独立核验仍在。
- terrain profile 的 PlayersInfo 0.1.1 和 ItemInfoCN 1.0.0 为失效旧 DLL，使用前需核对更新；本轮未自动覆盖。
- 其余通过构建项目需补 3.0.0 实机；StateKeeper 跨平台身份比较是条件性增强项，不判定 Steam 采集全面失效。
- 完整结果：[COMPATIBILITY_3.0.0_2026-10-23.md](COMPATIBILITY_3.0.0_2026-10-23.md)。

各 MOD 的专属待办已迁移到 `memory/mods/<ModName>/TODO.md`。历史完整清单保存在 `archive/TODO-2026-09-09.md`，冻结项目不应自动执行其中内容。2026-10-12 PeakWorkshopCN 与 PeakWorkshopCNScanner 已作废，专属待办停止执行，历史资料见各自作废入口。

当前跨项目事项：

- 四 MOD ModConfig 更新已构建部署，发行文件五件套已备齐（无 ZIP、未上传）（PlayersInfo 0.2.5、WhySoLaggy 1.0.5、两个定位 MOD 0.1.2/1.0.4）；剩余实机矩阵见 `mods/ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，Lantern 和公共依赖仍另行处理。

- 完成 ModConfig 本地化公共依赖修复和多 MOD 语言切换复测。
- 完成 StateKeeper 与 PEAK-MAP 的接口、隐私和性能验收。
- 继续维护 memory 结构检查，避免状态、索引和临时摘要分叉。
