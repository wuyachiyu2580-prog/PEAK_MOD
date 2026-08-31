# WhySoLaggy Decisions

更新时间：2026-08-31

## 已确认决策

### 基础
- 当前维护/发布版本为 `1.0.4`，兼容基线为 PEAK `2.3.a`；`发行/1.0.4` 已建立。后续若工作区源码继续变化，必须重新构建、测试并核对包内容后才能更新发行状态。
- 本 MOD **只做诊断**，不改业务逻辑、不修补游戏 bug。
- AssemblyName：`WhySoLaggy`。

### 诊断能力构成
- 必备模块：`FpsTracker`、`LagLogger`、`RpcMonitor`、`NetworkAbuseDetector`、`PatchProfiler`、`PluginProfiler`、`HarmonyScanner`、`StructuredLogger`、`FieldProbe`、`PerformanceDashboard`。
- 新增诊断模块必须独立文件，放 `Helpers\` 目录。

### FieldProbe 设计
- 使用 DSL 驱动，避免硬编码探针。
- JSON 配置**禁用注释**，说明信息用 `_doc` / `note` 字段承载。
- 默认 `enabled: false`，避免对性能有影响。
- 配置文件位置：`BepInEx\config\WhySoLaggy.fieldprobe.json`（Steam 游戏目录下）。

### 1.0.4 网络和日志边界

- 滥用阈值只使用远端入站流量；本地调用只进入周期报告，不参与嫌疑人告警。
- Ownership Request 按远端 sender 统计，Transfer 按远端 new owner 统计，Update 只做批量审计。
- RPC 队列默认容量 `2048`，超限保留最新、丢弃最旧；`PumpBatchSize` 保持 `32`。
- watched RPC 每次只写一条 `RpcCall`；`RemoteRpcTrace` 枚举只为旧日志兼容保留。
- 结构化日志每秒批量写入；周期报告和退出必须强制 Flush；schema 不匹配必须轮转旧 CSV。
- 方法键使用完整类型名、方法名和参数签名，但旧 `Type.Method` Ignore 配置继续有效。
- 卸载顺序固定为停止回调/监控、模块 Shutdown/Reset、`UnpatchSelf()`、强制落盘并关闭日志。

### IMGUI 通知样式
- 左上角堆叠显示。
- 支持淡出动画。
- 不阻塞游戏输入。

## 禁止回退

- **别把版本号改回 1.0.5**（历史遗漏，已全量清理）。
- 别把 `FieldProbe` 默认改成 `enabled: true`——默认关闭是为了零开销。
- 别在 JSON 配置里用 `//` 或 `/* */` 注释，会被 JSON parser 拒绝。
- 别把诊断输出混进业务 MOD 的日志流——`StructuredLogger` 走独立通道。
- 别把本地调用重新混入远端洪水阈值，也别让 Ownership Update 进入抢夺计数。
- 别把 RPC 队列改回无界，别在周期报告阶段强制排空积压。

## 数据格式约定

- `RpcMonitor` 数据：`{rpcName, sender, count, avgIntervalMs}`。
- `LagLogger` 数据：`{frameIndex, dtMs, topStack}`。
- `PatchProfiler` 数据：`{patchName, totalMs, invokeCount, avgMs}`。

其他 MOD 要消费这些数据时，保持字段名不变。
