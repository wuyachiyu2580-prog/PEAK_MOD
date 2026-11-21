# 2026-11-18 实测问题修复实施与验收

## 2026-11-18 已部署 2.0.a，等待实机验证

按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。

下一步在2.0.a启动游戏，进入实际扫描场景后开启两MOD的Diagnostics/PhaseTiming；默认45秒自动关闭。先保留现有配置做对照，再读取阶段报告定位5秒尖峰。自动测试仍为WhySoLaggy 105项、WhereIsThing 184+104项；本次仅部署，未重跑测试。

[部署前后哈希与回退备份清单](../.build/deploy-playtest-20261118-210002/deployment.json)。回退时先关闭游戏，只将备份plugins目录中的4个文件复制回目标；cfg备份仅供保留，不覆盖用户后续设置。下文记录的是实施阶段的隔离验收状态。

用户在修复调研后回复“开始”，11月18日要求“继续”。本次已完成代码、正式回归、同批日志回放与隔离 Release 构建。下方研究阶段的“未实施”状态已被本报告覆盖，但历史证据保留。

代码和构建标识保留本轮开始日期：WhySoLaggy 1.0.5-dev.20261117.playtest、WhereIsThing 0.1.3-dev.20261117.playtest；插件/程序集仍为1.0.5/1.0.5.0与0.1.3/0.1.3.0。2026-11-18为最终验收日期，不能把11月17日的实测日志误写成新版本实机结果。

## 已实施

### WhySoLaggy

- CaptureLossLedger按被丢事件原MonotonicSeconds记录，5类别×3原因×128个1秒桶。类别为Frame/Anchor/Protected/Network/Detail，原因Capacity/Oversize/SharedBudget；自然时间过期不算容量损失。
- Begin请求预录范围固定为[started−20, started]，只继承一次独立快照；完整桶精确计数，边界桶给上下界，历史覆盖不足或账本分配失败标Unknown，UpperBound为null。不会把累计滚动损失全搬入新录制。
- Recording统计保留原兼容口径：目标缓冲在复制预录和正式录制期间发生的损失，含复制拒收；与继承的PreRoll不重复。旧Dropped字段不含继承损失，Network是旧Critical的子集。新的manifest.LossStatistics使用独立schema=1，事件schema仍为2。
- summary.txt和loss-summary.txt说明范围、上下界、未知与旧字段口径，支持英/简/繁；manifest按阶段、类别、原因输出。
- 独立CaptureNetwork使用固定数字列、类型/存在掩码和共享字符串身份。保留大整数、ulong、原数值类型、浮点、null及缺字段；未知字段或不支持类型回退通用记录，不删除字段。共享身份使用长度前缀避免分隔符碰撞。
- 分区为帧40%（含固定账本与快照预留）、锚点3%、保护摘要2%、网络5%、明细50%。滚动4MiB、录制8MiB、共享载荷32MiB上限不变。账本及快照预留65,792 bytes计费，小缓冲不足时明确Unknown。释放/Detach/导出引用覆盖正式回归；这是载荷账面上限，不等于整个CLR堆占用。
- SelfMetrics保持锁和原计数语义，更新时复用Metric对象，消除每次Tuple.Create分配；未据此声称Unity Mono帧率提升。
- 新增Logging/StructuredDiskFormat=Both|Jsonl|Csv，默认Both。主线程请求按IO队列顺序切换，先刷旧缓冲，失败时保留旧策略并等待重试；各格式轮换语义保持。内存采集和ZIP中的JSONL/CSV独立，三种格式均通过ZIP内容/匿名化回归。
- 阶段计时覆盖Update、FrameBoundary、Seal、Statistics、Freeze、RollingAdd、RecordingAdd、PreRollCopy、TimingWindowFlush、RpcPump、Hud、Pack。Pack是EventRing.Add包络，包含到期清理/压缩/入队，不应当作纯编码时间。
- 首次阶段日志在IO线程创建目录后写入，避免尚未生成ZIP时目录不存在；正式测试覆盖此路径。

### WhereIsThing

- 新增同规格限时聚合计时：整体Update、网络发现、延迟分类重试、实际网络清理、标签刷新/投影、Scene00至Scene19全部场景步骤。
- 记录峰值对应的已索引对象数、标签数、有效选择代数、sceneGeneration、选中物品数、位置范围与地图范围。这些是采样时上下文，不是每个步骤的逐对象处理计数。
- 本轮没有改为雕像缓存或分帧清理；5秒尖峰的具体子方法仍待实机阶段数据确认。晚安莓未摘/已摘统一入口、果实锚点/空株隐藏、根茎/曼德拉草、共享/客机限制规则保持。

### 两个MOD计时共同约定

Diagnostics/PhaseTiming默认false；开启一次窗口，PhaseTimingSeconds默认45，允许30–60秒。到期自动关闭配置，重新开启才再次采样。每次窗口固定内存聚合Count/TotalMs/MaxMs/P95UpperMs，不逐帧输出日志；P95UpperMs为直方图上界，GC.CollectionCount差分仅表示次数，不能转换成暂停时长。阶段包含嵌套，不可相加当总耗时；计时器开销与调度暂停也会进入墙钟测量。

WhySoLaggy输出BepInEx/WhySoLaggy/phase-timing.log并写插件日志；WhereIsThing输出BepInEx/LogOutput.log中的PhaseTiming报告。阶段报告不自动加入匿名ZIP。

## 验证结果

| 验证 | 最终结果 |
| --- | --- |
| WhySoLaggy .NET Framework 4.7.2 MSTest | 105/105通过（原78项保留） |
| WhereIsThing helper回归 | 184/184通过 |
| WhereIsThing生产状态流程/计时回归 | 104/104通过（原97项保留） |
| 两个真实游戏依赖Release构建 | 均0警告、0错误 |
| 正式缓冲代码回放 | 10份录制，53,651帧，旧事件新增丢失0 |
| 网络样本逐字段还原 | 3,297条，差异0 |
| 回放压力检查 | 5组通过，最终载荷预算0 |

同原始比较范围的缺失由582降至253，补回329条（NetworkSample316、FpsReport6、SpikeFrame6、AbuseAlert1），没有排挤原有明细。第一份包剩余253条预录明细缺失，新账本给出211–262条Bounded范围，实际253位于其中；其余9份预录损失Exact 0。此处不将新旧导出边界差异的额外1条计入329。

正式回放直接链接六个当前生产源码文件，预算类和Size公式从生产提取。回放在.NET 10运行；正式单元测试、逻辑替身和静态Release构建均不代替Unity/Photon实机验收。帧率、GC暂停改善和WhereIsThing周期尖峰消除仍未证明。

## 产物及保护核验

隔离输出位于工作区.build/playtest-fixes-20261117/：

- whysolaggy-release/WhySoLaggy.dll及.pdb；DLL与执行105项测试的whysolaggy/bin/WhySoLaggy.dll逐字节一致。
- whereisthing/bin/WhereIsThing.dll及.pdb。
- results/保存历次TRX；以verification.json指向的最终105项成功记录为准。较早的失败记录保留排错轨迹。
- whysolaggy-tests.log、whysolaggy-build.log、whereisthing-build.log、wit-regression.log、stateflow.log。
- replay-result.json、replay.log、replay/；verification.json含源码/产物/受保护文件哈希与结果汇总。
- backup/与baseline.json保存本轮开始时的已有改动。没有重置Git HEAD或覆盖上轮未提交工作。

| 文件 | bytes | SHA256 |
| --- | ---: | --- |
| WhySoLaggy.dll | 279040 | EE36793F76ABF3DCCDB1B2EC430EBA96B10A32D6D583578693C8CD62B5EF567B |
| WhySoLaggy.pdb | 76712 | C1BC328EF8BB6C7300CB78BD6882D4DA887B3DB101E043A0A21E60B817B500C6 |
| WhereIsThing.dll | 197632 | ED6043D4A73A4DC1EFC031776C58A6C99FF890768F41E79993D52BC70E43D794 |
| WhereIsThing.pdb | 59856 | 64A4E7D1B44A15CCA2664107D3F0E296701543C5BE1528C529F0571B9A622775 |

收尾核对66个已安装DLL/PDB/cfg及历史发行文件全部与本轮基线一致。本轮未部署到2.0.a、未启动游戏、未改用户配置、未提交或发行。客机配置RestrictClientPresets=false保留，代码默认仍true。安装目录中的旧dev构建仍是上一局运行版本。

## 下一轮实测

1. 使用本报告列出的隔离DLL/PDB安排部署；确认版本后，再在游戏内开启限时阶段计时。当前尚未部署，旧安装版本不会出现这些新配置。
2. 保持原重诊断/共享/选择配置，先运行一段PhaseTiming关闭的对照；不要同时改变ManualPluginProfiling、ManualPatchProfiling或持续日志格式。
3. 在实际扫描场景把两MOD的Diagnostics/PhaseTiming设为true，45秒足够覆盖多次5秒周期；采集结束确认自动变false并保留PhaseTiming报告。不要仅在启动菜单开启导致进入场景前已超时。
4. 对比Scene00Statues、NetworkCleanup、其他场景步骤与Update包络，查看峰值上下文；确认根因后再选sceneGeneration缓存或分批清理。重复一轮关闭计时对照，避免把诊断开销当成常驻成本。
5. 做一份30秒手动录制（需要时延长/触发60秒），核对20秒预录、连续帧、LossStatistics及独立Network损失；网络/异常洪泛下允许有界丢失但必须披露。
6. 继续检查晚安莓采摘/恢复/空株、护符隐藏恢复和延迟生成、CurrentSegment/WholeMap、客机本地/共享预设及菜单开关。Compass异常作为独立因素记录，不混作本轮修复效果。

参考：[实机复核](PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md)、[修复方案调研](RESEARCH_PlaytestFixes_2026-11-17.md)。只同步本地memory。
