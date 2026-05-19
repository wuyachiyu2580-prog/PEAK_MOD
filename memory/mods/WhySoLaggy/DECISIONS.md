# WhySoLaggy Decisions

更新时间：2026-05-08

## 已确认决策

### 基础
- 当前版本记录为 `1.0.3`，版本号全链路一致（csproj / 注释 / README / CHANGELOG）。
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

### IMGUI 通知样式
- 左上角堆叠显示。
- 支持淡出动画。
- 不阻塞游戏输入。

## 禁止回退

- **别把版本号改回 1.0.5**（历史遗漏，已全量清理）。
- 别把 `FieldProbe` 默认改成 `enabled: true`——默认关闭是为了零开销。
- 别在 JSON 配置里用 `//` 或 `/* */` 注释，会被 JSON parser 拒绝。
- 别把诊断输出混进业务 MOD 的日志流——`StructuredLogger` 走独立通道。

## 数据格式约定

- `RpcMonitor` 数据：`{rpcName, sender, count, avgIntervalMs}`。
- `LagLogger` 数据：`{frameIndex, dtMs, topStack}`。
- `PatchProfiler` 数据：`{patchName, totalMs, invokeCount, avgMs}`。

其他 MOD 要消费这些数据时，保持字段名不变。
