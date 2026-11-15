# WhySoLaggy 开发构建实施记录

更新：2026-11-15；本条覆盖11月14日“只调研”状态。用户已授权完整计划，后续确认游戏关闭，可以安装开发构建。

## 交付状态

- 源码、隔离开发DLL/PDB已完成；70/70自动测试通过，Release构建0警告、0错误。
- 构建标识：1.0.5-dev.20261115。插件版本1.0.5，程序集版本1.0.5.0，GUID和原配置键保持稳定。
- 已安装：2026-11-15T13:08:31.5468217+08:00。游戏进程检查为空；本轮没有启动游戏。
- 目标：C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx/plugins/WhySoLaggy.dll与WhySoLaggy.pdb；递归检查仅一份WhySoLaggy.dll。
- DLL SHA256：90BF430A3838D0B7C74738554D46C261886E1EE17E6172A2238C2952471DB139。
- PDB SHA256：09551B1CF7BA9BE6AFD6F864F839005DF320CADC324DE72A26BCD5C571916E21。
- 安装前DLL SHA256：1C04AAF283EE30E9B3CAC923A5C1A431ECE1B46F843AA149B9C4BC2CFA405807。
- 回退目录：C:\Users\Administrator\Desktop\MOD\PEAK\.build\whysolaggy-dev-20261114\rollback-20261115-130831。原安装无PDB，回退旧DLL时需移走本次新增PDB。
- 安装前后配置SHA256：74DC09BA8CC37665C9FD4E4FE67F5C412096F2C4481E82615E4613D83F945B60，逐字节未改变；启动新版后BepInEx将按惯例补充新增项。
- 构建始终显式覆盖工程profile OutputPath，输出到.build/whysolaggy-dev-20261114/bin/；未更新历史发行目录、未上传、未提交Git。
- **真实联机性能对照、30分钟四人稳定性、完整ModConfig三语言界面验收仍待进行。自动测试不替代这些验收。**

## 已实现

1. **准确性**：当前PlayerLoop Initialization首节点帧协调；自有节点移除/恢复；方法规范化去重；Prefix/Finalizer正常及异常一次记账；背景耗时单列；入口采样；主线程区间并集覆盖和未归因；内存有符号净变化、受支持GC指标及unavailable；30秒分位数/1% low；加载/机场/游戏/失焦/切换状态。
2. **管线**：RPC入口总量计数和方法/Actor明细限流；保留原配置条数上限，额外实际载荷4MiB；0.5ms软消费预算；有限参数副本；房间/连接/场景代次；实际速率分母；异常聚合；4096条/4MiB后台写入；文本/结构化轮转；磁盘退避与有界缓存；退出最多2秒等待。
3. **自动深入**：20秒滚动证据；卡顿触发；插件约5秒观测后按整窗平均或同帧尖峰选最多3候选；补丁方法程序集核验关联，最多50目标；组合安装每帧最多2操作/2ms软预算；单操作>20ms停止扩展、保留基础记录；30秒默认、60秒封顶、120秒冷却、10次/小时；用户手动设置优先恢复。
4. **用户功能**：四页IMGUI、4Hz快照、滚动/折叠/字体/位置；Ctrl+F8/F9/F10；录制/标记/停止/导出/目录/清单刷新/规则重载；RTT、SDK方差、队列、可靠重传增量；可选CPU/GPU有效样本；自身累计/最大及主线程p95/p99。
5. **诊断包**：临时文件成功改名ZIP；summary、manifest、events.jsonl、frames/calls/network/exceptions.csv、补丁清单；默认匿名，Actor+代次别名；自由参数/探针/路径省略，异常保留符号栈；完整身份/原始日志每次明确选择；10份/500MiB，只轮转自有文件，不上传。
6. **国际化最后完成**：50项英简配置说明、英简繁HUD/状态/导出说明；繁中使用从本机zh-TW生成并校验的源码映射，不依赖运行时Windows接口；多MOD首页/泛型继承回归通过。

## 新增测试发现并修复

- 旧事件enum数值曾受新增类型前置影响：改为末尾追加，历史0–11不变。
- 当前Harmony UnpatchSelf按回调身份清除，两个重载代次共享回调会误删旧owner：改为逐目标Unpatch(All, exactOwner)，失败回退和连续重载回归通过。
- 后台等待混入主线程成本、并发快照单字段清零、固定1024条近似4MiB：分别修正为线程归属、关联计数锁内快照、真实载荷上限。
- 共享父类插件生命周期按实例类型归属；自动候选用整段观测帧作分母。
- 原LOC:0修复保持；共享泛型、继承反射顺序、后加翻译补丁、外部异常、递归、跨代次均回归。

## 容量与边界

- 滚动/单录制各最多16MiB；最多两个冻结记录。记录/导出合用保守**32MiB载荷池**，正在导出的引用仍计入，为其他队列留余量。超限省略并记录，不能保证每份都填满16MiB。
- watched名单全保留，明细合计最多1024条，说明已注明；入口计数独立。聚合键超限进入overflow，保留总数。
- 补丁清单约512Ki字符软上限，超出标unavailable；清单扫描、显式规则重载和单次Harmony操作不能抢占，需实机检查停顿。
- 自适应减少样本量，合成测量**没有显示稳定提速**，不作速度承诺。
- inclusive耗时和关联owner不等于根因；未归因不等于空闲；队列丢弃不是网络丢包。
- 现有InstantiateTrace有限采栈保留；自动流程不启用任意MethodTracer/FieldProbe/全量栈。
- 规则重载失败保留相应模块有效旧钩子。两个独立模块不声称全局原子事务，失败状态可见。

## 验收

| 项目 | 结果 |
|---|---|
| 原37项协议/版本/配置/事件兼容和真实Harmony/ModConfig回归 | 通过 |
| 新33项统计/异常/并发/背景/容量/突发/匿名ZIP/触发恢复/重载/主循环/语言/三轮合成基准 | 通过 |
| 合计 | 70通过，0失败，0跳过 |
| 隔离Release DLL/PDB | 0警告、0错误 |
| 安装/配置/单DLL | 通过 |
| 真实菜单、三语言切换/搜索/重绑定、无ModConfig启动 | 待实机 |
| ProfilerRecorder与CPU/GPU实际可用性 | 待实机；无样本显示unavailable |
| 五模式各三轮三分钟同场景A/B | 未执行 |
| 30分钟四人、场景切换、离房重连、长期内存/句柄/文件趋势 | 未执行 |
| 磁盘故障与全量模式实机压力 | 合成覆盖部分，实机待验 |

## 证据/使用/回退

- 测试：.build/whysolaggy-dev-20261114/results/final-verification.trx。
- 合成测量与未执行的实机矩阵：.build/whysolaggy-dev-20261114/PERFORMANCE_AND_ACCEPTANCE.md。
- 安装：.build/whysolaggy-dev-20261114/installation.json。
- Ctrl+F8面板，Ctrl+F9录制30秒，Ctrl+F10标记；快捷键可修改。
- 输出：profile/BepInEx/WhySoLaggy/diagnostics/。
- 退出游戏后复制回退目录WhySoLaggy.dll覆盖plugins根目录，并移走新版PDB；配置无需回退。
