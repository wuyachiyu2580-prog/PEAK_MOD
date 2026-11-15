# WhySoLaggy 后续功能与优化调研

日期：2026-11-14。用户确认“现在不会出现了”：记录为本次 LOC: 0 症状的实机通过反馈；不扩展为全部语言、主客机或性能矩阵已验收。用户继续要求调研功能和优化，当前不授权新增代码或发布。

范围：以当前 1.0.5 修复构建为基线，检查现有监测能力、准确性、采集开销和使用体验。正式源码、配置、DLL、发行包保持原状；本轮仅更新调研与 memory。

## 结论与优先级

建议顺序：准确性 → 低开销取证 → 界面与网络扩展。当前 FPS、插件/补丁计时、RPC、异常流量、HUD、CSV/JSONL 已较齐全，优先让结果可信、抓住偶发卡顿，并方便分享诊断材料。

| 优先级 | 项目 | 实际价值 | 类型 / 相对工作量 |
|---|---|---|---|
| P0 | 内存指标、帧归因校正 | 避免把内存变化当 GC 分配，或把错帧/后台等待归给某 MOD | 修正 / 中到大 |
| P0 | 补丁扫描降误报 | 不再把大量共同打补丁的目标当成“冲突” | 修正 / 小到中 |
| P1 | 卡顿前后记录 + 一键诊断 ZIP | 偶发卡顿有上下文，一份文件就能分析 | 新增 / 中到大 |
| P1 | 限时、定向采集 + 自身开销统计 | 平常轻量，深入调查后自动恢复 | 优化 / 中 |
| P1 | 配置实际生效状态、HUD、帧时间分布 | 开关含义明确，排序正确，更易看出顿挫 | 优化/新增 / 中 |
| P2 | 网络 RTT 波动、重传与积压 | 帮助区分链路延迟、本机处理慢及异常流量 | 新增 / 中 |

工作量只是相对判断，不是工期。以上均未实施；下一版版本号未定。

## 已有能力，不应重复列为新功能

- FpsTracker 已有平均/最低/最高 FPS、帧耗时、尖峰和 600 帧窗口。
- PluginProfiler / PatchProfiler 已有被观测调用计时；上一轮已修声明目标规范化/去重、独立计时 owner、主线程切换、并发和旧会话保护。
- HarmonyScanner 已有补丁 CSV 和结构化事件。
- RpcMonitor 已有方法/发送者/估算负载统计、重点方法环形记录、有界队列及队列延迟/深度/峰值/丢弃/消费耗时。默认队列容量 2048，每帧最多消费 32 条。
- NetworkAbuseDetector 已有异常流量/行为检测和周期网络信息。
- StructuredLogger 已有缓冲、约 1 秒刷盘、文件轮转及保留数量/磁盘上限；需要补的是整个采集路径预算，不是从零增加轮转。

## 1. 准确性优先

### 内存口径和旧值残留

FpsTracker.cs:82–88 用 Profiler.GetTotalAllocatedMemoryLong() 的差值计算 AllocRate。该读数不是累计托管分配量，不能据此认为是准确的 GC 分配速率。代码另有明确旧值问题：当内存下降时不更新 _memLastRateKBps，之前的正增长率会保留到下次非负差值。

建议拆分“Unity 已分配内存/有符号净变化”“托管堆使用量”“GC 回收次数”。真正逐帧 GC 分配仅在运行环境存在合适计数器时启用，否则显示不可用。3.1.0 本地存在 ProfilerRecorder / GetAvailable / GetDescription，但类型存在不证明发行游戏提供具体计数器。旧 CSV AllocRateKBps 不可静默换定义，要加 schema 版本或新字段。

### 帧归因与覆盖范围

WhySoLaggyPlugin.cs:423–463 在自身 Update 中采样尖峰、输出明细并 ResetFrameTimers，注释叫“帧末”，实际不是所有插件 Update/LateUpdate/FixedUpdate 的末尾。统计边界可能跨帧；只挪到普通 LateUpdate 也不保证顺序。

建议使用明确 frame id 的双缓冲/已完成帧快照。后台 Socket 接收等待等墙钟耗时应与主线程帧成本分开。补丁计时包含原方法及整条补丁链，关联 owner 不代表其独立增量成本；嵌套调用、PluginProfiler 与 PatchProfiler 会有重叠，不能直接相加或认定某 MOD 占整帧多少 CPU。应显示覆盖范围和未归因部分，不能宣称覆盖所有代码或 GPU。

验收需覆盖执行顺序、多次 FixedUpdate、LateUpdate、递归/嵌套、后台等待与开关切换，核对实际帧标识。

## 2. 补丁扫描：共用目标不等于冲突

HarmonyScanner.cs:42 只输出类型名和方法名，无参数签名；:51 把至少两个 owner 一律标为 Potential conflict，计入自身诊断补丁。

本次 CSV 按当前显示名称分组：664 组目标，644 组多 owner；排除 WhySoLaggy 两个 owner 后 67 组仍多 owner，577 组只剩一个外部 owner。这是显示名分组，可能合并重载，不是精确方法数或已证实冲突数；剩余 67 组也不能直接判错。

建议：完整声明签名/稳定标识、补丁方法/阶段/优先级/before-after、声明类型与反射类型；普通多 owner 显示为“共享补丁目标”，只有明确异常或具体顺序风险才提升警告。诊断 owner 单独标记，不粗暴删掉自身 root 下实际功能补丁。启动清单单独保存，开关变化或用户请求时再生成差异，不做逐帧扫描。

## 3. 最值得新增：卡顿前后记录与一键诊断包

建立按时间与字节双重限额的全局滚动记录，保留尖峰前后上下文，例如前 10–30 秒（设计示例，可调）。支持热键标记/自动触发，设冷却与空间上限；复用现有 RPC 环形记录。

本地 ZIP 包含帧时间趋势、可用内存/GC、已完成帧的调用链摘要、RPC/队列、场景加载/菜单状态、错误指纹汇总、游戏/MOD/依赖版本、构建标识、会话 id、实际模式，以及简短可读摘要和原始 CSV/JSONL。默认遮盖玩家名、房间信息、私人路径和敏感配置，不自动上传。

可增加异常聚合：同一异常次数/首末时间/样例，避免刷屏淹没现场。线程日志回调仅有界入队，不操作 Unity 场景对象，不把自身日志失败再次写回形成递归。首批只做本地，不要求其他玩家安装配套协议。

## 4. 限制诊断自身开销

StructuredLogger.cs:93–118 在调用线程加锁并同步序列化，Tick 在主线程刷盘；LagLogger / AbuseLogger 文本日志 AutoFlush=true，未见同等轮转机制。StructuredFlushMs 只覆盖刷盘部分，不代表总开销；StringBuilder 缓冲路径未见事件字节硬上限。

本次短会话 JSONL 2672 事件中 HarmonyPatchMap 2487 条，约 93% 为启动映射，不能推广为长期游玩的比例。日志记录 1842ms、1512ms 启动尖峰，其中前者靠近启用计时与扫描；没有 A/B，不能把整个尖峰归因于 WhySoLaggy。

建议轻量常驻、指定 MOD/方法限时采集（如 30–60 秒）、高级完整采集三个入口；尊重现有计时默认关闭。结束恢复先前状态，退出/切场景清理。分别测钩子、汇总/序列化、RPC 消费、刷盘耗时，使用 Stopwatch。RPC 在条数上限之外加时间预算；结构化缓冲限条数/字节并记录丢弃，文本日志增加上限与错误降噪。

后台写盘是否有必要须测量决定；后台只处理不可变快照，不能把 Unity API 一起搬过去。先有界、再比较同步/异步方案。

## 5. 配置、HUD 与帧时间

- WhySoLaggyPlugin.cs:299–313：只有补丁计时接入 SettingChanged 安全切换；ShowDashboard、日志级别、插件计时等主要在启动复制值。需逐项标注立即/重启生效，并显示实际运行状态。若插件计时也热切换，使用独立 owner，不解绑 root 全部功能。
- PerformanceDashboard.cs:41–66 每次绘制重新计算均值/拼摘要，文字多为固定英文。可按每秒 2–4 次缓存快照，显示观察范围、记录状态；窗口位置保存/折叠/字号作为体验项。
- RpcMonitor.cs:998 的摘要取字典中前几个非零项目，并非真正排序的 Top。改成明确时间窗内的调用数/估算负载排序；估算负载不能当成实际网络包字节。
- 600 帧在 60 FPS 约 10 秒、300 FPS 约 2 秒。改为明确时间窗口，增加 p95/p99 帧时间、连续卡顿次数/时长；如加 1% low，说明算法，不把 1000/p99 冒充统一定义。
- 增加菜单/加载/游玩标记；保留原始尖峰但分类，避免加载帧混入正常游玩判断。
- 全部新增功能完成后最后统一国际化，覆盖 HUD、配置说明、导出摘要；保持配置键稳定。

## 6. 网络细分诊断

本地 3.1.0 PhotonPeer.cs 确认有 QueuedIncomingCommands/QueuedOutgoingCommands（:257/259）、ResentReliableCommands（:286）、RoundTripTime/Variance（:337/339）、TrafficStatsEnabled（:407）。可低频记录本客户端 RTT/波动、收发积压、UDP 可靠命令重传增量，与插件自身 RPC 消费队列分开显示。

重传次数不是精确丢包率，Variance 沿用 SDK 定义；本地字段不能直接提供所有玩家各自 ping。异常流量/Actor 归因不是作弊定论，应给时间窗和证据。跨主客机同步诊断、GPU 精确瓶颈分类另需协议或实际可用计数器；自动关 MOD/踢人不列首批。

## 建议实施顺序与验收

1. 内存定义/下降处理、扫描措辞与标识；帧归因单独做可验证改动。
2. 在可信数据上加限时定向采集、自开销预算、会话标识、卡顿记录和本地诊断包。
3. 配置状态、HUD/帧时间窗口与网络细分，最后国际化。
4. 同 profile 比较计时关闭/轻量/定向/完整采集，包含菜单、加载、单人、主客机；对比帧时间分布、日志增长、缓冲上限、丢弃、开关恢复、异常。无计数器显示 unavailable，不以零代替成功。

没有测量依据前不承诺 FPS 提升或任意百分比开销保证。

## 反馈、证据与边界

- 用户确认原 LOC: 0 不再出现。19:18–19:19 最新日志显示补丁计时启用，Found 661 / Unique 659 / Merged 2 / Hooked 644 / Failed 0；Player.log 原 GetDisplayName 解析错误 0 次。ShootZombies 继承目标告警仍在，不称所有第三方告警都消失。
- 上述只验证原症状反馈，不扩展为全语言/主客机/性能矩阵通过。37/37 自动测试属上一轮，本轮未重跑。
- .build/whysolaggy-roadmap-20261114/ 保存 WhySoLaggy.log、WhySoLaggy_Abuse.log、harmony_patches.csv、LogOutput.log、whysolaggy_events.jsonl、Player.log、summary.json；本次样本无 RpcCall，不据此判断联机流量表现。
- 安装 DLL SHA256 复核不变：1C04AAF283EE30E9B3CAC923A5C1A431ECE1B46F843AA149B9C4BC2CFA405807。
- 源码目录：MOD开发/WhySoLaggy/WhySoLaggy/；引用文件为 WhySoLaggyPlugin.cs 及 Helpers/ 下 FpsTracker.cs、HarmonyScanner.cs、StructuredLogger.cs、LagLogger.cs、AbuseLogger.cs、PerformanceDashboard.cs、RpcMonitor.cs、PatchProfiler.cs、PluginProfiler.cs。
- 依赖证据：引用参考代码/反编译/3.1.0/Photon3Unity3D/ExitGames.Client.Photon/PhotonPeer.cs；UnityEngine.CoreModule/Unity.Profiling.LowLevel.Unsafe/ProfilerRecorderHandle.cs。公开文档检索未返回可用内容，结论以本地源码/日志为依据，具体计数器实机能力仍待测。
- 仅同步本地 memory；当前无 update_memory 实体接口，不声称外部记忆实体已同步。
