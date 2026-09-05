# StateKeeper

更新时间：2026-09-06

## 项目定位

`StateKeeper`（中文名：PEAK远征数据分析）是独立的 PEAK 局数据采集 MOD。当前版本 `0.1.0`，尚未正式发布；第一阶段只负责可靠采集和保存，不包含游戏内分析面板。

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
- 活动局每 5 秒检查点，高频数据写入约 30 秒一个 `*.json.gz` 分块。
- JSON/GZip 写入使用串行后台队列；过时未封存检查点可合并，封存分块不能跳过；正常退出会等待队列。
- 默认保留最近 10 局；F8 收藏/取消收藏最近正式结束的一局，收藏局不计入 10 局。

## 2026-09-05 实机数据基线

改名前旧构建产生的一局已迁移到 `StateKeeper` 数据目录：胜利局约 74 分 51 秒，128 个连续可解压分块，压缩后约 4.80 MiB、解压 JSON 约 88.69 MiB，21,367 个样本、1,085 个库存快照、87,678 个事件，实际采样约 4.79Hz。

旧数据中 `StaminaChanged` 有 65,150 条，其中 63,967 条变化量小于 0.01。当前构建新增 `Advanced.StaminaEventThreshold=0.01`，只记录绝对变化量大于或等于阈值的即时事件；5Hz 体力曲线不受影响。

## 当前状态

- 严格构建：0 warnings / 0 errors。
- 自动测试：2/2 通过，覆盖 8 人分块、活动恢复、收藏、最近 10 局和分块联动清理。
- 本机旧数据、旧配置已一次性迁移；运行时代码不再包含旧名兼容迁移。
- `MOD开发/StateKeeper/发行/0.1.0/` 仍是改名前旧发行草稿，按用户要求暂不更新，禁止直接发布。
- 下一步是用当前 `StateKeeper.dll` 做新的长时间多人实机采集，再比较事件量、磁盘体积、GC 和结算尖峰。
