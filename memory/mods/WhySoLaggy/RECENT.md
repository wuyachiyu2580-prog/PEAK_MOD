# WhySoLaggy Recent

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
