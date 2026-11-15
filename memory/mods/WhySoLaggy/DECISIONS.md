# WhySoLaggy Decisions

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 后续候选顺序

先准确性，再有界/限时取证，后 HUD/网络，最后国际化。不可把 Unity 内存净变化称为真实 GC 分配、多 owner 当已证实冲突、整条调用链耗时直接归罪某 MOD。网络先本地读取，不自动关 MOD/踢人或新增协议。

[依据与边界](RESEARCH_Roadmap_2026-11-14.md)。只有调研建议，非已实施功能。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

DLL 已部署原 2.0.a profile 的 plugins/WhySoLaggy.dll 根目录，仅一份；旧子目录 DLL 已备份移出。截图捕获超时，未启动游戏或实机验收。本条覆盖同日先前未实施状态。详见 [实施记录](IMPLEMENTATION_ModConfig_2026-11-13.md)。

## 2026-11-13 下一步方案已调研，未实施

首选声明目标规范化+去重，不因可能风险直接重写为动态生成计时回调；此备选已单独验证但尚无必要性证据。专属 Harmony id 只撤计时，保留语言/RPC；开关配置限定范围，其他热更新另列。国际化在最终功能确定后统一。

[完整方案与验收](NEXT_STEPS_ModConfig_2026-11-13.md)。

## 2026-11-13 补充：旧版本同样具备触发条件

六版游戏的 MethodBase 反射实现相同；ModConfig 从 1.7.0 起采用泛型按键父类，WhySoLaggy 1.0.3–1.0.5 均有原方法注入计时。旧 3.0.0 审核 Harmony 与当前同哈希。隔离 Harmony 程序 12 组调用验证：继承目标加计时报同类异常，正确声明目标或不计时则正常；未运行旧版游戏。本次 ShootZombies 的继承目标告警与此链路直接相关。旧 9 月日志无 ShootZombies，且 Unity 日志写入失败，不能据此认定此前没有问题或计时开关一定关闭。

[历史调查](HISTORY_ModConfig_LOC0_2026-11-13.md)。仍只调研，未修复/部署；诊断输出见 .build/whysolaggy-modconfig-3.1.0-20261113/history-probe/。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

本次是摸排，修复尚未授权实施。优先在 WhySoLaggy 计时注入边界解决共享方法异常，保留语言功能、配置身份和其他诊断模块；不盲目换全局 Harmony。捕获失败与修复验收分开记录。

方案及未证实部分：[RESEARCH_ModConfig_3.1.0_2026-11-13.md](RESEARCH_ModConfig_3.1.0_2026-11-13.md)。

## 2026-10-23 PEAK 3.0.0 兼容性审核

1.0.5插件隔离Release 0警告/0错误，当前DLL成员检查无断点。现成测试22通过、1失败（写死反编译2.4.b目录）；独立复核63/63 watched RPC仍在3.0.0。需要更新测试路径，不因此判定插件运行时不兼容；测试源码与profile/发行DLL本轮未改。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

## 2026-09-21 发行语言与格式

- 用户要求发行文件使用英文，保留上一版格式；仅追加对应版本更新说明及必要修改。
- README 和 CHANGELOG 都必须有当前版本的明确更新条目，不能只改版本号。

## 2026-09-21 发行文件范围

- 用户明确要求编辑四 MOD 发行文件，包含此前非 ModConfig 改动；该指示覆盖旧记录中的“验收前不更新发行目录”限制。
- 当前 `1.0.5` 目录仅准备 DLL、图标和三份说明/清单，不生成 ZIP、不上传；不因此宣称完成实机验收。
- 复用已验证最终 DLL，不改源码版本；既有历史发行目录及 ZIP 原样保留。

## 2026-09-21 ModConfig 接入约束

- ModConfig 保持 SoftDependency；按新菜单签名适配，旧类型只回退；按声明方法去重。
- 配置值、section/key、枚举原值与网络协议不变；只改归属明确的显示控件，不清空/重建第三方全局设置。
- 自身实例 autoSet=false 防止标题覆盖，不能修改共享模板；同根 UI 刷新合并，销毁清理自身监听/补丁。
- 本轮源码版本 1.0.5，历史发行包保留；最终实机边界见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

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
