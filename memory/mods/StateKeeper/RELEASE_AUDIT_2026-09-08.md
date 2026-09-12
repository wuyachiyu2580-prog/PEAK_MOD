# StateKeeper 0.1.0 发布前自审

> 2026-09-09：本包已追加英文按钮和重命名修复，最新产物哈希见`temp/2026-09-09.md`。下方哈希/包大小为09-08归档；当时静态自审未发现原版按钮持久Resume监听和父Canvas排序，现已通过实际资源读取确认并修复。

日期：2026-09-08。用户要求“先发布一版，发布前自审，检查废弃代码和参数”。沿用未正式发布的0.1.0，不擅自升版。

## 自审发现与处理

1. 已修复：General.Enabled=false原先只停Update采样，Harmony物品/跳跃等入口仍可能写记录。现在事件入口、内部AddEvent、绑定与采样均检查开关，初始化先应用配置；重新启用已有局会记录中断并清空跨中断的库存/状态缓存。已开始的局仍正常保存、封存，不因关采集丢数据。
2. 已移除：RunCollector.ToggleFavoriteLatest、RunStore.ToggleLatestFavorite、GetHistoryEntries。前两者来自旧F8流程；历史页已按runId操作。测试改为使用生产ToggleFavorite和最近/收藏列表接口。
3. 已移除：图表旧SetPoints单曲线回退、从未传入的bands参数/绘制分支、始终被设为-1的cursor支路；当前多曲线和事件刻度保留。
4. 已移除：AddAxisChart中随后必被覆盖的OnSelected默认回调和OpenTimeEvidence。两个真实图表调用者仍用各自数据点的原始引用打开证据，不再有从最近无关事件借来源的回退。
5. 已移除：ItemAttributionEngine.Interrupt/Reset的未使用reason参数、ReportAnalysis.Track的dt参数、AddStatusSnapshot的player参数、ModifyStatusOutcomePatch.Postfix的__instance参数。所有实际调用点同步清理。
6. 已修复：性能汇总和慢UI重建日志原先未受DebugLogging约束。现在默认不输出，排查时打开Advanced.DebugLogging；一般错误和局生命周期日志仍保留。
7. 已修复：发行草稿manifest/CHANGELOG旧名PeakRunAnalytics、根README旧schema2/F8/无面板说明。发行与根文档同步为StateKeeper现状，保留用户刚写的英文创作缘由。

## 明确保留

- Unity/Harmony反射入口：Awake/Update/LateUpdate/OnDestroy/Prefix/Postfix，以及OnPointerClick/OnSelect/UIPage接口回调。不能因为没有普通C#调用而删除。
- StatsModels/AnalysisModels等序列化字段：存在历史schema3读取、定义目录、诊断或跨局指标合同；本次不按文本引用次数删除数据字段。
- CaptureItemResourcesForTests：真实单元测试仍调用，用来验证两类Used数据和HasData等语义，不属于废弃代码。
- 三个配置项均有实际消费者；未增加新参数、RPC或采样频率。
- 参考游戏程序集只用于构建，发行包不携带Unity/Photon/BepInEx/Harmony/Newtonsoft等依赖DLL。

## 验证

- dotnet format style仅检查IDE0051/IDE0052/IDE0060、IDE0005：清理后无诊断。另用实际源码引用搜索核对内部接口、框架入口和测试使用情况；不能宣称静态分析发现了全部可能的动态问题。
- 严格Release构建：0 warnings / 0 errors。
- 64项测试全部通过，0失败/跳过，含新增禁用事件入口与恢复中断检查。测试报告：MOD开发/StateKeeper/.build/test-results/statekeeper-release.trx。
- 最新局回归仍为168 chunks、28500 samples、1676 inventory、38674 events；8确认死亡/14原始/6重复；127时间回退/6进度点。此次只读回放4865ms，不等于Unity帧耗时。
- ObserveSync直接调用Unity原生方法，不能在普通.NET进程执行；其禁用分支已源码检查，仍需短时实机验证，未用测试替代Unity验证。
- icon.png为可重复生成的256x256本地图表/状态条图标，已查看输出；不包含游戏截图、玩家身份或外部图片。
- ZIP严格白名单：StateKeeper.dll、manifest.json、README.md、CHANGELOG.md、icon.png。已逐个校验压缩前后SHA256，不含测试DLL/PDB、配置、日志、原始局或开发脚本。

## 产物与部署

- 发行目录：MOD开发/StateKeeper/发行/0.1.0/。
- ZIP：MOD开发/StateKeeper/发行/StateKeeper-0.1.0.zip，130003字节。
- DLL：274432字节，SHA256 B85E42B5D622CD8FF5C3A09B1CC5BB61D67B10C74C069341A5CA52C4A8D41316。
- ZIP SHA256：E56BCAC4EA788A38E6480DEAEFDA6DE26BEE138D0A781DA723F8A6CF81D1D593。
- .build/release、发行目录、r2modman 2.0.a profile三处DLL哈希一致，已部署。
- 可复现脚本：MOD开发/StateKeeper/tools/Build-Release.ps1；可加-Deploy同步profile。脚本从manifest读取版本并核对工程/程序集版本、说明长度、依赖、包内白名单和ZIP内容。
- 本地发行包已生成；未登录或上传Thunderstore，没有线上发布地址。

## 发布边界

- 本包是首个公开测试版本，README明确尚未完成Unity中英文分辨率、输入法/控制器、多人真实性能等全部实机验收。
- 不修改原始局和旧schema，不要求再录两小时。不恢复旧F8接口，不把Unknown变成0，不声称已排除StateKeeper卡顿。
- schema3 / 新局collectionRevision3 / analysisVersion5不变。本轮参数清理不改变派生数据合同；旧实现记录的哈希与62测试数为发布前历史。
