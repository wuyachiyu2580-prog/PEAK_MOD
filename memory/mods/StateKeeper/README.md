# StateKeeper

> 2026-09-12 当前开发状态：analysisVersion7使用归因修订，见 `research/USE_RULES_2026-09-12.md`。完成动作与效果分开、归因组统一判定、连续资源不虚构次数、原单位汇总和使用/流转视图；原始采集不变。先前整份扩展分析计划并未全部完成，剩余范围见TODO。

> 2026-09-09 UI修复：见`temp/2026-09-09.md`。已确认并修复克隆按钮遗留GUIManager.Resume持久事件、重命名Canvas100低于原菜单204，以及英文文字区不足；64项回归及实际资源/字体核查通过，新DLL和0.1.0包已替换，运行中UI仍待重启确认。

> 2026-09-08 发布状态：`RELEASE_AUDIT_2026-09-08.md`。0.1.0本地首版发布包已生成、profile已更新，64项测试通过。发布前清理废弃收藏/图表接口和参数，修复Enabled=false事件仍写入及默认诊断日志。没有向Thunderstore网站上传；实机待验收项已写入发布README。

> 2026-09-08 当前状态：`REPORT_REBUILD_2026-09-08.md`。统计纠错、六页复盘、搜索/命名和跨局比较已实现；62项测试（含七局只读回放）通过，Release已部署。schema3 / 新局collectionRevision3 / analysisVersion5。最新局14条死亡观察为8次确认死亡、6条重复；有6个进度点和127处时间回退。Unity界面与游戏内性能仍待短时验收。下文保留早期历史，不作为当前完成度依据。

更新时间：2026-09-12

## 项目定位

`StateKeeper`（English：`STATE KEEPER`；中文名：`状态分析`）是独立的 PEAK 局数据采集 MOD。当前版本 `0.1.0`，本地首版包已生成但尚未上传 Thunderstore。六页复盘、搜索、命名和跨局比较已接入；Unity 实机 UI、真实性能和匿名提交仍属后续工作。

它不修改 `PlayersInfo`，不依赖其私有代码，不发送新的游戏业务 RPC，也不把远端观察数据伪装成本地权威数据。

## 当前身份与路径

- 插件名：`StateKeeper`
- GUID：`com.local.statekeeper`
- 版本：`0.1.0`
- 源码：`MOD开发/StateKeeper/StateKeeper/`
- 测试：`MOD开发/StateKeeper/StateKeeper.Tests/`
- profile DLL：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/StateKeeper.dll`
- 配置：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/config/com.local.statekeeper.cfg`
- 数据：`C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/StateKeeper/`
- 后续完整计划：`memory/mods/StateKeeper/PLAN.md`
- 关联网站计划：`memory/mods/PEAK-MAP/`

## 当前采集内容

- 所有玩家的普通体力、额外体力、最大体力、力竭值、位置和异常状态。
- 死亡、倒地、清醒、冲刺、着地、攀爬、绳索、藤蔓、滑翔、火箭和挣扎等状态。
- 当前玩家之间的全部两两距离。
- 三个主栏、临时槽、背包槽和背包内部四格的紧凑物品快照。
- 物品 ID、名称、Prefab、GUID、使用次数、剩余使用比例、燃料和烹饪量。
- 拾取、开始使用、主/次施法完成、喂食、消耗、使用次数减少、库存变化、死亡、倒地、胜利和正式结束事件。
- 事件来源区分 `LocalAuthoritative` 与 `RemoteObserved`。

体力、位置、状态和距离保持 5Hz。库存轮询当前为 2Hz，关键物品动作仍由 Harmony 事件即时补充。

## 局识别与存储

- 使用 `RunManager.RunId` 作为一局身份；空 RunId 不创建记录。
- `GlobalEvents.TriggerRunEnded()` 是正式结束基准；`TriggerSomeoneWonRun` 判定胜利，否则正式结束记为失败。
- 单人死亡、倒地、复活、传送和力竭都不会结束本局。
- 活动局每 5 秒检查点，高频数据写入约 30 秒一个 `*.json.gz` 分块；后续计划在正式结束或 Aborted 后后台合并为单局完整 `*.data.json.gz`。
- JSON/GZip 写入使用串行后台队列；过时未封存检查点可合并，封存分块不能跳过；正常退出会等待队列。
- 默认保留最近 10 局；后续改为在面板中收藏/取消收藏，移除 F8 快捷键，收藏局不计入 10 局。

## 2026-09-05 实机数据基线

改名前旧构建产生的一局已迁移到 `StateKeeper` 数据目录：胜利局约 74 分 51 秒，128 个连续可解压分块，压缩后约 4.80 MiB、解压 JSON 约 88.69 MiB，21,367 个样本、1,085 个库存快照、87,678 个事件，实际采样约 4.79Hz。

旧数据中 `StaminaChanged` 有 65,150 条，其中 63,967 条变化量小于 0.01。当前构建新增 `Advanced.StaminaEventThreshold=0.01`，只记录绝对变化量大于或等于阈值的即时事件；5Hz 体力曲线不受影响。

此前 5 局整体调研约有 107,130 个样本、6,911 个库存快照、238,915 个事件，数据目录约 36.7 MB；约 4 局 Victory、1 局 Aborted。更完整的历史分析、异常时间轴和后续展示建议见 `PLAN.md` 的“前期调研结论归档”。

## 当前状态

- 严格构建：0 warnings / 0 errors。
- 自动测试：2/2 通过，覆盖 8 人分块、活动恢复、收藏、最近 10 局和分块联动清理。
- 本机旧数据、旧配置已一次性迁移；运行时代码不再包含旧名兼容迁移。
- `MOD开发/StateKeeper/发行/0.1.0/` 已是本地首版包，尚未上传 Thunderstore；发布审计见 `RELEASE_AUDIT-2026-09-08.md`。
- 下一步按 `TODO.md` 完成 Unity 实机和真实性能验收；基础分析范围已由五局数据确定，复杂评分仍暂缓。
