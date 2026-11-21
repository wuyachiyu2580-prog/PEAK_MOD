# 2026-11-17 WhereIsThing / WhySoLaggy 实机日志复核

用户游玩一遍后要求检查两个 MOD。此次仅审计日志、诊断 ZIP 与现有源码；没有修改生产代码、用户配置、安装文件或发行，没有重建、重跑旧测试或启动游戏。

## 当前结论

- 两个实际安装 DLL 与此前部署哈希一致，均正常加载。WhereIsThing 已有运行证据，不能再笼统记为“从未实机”；具体玩法功能仍需验收。
- WhySoLaggy 的帧保留修复有效：当前保留的 10 份诊断包都有约 20 秒预录，帧连续，合计 53,651 帧，DroppedFrames 全为 0。旧版仅保住末尾几秒的症状在这 10 份包中未复现。
- WhySoLaggy 仍有实际缺口：8/10 包丢弃关键事件；滚动预录的部分省略没有传递到导出计数；自身主线程计量有明显尖峰。不能将本次验收写成“全部通过”。
- WhereIsThing 的 Update 全程周期报告累计 699,949 次调用、699,949 次计时、0 次异常，观测均值 0.117934 ms/调用；但保留的诊断窗口中有约每 5 秒重复的 8–15 ms 尖峰，应优先定位周期扫描。
- 最突出的异常洪泛来自 Compass：Player.log 中 17,124 条异常堆栈涉及 CompassTapeUI。不能把这批异常归为 WhereIsThing / WhySoLaggy，也不能只凭异常数量断言它解释了全部卡顿。

## 会话与证据范围

- profile：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/。
- SessionId：a6aba86e291e4664be0fa1a3498d9bfe。按 SessionId 筛选，避免把追加写入的旧日志当成本次。
- CSV 当前会话覆盖 2026-11-17 19:38:01.009–21:34:56.231，702,281 个 FrameSample，700 份周期性能报告；这是约 1 小时 57 分钟运行，并非全为前台游戏。
- JSONL 当前保留 20:08:39.130–21:34:56.231，共 497,728 个 FrameSample。前段已受文件轮换淘汰；保留段事件 Sequence 连续、无倒序、无 JSON 解析错误。
- WhySoLaggy.log 是追加历史，开头无日期的 21:48、Hooked/Failed 统计属于旧记录，未作为本次启动成功率证据。
- LogOutput.log 记录 WhereIsThing 0.1.3、WhySoLaggy 1.0.5 正常加载，结尾有 Victory/Summited；启动另有 BepInEx “Unable to start Unity log writer”。该文件没有 Unity 异常，不代表无异常，需结合 LandCrab/PEAK/Player.log。
- 10 份 ZIP 均为 Build 1.0.5-dev.20261116.retention；归档文件名使用 UTC，表中时间为北京时间。CSV 有 34 条 CaptureState，但当前只保留最后 10 份 ZIP，不把这 10 份说成整局所有录制。
- WhereIsThing DLL SHA256：5FC795194989B4F0A47FF73B50914642FA86390526CD90EF2268B7D482AFBBC5。
- WhySoLaggy DLL SHA256：97F1320DE575B44034F251F6FBFC2A1A2250BC4BF736C0E89FA2EFDB80AA328E。

## WhySoLaggy：保留改善与剩余缺口

| 录制结束（北京时间） | 实际保留秒 | 帧数 | 缺帧 | manifest 关键事件省略 | 与原始 JSONL 比较缺失事件 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 20:15:12 | 50.010 | 4,833 | 0 | 24 | 278 |
| 20:24:38 | 50.124 | 5,126 | 0 | 0 | 0 |
| 20:28:12 | 50.005 | 4,843 | 0 | 85 | 85 |
| 20:56:00 | 60.249 | 5,499 | 0 | 134 | 134 |
| 20:58:51 | 49.999 | 4,151 | 0 | 6 | 6 |
| 21:06:10 | 51.077 | 4,799 | 0 | 36 | 36 |
| 21:08:51 | 50.009 | 4,422 | 0 | 1 | 1 |
| 21:15:41 | 49.995 | 4,360 | 0 | 23 | 23 |
| 21:26:17 | 72.627 | 8,593 | 0 | 19 | 19 |
| 21:33:15 | 50.008 | 7,025 | 0 | 0 | 0 |

所有包的预录约 19.990–20.000 秒；正式录制约 30.003–52.630 秒。帧序号跨度、实际帧数和 manifest 一致。方法窗口省略为 0；两份包另有 1 / 28 条尖峰方法明细省略，它们是窗口总量的子集，不应重复相加或误作整段方法丢失。

### 已确认：导出省略计数不完整

manifest 合计报告 328 条关键事件省略、0 条明细省略；同 SessionId、同保留事件 Sequence 区间与原始 JSONL 比较，实际少 582 条：NetworkSample 316、RpcCall 244、PluginTiming 8、FpsReport 6、SpikeFrame 6、OwnershipChange 1、AbuseAlert 1。全部缺失发生在触发前，正式采集中未发现额外 Sequence 缺口。

尤其 20:15:12 的包报告关键事件 24、明细 0，实际少 278 条：比报告多 254 条，其中包括 244 条 RPC、8 条插件周期报告、1 条归属变化和1条 FPS 报告。匿名导出保留事件行及 Sequence，只清除/匿名字段，不能用匿名化解释这些整行缺失。

源码 CaptureBuffer.cs 的 EventRing.CopyTo 只复制仍存活的帧与事件，没有将滚动缓冲区省略计数/覆盖边界传递给新录制。当前录制区自身的丢弃会计入，但预录阶段已经丢失的部分无法在 manifest 得知。这是实测证据与源码一致的准确性缺口。修复时需要按实际预录时间窗传递省略证据，不能直接把滚动缓冲整个生命周期的累计计数相加。

### 已确认：关键事件分区容量会排挤诊断摘要

CaptureBuffer.cs 把 NetworkSample 与 ExceptionSummary、SpikeFrame、FpsReport、AbuseAlert 放入共同的 7% 配额。网络样本占多数，原始对照已证实 FPS/尖峰/告警也被排挤。下一步优先分离/聚合网络样本，并保护错误与帧尖峰摘要；不要仅放大全局缓冲。

### 自身开销与磁盘体积

JSONL 保留段的 Gameplay 样本共 466,046 帧，SelfMainMs 均值 0.248571 ms，P95 1.0457 ms，P99 1.6280 ms，最大 85.0457 ms。全部场景有 145 帧自身计量超过 20 ms。例：20:59:23.144 整帧 98.5329 ms，自身 85.0457 ms；20:27:53.575 整帧 90.1179 ms，自身 82.5962 ms。

该字段包含主线程计时探针开销与 MainUpdate / FrameBoundary / HUD 的实测时间；包含所在作用域期间的 GC/调度停顿，不能直接等同于可通过删代码消除的 CPU 时间。当前配置同时手动启用插件和补丁计时，属于较重诊断模式，不代表纯待机开销。应细分主线程冻结、预录复制、帧封口和分配，再做开关对照。

Serialization 和 Flush 在 DiagnosticIO 后台线程执行，其 111 ms / 72 ms 单次最大值不能直接宣称为主线程卡顿；此次不作此归因。保留段 WriterDropped、DiskDroppedBatches、RPC detailDroppedTotal、周期报告 RpcQueueDropped 均为 0。CapturePayloadBytes 峰值 14,683,180（约 14.00 MiB），没有撞到 32 MiB 载荷上限；它不是进程内存。

当前结构化 CSV+JSONL 共约 602.70 MiB（含残留旧会话 CSV）。每种格式分别保留 30 份轮换文件，当前文件另计；350 MiB 配置并非所有格式共用上限。双份逐帧持久化导致 JSONL 已失去本局前约 30 分钟。优化方向是可选单格式/聚合持久化及总容量口径，不是把正常轮换误报为写盘失败。

## WhereIsThing：运行稳定，但周期尖峰可复现

全会话 700 份周期报告仅累加 inclusive observed chain，未与录制窗口或尖峰明细重复求和：

| 方法 | 调用数 | 计时覆盖 | 异常数 | 观测均值 ms/调用 |
| --- | ---: | ---: | ---: | ---: |
| WhereIsThingPlugin.Update | 699,949 | 100% | 0 | 0.117934 |
| WhereIsThingPlugin.LateUpdate | 699,948 | 100% | 0 | 0.000238 |

在已覆盖插件主类生命周期方法中，WhereIsThing Update 的累计耗时最高，但绝对均值仍低，不能据此认定它是整局卡顿主因；子组件、回调、渲染和未插桩方法不在该排名内。

10 份录制的窗口峰值中，Update 最大 14.7521 ms（20:14:43.586，整帧 38.3967 ms），其次 14.6980 ms（20:24:38.403，整帧 36.7375 ms）。多数 8 ms 以上尖峰间隔约 5.02–5.17 秒，跨多份录制重复，说明周期性开销值得定位。

源码 FullValidationInterval=5f 与周期相符；ProcessSceneDiscovery 的 step 0 会在 Statue 范围开启且选择非空时调用 Resources.FindObjectsOfTypeAll<PropSpawner_AmuletStatues>()。本次退出配置确实含 Statue，是优先候选，但现有数据只到 Update，尚不能证明就是雕像查询，也不能排除该流程内其他操作及停顿。建议下一步短期分阶段计时后再做缓存/扫描门控，不直接改行为。

当前退出配置：Enabled=true、Persistent、CurrentSegment、MaxDistance=0、RestrictClientPresets=false，ActiveLocalPresetIds 为 preset-ascent-8,preset-achievement，范围 Ground/Backpack/Luggage/Statue。false 是本次读取到的用户现值，不是默认值变化；代码默认仍应保留 true。本轮不改回。

日志不足以确认未摘/已摘晚安莓是否正确切换、空株是否消失、曼德拉草/根茎和毒蘑菇是否正确、多预设菜单行为、受限客机/换房主/定时扫描矩阵。不能从没有异常推导这些功能全部通过，尤其本次退出时是自由客机+常驻模式。

## 整局性能及第三方干扰

CSV Gameplay 共 634,094 帧，平均帧时 10.35797 ms（按总帧数/总帧时约 96.54 FPS），P95 14.6831 ms、P99 22.3501 ms、最大 383.8335 ms。菜单、载入、失焦、机场单独分组，没有把启动载入 7.47 秒停顿混入前台游戏均值。无同场景 A/B，不能宣称本轮修复带来多少 FPS 提升。

Player.log 中 CompassTapeUI 相关空引用 17,124 条：17,061 条直接在 Update、61 条 UpdatePlayerMark、2 条 Unity transform 异常但堆栈含 Compass。WhySoLaggy 结构化记录也捕获到了持续 Update 错误，说明异常观察器工作。仅看 Compass 主插件 Main.Update 的低耗时/零异常会漏掉其子组件 CompassTapeUI，解释排名必须结合 ExceptionSummary。

其他独立错误：BingBongAI 缺 CavyCrew.GuineaPigsAndTreats 0.9.5 相关 TypeLoadException 1 次；Photon Duplicate key 53 1 次；StormVisual.Start 2 次；WarpOnThrow.OnDisable 12 次；GUIManager.Start 1 次。Player.log 和 LogOutput.log 未见 WhereIsThing / WhySoLaggy 错误堆栈，也未见 GetDisplayName / LOC: 0。部分方法同时挂多个第三方补丁，不单凭 owner 列表指定根因。

保留段 ping 均值约 45 ms、P95 52 ms、最高 79 ms，采样 RPC 队列没有丢弃。RPCA_AddForceAtPosition 在保留段按 NetworkSample 窗口累加 188,803 次；RPC 告警表示超过阈值，不能据此认定玩家恶意或直接推出卡顿原因。

## 建议顺序与未完成项

1. WhySoLaggy：先修预录省略计数及窗口覆盖说明，再处理网络样本挤占关键摘要。当前报告不能被当成“事件零缺失”。
2. WhySoLaggy：对自身主线程尖峰分阶段取证，降低逐帧双格式写入/分配成本；先测量，避免把后台 I/O 最大值当主线程耗时。
3. WhereIsThing：针对约 5 秒一次的扫描进行短期计时，确认具体子流程后优化，保持标注语义。
4. 后续对照测试排查 Compass 异常洪泛，并检查 BingBongAI 依赖；此次没有禁用/更新第三方 MOD。
5. 晚安莓、受限客机/共享/切换、菜单计时功能仍缺直接操作证据。此次是已有一局运行证据的部分验收，非全部功能验收。

## 本地复核产物

- .build/playthrough-20261117/audit_run.py：按 SessionId 扫描 CSV/JSONL、归档与 Unity 异常；保存 audit.json。
- .build/playthrough-20261117/compare_captures.py：按同会话 Sequence 对照 ZIP 与原始 JSONL；保存 capture-comparison.json。
- evidence/：10 份 ZIP、LogOutput.log、Player.log、两个配置快照。原始大型 CSV/JSONL 的位置、大小与时间记录在 audit.json/inventory；没有复制全部大日志。
- review-baseline.json：本次收尾前源码/工程/安装文件/配置的 87 项哈希，供写 memory 后确认未触碰；不是代码回滚基线。
- 仅本地 memory 同步，无外部 update_memory 接口调用。
