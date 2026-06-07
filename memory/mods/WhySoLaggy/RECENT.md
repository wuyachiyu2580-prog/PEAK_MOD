# WhySoLaggy Recent

更新时间：2026-05-13

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
