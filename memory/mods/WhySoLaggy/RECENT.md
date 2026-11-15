# WhySoLaggy Recent

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 原症状反馈通过与后续调研

用户确认 LOC: 0 不再出现，日志中原 GetDisplayName 错误 0 次；完整矩阵仍待验。读取当前代码、3.1.0 接口及最新日志，保存 .build/whysolaggy-roadmap-20261114/。

[后续调研](RESEARCH_Roadmap_2026-11-14.md) 已完成；本轮仅文档，无生产改动或重跑构建/测试。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

DLL 已部署原 2.0.a profile 的 plugins/WhySoLaggy.dll 根目录，仅一份；旧子目录 DLL 已备份移出。截图捕获超时，未启动游戏或实机验收。本条覆盖同日先前未实施状态。详见 [实施记录](IMPLEMENTATION_ModConfig_2026-11-13.md)。

## 2026-11-13 下一步方案已调研，未实施

新增 FixStrategyProbe：规范化、绑定 id 回调、跳过策略各 4 个组合，覆盖后续重挂和三轮开关；12/12 达到预期。优先较小改动方案，保留动态回调备选。记录 Shutdown、共享 owner 和网络线程统计风险。

[完整方案与验收](NEXT_STEPS_ModConfig_2026-11-13.md)。

## 2026-11-13 补充：旧版本同样具备触发条件

六版游戏的 MethodBase 反射实现相同；ModConfig 从 1.7.0 起采用泛型按键父类，WhySoLaggy 1.0.3–1.0.5 均有原方法注入计时。旧 3.0.0 审核 Harmony 与当前同哈希。隔离 Harmony 程序 12 组调用验证：继承目标加计时报同类异常，正确声明目标或不计时则正常；未运行旧版游戏。本次 ShootZombies 的继承目标告警与此链路直接相关。旧 9 月日志无 ShootZombies，且 Unity 日志写入失败，不能据此认定此前没有问题或计时开关一定关闭。

[历史调查](HISTORY_ModConfig_LOC0_2026-11-13.md)。仍只调研，未修复/部署；诊断输出见 .build/whysolaggy-modconfig-3.1.0-20261113/history-probe/。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

PEAK 3.1.0 实际复现：共享按键设置 GetDisplayName 的泛型方法句柄解析异常，中断列表构建并留下 LOC: 0。WhySoLaggy PatchProfiler 的 __originalMethod 注入是优先修复点；不是配置值被改成 0。8 次同类异常、补丁快照和安装 DLL IL 已核对；只完成调研，未改源码/配置/DLL，未构建部署或实机验证修复。

保存五份复现日志/补丁快照，验证当前 Harmony 的 ReflectedType 判断与泛型句柄单/双参数差异。报告：[RESEARCH_ModConfig_3.1.0_2026-11-13.md](RESEARCH_ModConfig_3.1.0_2026-11-13.md)。

## 2026-10-23 PEAK 3.0.0 兼容性审核

1.0.5插件隔离Release 0警告/0错误，当前DLL成员检查无断点。现成测试22通过、1失败（写死反编译2.4.b目录）；独立复核63/63 watched RPC仍在3.0.0。需要更新测试路径，不因此判定插件运行时不兼容；测试源码与profile/发行DLL本轮未改。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

## 2026-09-21 发行文案纠正

发行文件已按用户纠正恢复英文，沿用上一版章节、表格、图片位置和 manifest 排版；本版更新说明明确写入 README 的 What's new 和 CHANGELOG 对应版本章节。DLL/图标/旧版目录及 ZIP 未改，未生成新 ZIP。 当前发行版本 1.0.5。

## 2026-09-21 发行文件准备完成

当前版本 `1.0.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhySoLaggy/发行/1.0.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 语言监听、配置归属和补丁警告；重新核对诊断配置及日志说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 ModConfig 集成更新

开发/测试版本 `1.0.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。修正 Action 字段订阅、MOD 归属与测试执行链。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/1.0.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。

WhySoLaggy.Tests 实际 23 项通过/0 跳过；四项目构建通过。完整 UI 验收未完成，详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-08-31

## 1.0.4 发行包已建立

- `发行/1.0.4` 已建立，包含 DLL、README、CHANGELOG、manifest、icon 和 `wuyachiyu-WhySoLaggy-1.0.4.zip`。
- 发行 DLL 与 2.0.a profile DLL 均为 `1.0.4.0`、`157184` 字节，SHA-256 为 `CC9FCC52E825088AE254754BF30BF5E58A6A1FA8FA78C5E3DDA0A121B3B71431`。
- ZIP SHA-256 为 `A2B6960F9FA5893EBC3434D391306A4142DAE422F744325D047327F369A0C809`，包内五个文件与发行目录对应文件一致。
- 当前 `CoreBehaviorTests.cs` 有 18 个测试方法；旧记忆中的 9 项是过时数量。最近的 ModConfig 分类测试尚未在本轮重新执行。
- 当前工作区还有未提交的 `ModConfigLocalization` 与测试改动，后续重新跑测试后再决定是否制作下一版包。

## 2026-08-29 部署到 2.0.a profile

- `WhySoLaggy.csproj` 的 `OutputPath` 已改为 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\`。
- 1.0.4 Release 发行 DLL 与 profile DLL 当前均为 `1.0.4.0`、`157184` 字节，SHA-256 `CC9FCC52E825088AE254754BF30BF5E58A6A1FA8FA78C5E3DDA0A121B3B71431`。

## 2026-08-28 WhySoLaggy 1.0.4 全量修复

- 以 PEAK `2.3.a` 反编译源码为基线，修正 PUN Ownership 事件码为 Request `209`、Transfer `210`、Update `212`，忽略 VacantViewIds `211`；Request、Transfer 分别统计，Update 只审计，畸形 payload 限频告警且不计数。
- Instantiate、Destroy 和 RPC 洪水阈值只使用远端入站事件；周期报告保留本地、远端和合计三组总量，并绑定 `ActorMethodRateThreshold=20`、`OwnershipGrabRateThreshold=10`、`OwnershipRequestRateThreshold=20`。
- RPC 队列改为有界队列，默认 `QueueCapacity=2048`，超限丢弃最旧项并每窗口最多输出一条 `RpcQueueOverflow`；周期报告不强制抽空队列。
- watched RPC 每次只生成一条完整 `RpcCall`，不再写重复 `RemoteRpcTrace`；移除 2.3.a 不存在的 8 个旧 RPC，加入 `OnPickupAccepted`、`SetItemInstanceDataRPC`、`SetKinematicRPC`、`RPCA_StartGrabbing`、`RPCA_GrabCharacter`、`RPC_SpawnItemInHandMaster`，攀爬 Start/Stop 只做聚合。
- StructuredLogger 改为内存缓冲、每秒统一写入和 Flush，报告/退出强制落盘；CSV schema 不一致时先轮转旧文件。
- PatchProfiler 保留精确调用数和帧级 spike，周期总耗时按采样均值估算；方法键使用完整类型和参数签名，同时兼容旧 `Type.Method` Ignore。
- Zombie 数量改为反射读取 `ZombieManager.Instance.zombies.Count`；FieldProbe 静态根和所有监控模块的 Shutdown/Reset 已修复，插件退出会 `UnpatchSelf()`。
- 新增 `net472` MSTest 项目；历史首轮有 9 项测试通过。当前测试源码扩展到 18 项，最近的 ModConfig 分类测试需要重新执行。
- 1.0.4 发行包已经建立；剩余工作是双客户端实机验收远端归因、Ownership 分类、单 RPC 单记录、队列有界和同进程卸载重载，以及重新执行当前 18 项测试。

## 1.0.4 测试配置

- 联机首轮：打开 AbuseDetection 与 RpcMonitor，将 LogVerbosity 设为 Normal；其余性能 Profiler 和 FieldProbe 先保持关闭。
- 队列压力测试：保持 `QueueCapacity=2048` 和 `PumpBatchSize=32` 先测真实负载；若要主动触发溢出，可临时把 QueueCapacity 调到允许的最小值 `256`，测完恢复 `2048`。
- 性能归因另开一轮：只开启 `EnablePluginProfiling` 或 `EnablePatchProfiling` 中需要的一项，不与网络基线测试混跑。

## 2026-05-13 PEAK 1.62.a 兼容性检查

- 用户更新 PEAK 1.62.a 反编译后复核 WhySoLaggy。`Assembly-CSharp`、`PhotonUnityNetworking`、`PhotonRealtime`、`Photon3Unity3D` 与 1.61.b 反编译源码哈希一致，RPC/Photon 诊断依赖的基础类型未变化。
- Release 构建通过：`dotnet build ... WhySoLaggy.csproj -c Release`，0 warnings，0 errors。暂不需要代码更新。

## 已稳定的诊断能力

### 帧率与卡顿
- `FpsTracker`：帧率采样，输出滑动均值。
- `LagLogger`：卡顿帧落盘，带上下文堆栈。
- `PerformanceDashboard`：左上角 IMGUI 面板，支持**堆叠 + 淡出**样式通知。

### 联机诊断
- `RpcMonitor`：按 RPC 名、发送者、频率归因。
- `NetworkAbuseDetector`：单玩家 RPC 频率超阈值自动告警，用于**炸房事件事后溯源**。

### Harmony / 插件耗时
- `PatchProfiler`：每个 Harmony 补丁的 prefix/postfix 耗时。
- `PluginProfiler`：插件级别耗时聚合。
- `HarmonyScanner`：全量扫描已注册的 Harmony 补丁，便于查冲突。

### 结构化日志与探针
- `StructuredLogger`：键值对日志，便于日志后处理工具抓取。
- `FieldProbe`：运行时字段值探针，通过 DSL 动态采样。
  - DSL 语法：`Type.Method >> 字段表达式1, 字段表达式2, ...`
  - JSON 配置位置：`测试环境\BepInEx\config\WhySoLaggy.fieldprobe.json`（实际在 Steam 游戏目录的 BepInEx/config）。
  - 默认**关闭**，需显式 `enabled: true`。

## 版本号治理完成

- 1.0.3 迭代中做过一次全项目版本号一致性治理：
  1. 搜全项目含 `1.0.5` 的历史注释。
  2. 更新基线记忆：标题 + 约束规则。
  3. 批量替换 4 个 C# 文件中 22 处 `1.0.5` → `1.0.3`。
  4. 跑 `dotnet build` 验证编译通过。
  5. CHANGELOG.md / README.md 补充 Master 端功能测试状态说明。

## 当前验证结论

- 1.0.3 已稳定运行，诊断模块全部可用。
- `FieldProbe` 在开启状态下对 FPS 的影响可忽略（限频后）。
- `NetworkAbuseDetector` 有效识别过真实炸房事件，溯源准确。

## 待补充

- 默认日志级别的确认需从 `BepInEx.cfg` 反查。
- 哪些监控项默认开启、哪些关闭，需从 `WhySoLaggyPlugin.cs` 源码抽取。
- 与其他 MOD 联合诊断时的操作步骤清单未整理。
