# 2026-11-17 实测问题修复方案调研

用户要求“先调研”。本次只读正式源码、PEAK 3.1.0 本地反编译和上轮实测证据，在 .build/playtest-fix-research-20261117/ 制作独立回放/策略实验。**正式源码、正式测试、用户配置、安装 DLL、发行未修改，未部署或启动游戏。** 下述候选验证不等于正式修复完成或实机性能改善。

前置审计：[本局实测复核](PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md)。

## 推荐结论

1. 先修 WhySoLaggy 的证据准确性：按预录事件时间窗继承省略记录，区分预录损失与录制损失，不能只搬累计计数。
2. 网络样本采用保留原字段语义的紧凑存储，同时从原关键事件配额中分出独立网络配额。实测回放支持“帧40% /锚点3% /受保护摘要2% /网络5% /明细50%”，不增总容量、不挤占原明细配额。这是候选起点，不保证任意洪泛下永不丢失。
3. WhySoLaggy 自身先减少已确认的重复分配并补充分阶段计量；WhereIsThing 先计量场景步骤和网络清理，再实施有失效机制的缓存。现有证据不足以直接把85ms归为某段算法，或把5秒尖峰全部归为雕像。
4. 磁盘记录改为可选输出策略，保留旧配置，不在本轮或后续默认迁移时静默关闭用户已经启用的计时/CSV。

## 一、原问题已在生产缓冲逻辑中逐条复现

隔离 baseline 使用当前 CaptureBuffer.cs、CaptureTiming.cs、StructuredEvent.cs 原样文本；CapturePayloadBudget 提取生产实现，EventCodec.Size 与生产公式一致。把本次 SessionId 的原始 JSONL 逐条喂入4MiB滚动缓冲与8MiB录制缓冲。

10/10 份 baseline 回放与实际 ZIP 的 **Sequence 集合完全一致**，没有多行或少行差异；53,651 帧、328 条记录区关键事件省略也一致。因此候选比较依赖的是实际缓冲语义，而非另写一套近似算法。

实验限制：运行在 .NET 10，不在 Unity Mono；未复现已冻结/导出中记录的全部并发生命周期。实测载荷上限未触顶，因此这个范围足以复现本次容量淘汰，但不能作为跨平台性能或高压内存验收。

| 隔离候选 | 相同原始区间缺失行 | 相比原ZIP新增缺失 | 帧数 | 结论 |
| --- | ---: | ---: | ---: | --- |
| 生产 baseline | 582 | 0 | 53,651 | 逐条匹配现有10份ZIP |
| 仅新增7%网络分区、明细50%降到43% | 564 | 179 | 53,651 | 否决：虽然补回摘要，却排挤更多RPC/方法明细 |
| 紧凑网络、沿用原7%共同关键分区 | 253 | 0 | 53,651 | 能修本次容量问题，但洪泛仍可能互相挤占 |
| 紧凑网络 + 原7%拆为2%摘要/5%网络 | 253 | 0 | 53,651 | 首选候选：无新增丢失，额外保护错误/尖峰摘要 |

首选在相同原始比较范围补回 **329 条**：NetworkSample 316、FpsReport 6、SpikeFrame 6、AbuseAlert 1。回放结果的 actualDifferenceExtra 合计330，其中一条在旧导出范围边界之外；统一口径应报告同范围329条，而不是混用330与582。

剩余253条均是第一份包预录中已被明细容量淘汰的 RpcCall 244、PluginTiming 8、OwnershipChange 1。候选依然会打印零DroppedDetails，因为省略继承尚未实现；不能据此宣布“全部事件已完整”。

首选候选还完成：

- 3,297条保留NetworkSample逐字段还原校验，0差异；含原有整数/浮点/空值/缺字段。
- >2^53整数、long.MaxValue、浮点、null及不存在字段的边界还原检查通过。
- 5组隔离压力检查：网络洪泛保住异常/尖峰；清理释放身份及载荷；共享预算拒绝计数；自然过期不算容量丢失；Detach/导出引用最终释放平衡。
- 最终首选实验构建0警告0错误；结束时共享预算为0。较早baseline/第一版候选有4条实验元数据未赋值警告，已在公共harness赋值消除，未修改生产声明。

## 二、省略计数的可靠修法

### 根因与计数范围

CaptureBuffer.cs 的 RemoveFrame/RemoveEvent只保留累计Dropped值；CopyTo只搬存活数据，没有搬丢失证据。CaptureController.Begin将滚动记录复制到录制后，manifest只看到后者的损失。

不能直接 destination.Dropped += rolling.Dropped：滚动缓存长时间运行过，累计损失包含早于所请求20秒的事件。也不能按“淘汰发生的时刻”计数：一个19秒前的事件在现在被淘汰，它的归属仍应由原事件时刻决定。

推荐增加独立的有界LossLedger：

- 记录被淘汰/拒收事件原MonotonicSeconds、类别、原因；区分 Capacity、Oversize、SharedBudget 等。自然20秒滚动过期不是容量损失。
- Begin固定请求区间[started−20, started]，复制预录时只继承该区间损失的只读快照；开始后损失另计。明确PreRoll/Recording阶段，不能与异步写盘Dropped相加混称同一种丢失。
- 使用固定类别×固定原因×128个1秒桶，空间封顶；桶保留首末事件时刻和计数。完全落在区间内的桶精确计数，跨边界桶给出下界/上界。不能把不确定值硬写成0。
- 超过journal覆盖或未知schema时用Unknown/Incomplete，导出上下界及计数质量；总窗口字段与各类别实际保留范围分开。
- manifest新增PreRollStart/End、LossCountQuality、按阶段/类别/原因的计数范围，summary同时说明。旧Dropped字段如果保留，明确为兼容的录制区计数，并增加新统计schema版本；不悄悄改变旧字段定义。
- 新ledger/共享身份及容量也纳入内存预算；clear/session/reset/连续录制/导出引用处理都须覆盖。

14项独立策略检查通过，包括跨边界不伪精确、过期不带入、原事件时间归属、重复查询、不随滚动后续写入变化的继承快照、超长运行空间边界及未知范围。**这是Python策略验证，尚未接入生产EventRing，也未证明正式线程/内存实现。**

如果正式实现坚持输出精确单值，需要额外有界精细时间索引和溢出状态；不能用不受限逐条List换取精确度。首选允许置信上下界，UI直接显示“至少N条/最多M条/不完整”。

## 三、紧凑网络样本与关键事件保护

NetworkSample有两类：按RPC方法每窗口的Count/Rate/ActualWindowSeconds，以及全局Ping/队列/采样/CPU-GPU状态。后者的空值表示不可用，不能变成0；Rate与窗口必须原样保留，不能把已有1秒汇总再次相加当作逐次调用。

当前关键区里原始Dictionary账面载荷过大，网络样本挤掉FPS/尖峰。研究版仿照现有CaptureTiming，把已知数字存储为long位/双精度位，固定字段presence/null掩码，字符串身份共享，导出时重建原字段。这不改变事件数量或网络统计粒度。

正式实施边界：

- 独立NetworkPacked类型更清晰，不把研究版复用CaptureTiming类的实现直接整体复制进去。
- 未知字段/类型必须回退通用事件路径并计费，不能静默删掉未来字段。
- 保护ExceptionSummary/SpikeFrame/FpsReport/AbuseAlert，保留锚点分区；NetworkSample自己的丢失单独显示。研究阶段仍共用旧DroppedCritical字段，正式应补细分类。
- 不降低原明细50%配额；仍接受有界丢失，但必须正确披露。
- 共享字符串key在当前实验里沿用LINQ排序，虽有容量收益，仍有主线程临时分配；正式可用固定schema身份key，需保证相等关系及字段升级兼容。
- 不移动Unity/Photon读取到后台；后台只处理冻结的纯托管数据。

## 四、WhySoLaggy自身尖峰：先做哪些，不能承诺哪些

实测最大几个self尖峰分别出现在FrameBoundary或MainUpdate/RpcPump。其对应10秒报告有1–2次GC；有时后台Serialization也出现相近长耗时。这支持全局暂停/分配压力作为候选解释，但报告粒度太粗，**不能证实GC与单帧一一对应，也不能将85ms视为纯插件CPU执行时间。**

已确认的高频分配点：

1. HookSession.cs SelfMetrics.Add每次更新都Tuple.Create；帧、HUD、后台序列化都会更新。隔离提取原类，100万次约40,000,040 bytes；锁内复用单个可变Metric对象约40 bytes（测量器本身），并发10万次累计、Snapshot/reset、主线程帧范围计数保持一致。
2. 比较了值类型字典回写与复用对象：本机单轮约57.4ms/103.8ms/27.2ms（原/值回写/复用）。这仅帮助排除“零分配一定更快”的假设，不作Unity性能承诺；首选保留锁、复用对象，不贸然去锁。
3. TimingLedger.Seal每帧分配FrameRecord、List及多个MethodFrame；当前Completed只有CaptureController订阅，消费者同步使用。但若复用列表/值结构，必须审计尖峰top引用、方法窗口身份、导出冻结和停止录制边界，不能让异步记录引用被下一帧改写。建议次阶段处理。
4. StructuredLogger.WriteEvent主线程Freeze字典、Capture回调和Size计费，后台才做CSV/JSON序列化。紧凑Frame直接通道可进一步减少分配，但涉及身份/Sequence/线程所有权；先保留准确性再做。

建议临时诊断字段：Seal、统计窗口排序、FrameFreeze/Pack、RollingAdd、RecordingAdd、PreRollCopy、TimingWindowFlush、RPC pump、HUD；所有数字先在固定结构聚合，不逐方法每帧打印。仅显式调试开关启用，限时30–60秒自动停止，报Count/Total/Max/P95及GC.CollectionCount差分（计数只表示附近发生，不等于暂停时长）。嵌套阶段不可重复相加。

统计/持久化优化建议分批：

- 先完成SelfMetrics复用及网络紧凑存储，复用现有线程安全/计数语义。
- 增加可选StructuredDiskFormat（Both/Jsonl/Csv），旧配置保留Both；诊断ZIP及内存完整帧保留独立于持续写盘模式。
- 再考虑常驻磁盘仅写窗口摘要、触发录制输出完整帧；必须在schema与Coverage中标明降采样，不将意图省略伪装为写盘丢失。
- 总容量策略若加入新选项需保留旧“每格式上限”解释；后台仅轮换自己创建的文件，不连带删除用户诊断包或其他MOD日志。
- 本轮不改用户ManualPluginProfiling/ManualPatchProfiling，不将当前重诊断模式数据冒充待机成本。

## 五、WhereIsThing周期扫描方案

统计10份录制窗口的Update峰值≥8ms，有67个峰；同一录制内57个间隔，56个落在4.5–5.3秒，中位5.051885秒，剩余一个为0.213秒。这是很强的周期性证据，但只有Update包络。

### 同时排查的路径

- ProcessSceneDiscovery：20个步骤各占一帧，周期5秒。当前Statue scope且物品选择非空，step0执行Resources.FindObjectsOfTypeAll<PropSpawner_AmuletStatues>()，并逐个GetComponentsInChildren/身份解析；CurrentSegment过滤发生在后续，并不阻止全局发现的成本。
- CleanupNetworkObjectsIfDue：同为FullValidationInterval=5f，扫描整个_networkObjects，移除时可清标签/重试记录；它在Photon迭代结束处执行，结束分支没有把这次清理拆进0.75ms迭代预算。因此不能只盯住雕像。
- ShouldScanRootPlants仍遍历目录做分类门控；不同选择可能开启根茎/晚安莓/其他场景FindObjects。必须记录当时有效选择和step编号，不用退出cfg当作全程配置。

### 优先修法与兼容约束

1. 先加限时聚合阶段计量：网络发现、重试、网络清理、20个场景步骤、标签刷新/投影。记录对象数/标签数/有效选择代数，保留整体Update对照。
2. 若证实雕像查询：建立按sceneGeneration的spawner缓存；扫描目标对象列表与检查果实/活动状态分开。缓存子节点/FakeItem/prefab绑定，节点换代、child变化、prefab变化或pickedUp变化时重新解析。
3. 场景加载/卸载、附加场景、LevelGen完成、显式重扫及低频保底重新发现共同维护；不能只在sceneLoaded查一次，因为PropSpawner.Execute/Go稍后生成或重建子物体。
4. 不直接缓存statueIndex为最终护符：实际GetPropToSpawn使用(statueIndex + statueSpawnOffset) % props.Length，继续以当前FakeItem.realItemPrefab和现有后备解析为准。
5. FakeItem.PickUpVisibly隐藏，UnPickUpVisibly会重新激活；缓存不能永久删除已采摘但仍存活的对象，也不能因保留spawner而给空株恢复标注。
6. 改成FindObjectsByType并非完全等价替换。Unity官方说明它排除资源资产及HideFlags.DontSave对象；原Resources路径可看到更广范围。需要实机比对候选集合，最好首次/保底保留兼容发现，再通过scene缓存摊销，不能只为省时删掉类型覆盖。
7. 若证实网络清理：在完整发现代数结束后分批清理，冻结cleanupGeneration并避免与新发现混用，不能让LastSeenGeneration比较把仍存在对象删掉。把数量/时间预算纳入清理路径，保留View/绑定有效性检查。

不采用：把全局FindObjects放后台线程；仅将间隔从5秒拉长掩盖尖峰；简单只查当前段而破坏动态物/附加场景覆盖；未经身份验证替换护符索引；盲目删除延迟分类次数。

## 六、正式实施与验收顺序

1. LossLedger和导出字段/summary：精确/上下界/未知三态，带预录窗口；原旧字段兼容，三语言说明最后同步。正式测试覆盖淘汰时刻≠原事件时刻、边界桶、90秒录制/20秒预录、切房、连续录制、导出中冻结记录。
2. 独立紧凑网络记录及2%/5%保护：字段未知回退、null、大整数、共享身份生命周期、失败回滚、总32MiB边界、多份保存/并发导出；再用同批实测记录逐条回放。
3. SelfMetrics复用对象和限时阶段计量：保持锁和非重复归因。Unity Mono实测同场景开关对照后再评估帧记录/磁盘策略。
4. WhereIsThing先阶段计量，再按证据选缓存或分帧清理；根茎/曼德拉草、晚安莓采摘前后、雕像重建/隐藏恢复、CurrentSegment/WholeMap及共享矩阵要回归。
5. Compass异常作为独立对照因素；本轮没有修改它，也不把关闭它混进这两个MOD的性能改进结论。

原78项WhySoLaggy、281项WhereIsThing是上次正式验证；本轮没有重跑，不将本次14+5+字段回放检查混写成新的正式全量测试数。

## 产物与来源

- .build/playtest-fix-research-20261117/baseline-result.json：原10包逐Sequence一致性。
- partition-result.json：被否决的简单分区方案。
- compact-result.json、protected-result.json：紧凑候选对照及压力结果。
- research-summary.json：统一同区间口径582→253，补回329，无新增原事件丢失。
- policy_probe.py / policy-result.json：14项有界时间窗计数策略检查。
- metrics_probe/ 与 metrics-result-final.log：生产SelfMetrics提取、两种候选、分配/并发/计数验证。
- 源码文件仍在MOD开发；研究副本不得直接整目录覆盖生产。
- Unity官方6000.0 API页面已缓存为Resources.FindObjectsOfTypeAll.html、Object.FindObjectsByType.html及解析文本，供核查API差异；服务器web检索未返回可用内容，补充通过终端读取官方文档，未引用第三方推测。
- 本地游戏依据：引用参考代码/反编译/3.1.0/Assembly-CSharp/Peak/PropSpawner_AmuletStatues.cs、PropSpawner.cs、FakeItem.cs。
- source-before.json：87项生产源码/工程/安装DLL与PDB/cfg指纹；收尾再次核验，无修改。只同步本地memory。
