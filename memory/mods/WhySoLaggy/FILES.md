# WhySoLaggy Files

## 2026-11-18 已部署 2.0.a，等待实机验证

按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。

下一步在2.0.a启动游戏，进入实际扫描场景后开启两MOD的Diagnostics/PhaseTiming；默认45秒自动关闭。先保留现有配置做对照，再读取阶段报告定位5秒尖峰。自动测试仍为WhySoLaggy 105项、WhereIsThing 184+104项；本次仅部署，未重跑测试。

## 2026-11-18 实测修复已实施，隔离验收通过

WhySoLaggy已接入预录LossLedger及范围/未知披露、独立紧凑网络存储与2%保护/5%网络配额、SelfMetrics对象复用、默认关闭的30–60秒阶段计时、默认Both的可选持续日志格式。105/105正式测试、真实依赖Release零警告零错误；10包回放保持53,651帧、网络3,297条零字段差异、补回329条且无新增旧事件丢失。剩余253条预录明细以211–262上下界披露；最终共享预算0。

已按用户“开始/继续”完成正式修改；构建标识保留20261117.playtest。当前未部署、未启动游戏，66个安装DLL/PDB/cfg及历史发行文件核验不变。旧“未实施”段落仅为历史，以本条为准。

下一步：部署后在实际扫描场景采集45秒阶段报告并做开关对照，再决定雕像缓存/网络清理分批方案；继续晚安莓、护符、共享与菜单实机矩阵。Unity Mono收益仍待验。

关键新增源码：Helpers/CaptureLossLedger.cs、CaptureNetwork.cs、BoundedPhaseProfiler.cs、DiagnosticPhases.cs及Tests/PlaytestFixTests.cs。

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

新增CaptureBuffer.cs、CaptureTiming.cs、CaptureTimingWindow.cs及CaptureRetentionTests.cs。证据/产物/回退均在.build/whysolaggy-retention-20261116/；DLL SHA256：97F1320DE575B44034F251F6FBFC2A1A2250BC4BF736C0E89FA2EFDB80AA328E。

[实施、证据及回退](IMPLEMENTATION_Retention_2026-11-16.md)。

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 后续调研

- [功能与优化报告](RESEARCH_Roadmap_2026-11-14.md)。
- .build/whysolaggy-roadmap-20261114/：日志、补丁 CSV、summary.json；分组数字不是精确方法/冲突数。
- 安装 DLL 哈希复核仍为 1C04AAF283EE30E9B3CAC923A5C1A431ECE1B46F843AA149B9C4BC2CFA405807；未部署新文件。

## 2026-11-13 修复已实施并部署，实机待验

- [实施记录](IMPLEMENTATION_ModConfig_2026-11-13.md)；证据 .build/whysolaggy-fix-20261113/，含 final-deploy.trx、deployment.json、backup/。
- 当前安装：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/WhySoLaggy.dll；1.0.5.0，166912 bytes，SHA256 1C04AAF283EE30E9B3CAC923A5C1A431ECE1B46F843AA149B9C4BC2CFA405807。单份 DLL，PDB 同步，旧子目录 DLL 已移至备份。
- 配置哈希仍 9842FF91345A68FB49B04C1B030E26468D0EA6C41688A44845C4683EBBD93695；旧发行 DLL 仍 946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550。

## 2026-11-13 下一步方案已调研，未实施

新增方案报告 NEXT_STEPS_ModConfig_2026-11-13.md；实验 .build/whysolaggy-modconfig-3.1.0-20261113/history-probe/FixStrategyProbe.cs、FixStrategyProbe.exe、strategy-results.txt。独立程序不是可部署 MOD，结果不等于 Unity 实机验收。

[完整方案与验收](NEXT_STEPS_ModConfig_2026-11-13.md)。

## 2026-11-13 补充：旧版本同样具备触发条件

六版游戏的 MethodBase 反射实现相同；ModConfig 从 1.7.0 起采用泛型按键父类，WhySoLaggy 1.0.3–1.0.5 均有原方法注入计时。旧 3.0.0 审核 Harmony 与当前同哈希。隔离 Harmony 程序 12 组调用验证：继承目标加计时报同类异常，正确声明目标或不计时则正常；未运行旧版游戏。本次 ShootZombies 的继承目标告警与此链路直接相关。旧 9 月日志无 ShootZombies，且 Unity 日志写入失败，不能据此认定此前没有问题或计时开关一定关闭。

[历史调查](HISTORY_ModConfig_LOC0_2026-11-13.md)。仍只调研，未修复/部署；诊断输出见 .build/whysolaggy-modconfig-3.1.0-20261113/history-probe/。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

- [RESEARCH_ModConfig_3.1.0_2026-11-13.md](RESEARCH_ModConfig_3.1.0_2026-11-13.md)：本次完整诊断与修复建议。
- 证据目录：.build/whysolaggy-modconfig-3.1.0-20261113/，含 Player 两轮日志、BepInEx/WhySoLaggy 日志、harmony 补丁 CSV。
- 当前安装：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhySoLaggy/WhySoLaggy.dll，1.0.5.0，160256 bytes，SHA256 946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550。未改安装文件。

## 2026-10-23 PEAK 3.0.0 兼容性审核

1.0.5插件隔离Release 0警告/0错误，当前DLL成员检查无断点。现成测试22通过、1失败（写死反编译2.4.b目录）；独立复核63/63 watched RPC仍在3.0.0。需要更新测试路径，不因此判定插件运行时不兼容；测试源码与profile/发行DLL本轮未改。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhySoLaggy/发行/1.0.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 语言监听、配置归属和补丁警告；重新核对诊断配置及日志说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前产物

- 开发版本 `1.0.5` / 程序集 `1.0.5.0`；新增 `Helpers/ModConfigUiAdapter.cs`（独立反射适配器）。
- profile 输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhySoLaggy.dll`，160256 字节，SHA-256 `946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550`。
- 旧发行 DLL/hash 属于历史，不再与当前 profile 相同；验证入口：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-08-31

## 路径

- 源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy`
- 项目文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy\WhySoLaggy.csproj`
- AssemblyName：`WhySoLaggy`
- Version：`1.0.4` / AssemblyVersion `1.0.4.0`
- 输出路径：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\`
- 当前发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\发行\1.0.4`
- 当前发行 ZIP：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\发行\1.0.4\wuyachiyu-WhySoLaggy-1.0.4.zip`
- 发行 DLL：`1.0.4.0`、`157184` 字节，SHA-256 `CC9FCC52E825088AE254754BF30BF5E58A6A1FA8FA78C5E3DDA0A121B3B71431`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy\WhySoLaggy.csproj" -c Release
```

Release 构建会直接覆盖上述 `2.0.a` profile 中的 `WhySoLaggy.dll`。

## 已识别关键文件

- `WhySoLaggyPlugin.cs`
- `Helpers\LagLogger.cs`
- `Helpers\FpsTracker.cs`
- `Helpers\RpcMonitor.cs`
- `Helpers\NetworkAbuseDetector.cs`
- `Helpers\PerformanceDashboard.cs`
- `Helpers\PatchProfiler.cs`
- `Helpers\PluginProfiler.cs`
- `Helpers\HarmonyScanner.cs`
- `Helpers\FieldProbe.cs`
- `Helpers\StructuredLogger.cs`
- `Helpers\BoundedConcurrentQueue.cs`
- `Helpers\MethodKey.cs`
- `Helpers\OwnershipEventParser.cs`

## 测试

- 测试项目：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy.Tests\WhySoLaggy.Tests.csproj`
- 测试入口：`WhySoLaggy.Tests\CoreBehaviorTests.cs`
- 覆盖 Ownership payload、有界队列、Profiler 估算/精确调用数、方法键、FieldProbe 静态根、日志批处理/schema 轮转、重复初始化、ModConfig 本地化和 PEAK 2.3.a watched RPC 名单；当前文件共有 18 个 `[TestMethod]`。
