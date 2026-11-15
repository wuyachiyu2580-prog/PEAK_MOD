# 全局待办

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](mods/WhySoLaggy/IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 WhySoLaggy 原症状反馈通过

- LOC: 0 已获用户实机通过反馈；完整语言/主客机/开关/性能矩阵仍待验。
- [候选优化](mods/WhySoLaggy/RESEARCH_Roadmap_2026-11-14.md)：先准确性、后限时取证/诊断包，再 HUD/网络，最后国际化。本轮只调研，未实施新功能。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

[WhySoLaggy 实施与验收](mods/WhySoLaggy/IMPLEMENTATION_ModConfig_2026-11-13.md)：已部署 2.0.a/plugins 根目录单份 DLL、旧 DLL 备份、配置未变。实机工具截图超时，完整 profile 界面验收待完成；未发行或提交。

## 2026-11-13 下一步方案已调研，未实施

WhySoLaggy 修复执行顺序与范围见 [下一步方案](mods/WhySoLaggy/NEXT_STEPS_ModConfig_2026-11-13.md)：先计时入口、再安全开关/并发统计，国际化最后，隔离构建后完整 profile A/B。只调研，部署与正式修复未执行。

## 2026-11-13 补充：旧版本同样具备触发条件

WhySoLaggy LOC: 0 并非已证实的 PEAK 3.1.0 新缺陷：历史源码/依赖及独立 Harmony 实验说明旧版也有条件触发。本次 ShootZombies 继承目标与启用补丁计时组合相关。详见 [历史调查](mods/WhySoLaggy/HISTORY_ModConfig_LOC0_2026-11-13.md)。无生产代码/配置/安装变更。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

WhySoLaggy 的共享按键设置异常会中断其他 MOD 菜单；优先修计时注入，补多 MOD/多语言实机和真正执行补丁的回归。配置热更新另行明确。见 [专项调研](mods/WhySoLaggy/RESEARCH_ModConfig_3.1.0_2026-11-13.md)。本轮只调查，未修复或部署。

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

WhereIsThing 0.1.3 发行五件套已完成（沿用 0.1.2 格式），不再列为未准备。仍待植物/箱子/关卡范围/语言字体/主客机实机验收；无新 ZIP、未上传。DLL 已在 2.0.a/BepInEx/plugins 根目录，版本已获用户确认。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

WhereIsThing 0.1.3 已实施、71 项逻辑回归通过、Release 0/0 并部署单份到原 WUYACHIYU-WhereIsThing 子目录。下一步为主客机、地形植物、跨段/自定义地图、箱子与语言 UI 实机；不再停留只调研。详见 [实施记录](mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md)。未准备发行包/ZIP。

## 2026-10-29 基线提醒

WhereIsThing 0.1.3 已完成更新前调研，尚未实施；专属任务见 `mods/WhereIsThing/TODO.md`。本机游戏 DLL 已异于 10 月 23 日 3.0.0 基线，资源版本标记 3.0.1；后续开发重新核对当前安装 DLL，不自动批量改其他 MOD。

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
