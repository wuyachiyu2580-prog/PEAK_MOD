# WhySoLaggy 当前状态

## 2026-11-18 已部署 2.0.a，等待实机验证

按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。

下一步在2.0.a启动游戏，进入实际扫描场景后开启两MOD的Diagnostics/PhaseTiming；默认45秒自动关闭。先保留现有配置做对照，再读取阶段报告定位5秒尖峰。自动测试仍为WhySoLaggy 105项、WhereIsThing 184+104项；本次仅部署，未重跑测试。

## 2026-11-18 实测修复已实施，隔离验收通过

WhySoLaggy已接入预录LossLedger及范围/未知披露、独立紧凑网络存储与2%保护/5%网络配额、SelfMetrics对象复用、默认关闭的30–60秒阶段计时、默认Both的可选持续日志格式。105/105正式测试、真实依赖Release零警告零错误；10包回放保持53,651帧、网络3,297条零字段差异、补回329条且无新增旧事件丢失。剩余253条预录明细以211–262上下界披露；最终共享预算0。

已按用户“开始/继续”完成正式修改；构建标识保留20261117.playtest。当前未部署、未启动游戏，66个安装DLL/PDB/cfg及历史发行文件核验不变。旧“未实施”段落仅为历史，以本条为准。

下一步：部署后在实际扫描场景采集45秒阶段报告并做开关对照，再决定雕像缓存/网络清理分批方案；继续晚安莓、护符、共享与菜单实机矩阵。Unity Mono收益仍待验。



[实施报告、验证边界与下一轮实测](../../IMPLEMENTATION_PlaytestFixes_2026-11-18.md)。

## 2026-11-17 实测修复方案已调研，未实施

已用生产EventRing逻辑回放，本次10份ZIP的Sequence集合逐条一致。单纯新增网络分区挤占明细方案否决；紧凑NetworkSample+保护摘要候选在相同区间将缺失582降至253（补回329），无新增原事件丢失，53,651帧保持，3,297条网络字段还原与5组压力检查通过。剩余预录明细仍须LossLedger正确披露；14项时间窗计数策略检查通过但未接入生产。SelfMetrics复用对象验证减少Tuple分配，非Unity性能承诺。

用户本轮只授权先调研。仅新增隔离实验与报告，未改正式源码/正式测试/cfg/安装DLL/发行，未部署或启动游戏；87项文件哈希核验不变。研究运行在.NET 10，不是Unity Mono实机；不能把候选回放和旧78/281正式测试相加，不能标记问题已修复。

[修复设计、被否决方案与验收边界](../../RESEARCH_PlaytestFixes_2026-11-17.md)。实验位于.build/playtest-fix-research-20261117/；仅同步本地memory。

## 2026-11-17 一局实测复核：运行确认，仍有待修缺口

WhySoLaggy retention构建已有本局运行证据：10份保留ZIP、53,651帧均连续，约20秒预录完整；但8份包报告关键事件省略合计328条，按Sequence对照实际少582条，预录部分省略未传入manifest。主线程SelfMainMs在保留Gameplay段均值0.249ms、P99 1.628ms、峰值85.046ms，需分阶段取证。写盘/RPC队列丢弃为0；CSV+JSONL合计约603MiB，JSONL已轮换掉本局前段。

本局CSV覆盖19:38–21:34，安装DLL与部署哈希一致。Player.log中Compass相关空引用17,124条，不能误归因到这两个MOD；GetDisplayName/LOC: 0未复现。此次仅审计并保存报告/证据，未改生产源码、配置、DLL或发行，未启动游戏。当前属于部分实机验收，覆盖下方从未实机的旧阶段状态；旧记录保留。

[本局完整审计、数据口径与待修优先级](../../PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md)。证据：.build/playthrough-20261117/；只同步本地memory。

## 2026-11-16 录制保留修复已部署，实机待验

已完成上次实机反馈后中断的录制保留修复：帧/关键事件/明细分区，方法按秒汇总并保留有限尖峰；导出显示真实覆盖时间及分类省略。78/78回归通过，Release零警告零错误。构建1.0.5-dev.20261116.retention（插件仍1.0.5）于00:19安装2.0.a/plugins根目录，仅一份DLL/PDB，旧版可回退，用户配置哈希未变。发行未改、未上传/提交。

当前已安装修复版；旧日志没有LOC: 0，但新版游戏表现尚未验收。不要重复部署11月15日旧DLL。

[实施、证据及回退](IMPLEMENTATION_Retention_2026-11-16.md)。

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 原症状反馈通过，后续仅调研

用户确认 LOC: 0 消失。最新日志 Hooked 644、Failed 0，原 GetDisplayName 错误 0 次；完整语言/主客机/性能矩阵仍待验。37/37 为上轮测试，本轮未重跑。

仍为 1.0.5 修复构建；本轮未改源码、配置、DLL、发行。候选优化见 [调研](RESEARCH_Roadmap_2026-11-14.md)。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

DLL 已部署原 2.0.a profile 的 plugins/WhySoLaggy.dll 根目录，仅一份；旧子目录 DLL 已备份移出。截图捕获超时，未启动游戏或实机验收。本条覆盖同日先前未实施状态。详见 [实施记录](IMPLEMENTATION_ModConfig_2026-11-13.md)。

## 2026-11-13 下一步方案已调研，未实施

本轮只新增 .build/FixStrategyProbe 独立实验与 memory，12/12 方案组合达到预期。正式源码、配置、安装 DLL 与版本均未变；首选规范化/去重，后续安全开关与并发保护、国际化最后检查。

[完整方案与验收](NEXT_STEPS_ModConfig_2026-11-13.md)。

## 2026-11-13 补充：旧版本同样具备触发条件

六版游戏的 MethodBase 反射实现相同；ModConfig 从 1.7.0 起采用泛型按键父类，WhySoLaggy 1.0.3–1.0.5 均有原方法注入计时。旧 3.0.0 审核 Harmony 与当前同哈希。隔离 Harmony 程序 12 组调用验证：继承目标加计时报同类异常，正确声明目标或不计时则正常；未运行旧版游戏。本次 ShootZombies 的继承目标告警与此链路直接相关。旧 9 月日志无 ShootZombies，且 Unity 日志写入失败，不能据此认定此前没有问题或计时开关一定关闭。

[历史调查](HISTORY_ModConfig_LOC0_2026-11-13.md)。仍只调研，未修复/部署；诊断输出见 .build/whysolaggy-modconfig-3.1.0-20261113/history-probe/。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

PEAK 3.1.0 实际复现：共享按键设置 GetDisplayName 的泛型方法句柄解析异常，中断列表构建并留下 LOC: 0。WhySoLaggy PatchProfiler 的 __originalMethod 注入是优先修复点；不是配置值被改成 0。8 次同类异常、补丁快照和安装 DLL IL 已核对；只完成调研，未改源码/配置/DLL，未构建部署或实机验证修复。

调查结论与边界：[RESEARCH_ModConfig_3.1.0_2026-11-13.md](RESEARCH_ModConfig_3.1.0_2026-11-13.md)。活动安装位于 profile/plugins/WUYACHIYU-WhySoLaggy/WhySoLaggy.dll，旧根目录记录仅属历史。

## 2026-10-23 PEAK 3.0.0 兼容性审核

1.0.5插件隔离Release 0警告/0错误，当前DLL成员检查无断点。现成测试22通过、1失败（写死反编译2.4.b目录）；独立复核63/63 watched RPC仍在3.0.0。需要更新测试路径，不因此判定插件运行时不兼容；测试源码与profile/发行DLL本轮未改。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhySoLaggy/发行/1.0.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 语言监听、配置归属和补丁警告；重新核对诊断配置及日志说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

开发/测试版本 `1.0.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。修正 Action 字段订阅、MOD 归属与测试执行链。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

结果：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

## 更新前记录（历史）

版本 `1.0.4`，发行包已建立；双客户端归因和卸载重载仍待验收。
## 当前状态 2026-11-20

全面优化计划已实施并通过 Release 回归。当前实机 profile 为 2.0.a，WhySoLaggy DLL/PDB 已部署到 `BepInEx/plugins/WUYACHIYU-WhySoLaggy/`，游戏日志确认加载成功。隔离构建输出位于 `MOD开发/WhySoLaggy/WhySoLaggy/bin/Release/`，107/107 测试通过，Release 零警告零错误。

最近一次自动捕获包含 6,100 帧。捕获损失质量为 Exact，帧、关键事件和明细事件均为 0 丢失。FrameSample 已包含 `HookCoverage`、`FrameIntervalsOmitted`，NetworkSample 已包含 30 秒趋势字段，自动计时样本标记为 Exact。

下一步是用相同 MOD、配置和场景做多轮开关 A/B，记录 WhySoLaggy 自身耗时、整局帧时间 p95/p99、GC 分配、写盘队列和丢失计数。未完成前不把性能收益记为最终验收结论。

