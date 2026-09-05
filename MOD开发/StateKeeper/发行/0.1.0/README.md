# PeakRunAnalytics

中文名：PEAK远征数据分析

第一阶段只负责可靠采集，不包含游戏内分析面板。

数据目录为 `Application.persistentDataPath/PeakRunAnalytics`，PEAK 默认路径通常是：

`C:\Users\Administrator\AppData\LocalLow\LandCrab\PEAK\PeakRunAnalytics`

目录包含 `Runs`、`Favorites`、`active-run.json` 和 `index.json`。活动局每 5 秒自动保存；高频数据按约 30 秒一个 `*.json.gz` 文件分块，`active-run.json` 和每局主 JSON 只保存索引信息。正式结束由 PEAK 的 `GlobalEvents.TriggerRunEnded()` 判定，单人死亡、倒地、复活、传送和力竭不会结束一局。胜利由同局中的 `TriggerSomeoneWonRun` 判定。

格式版本 2 面向 2 小时、8 人房设计：JSON 压缩和文件写入在串行后台任务中完成，高频状态使用固定顺序数组，距离使用玩家索引数组。玩家索引对应每局主 JSON 的 `players`，状态数组顺序对应 `statusTypeOrder`。

默认按 `F8` 收藏或取消收藏最近一局。收藏局不参与最近 10 局清理。

远端玩家物品使用是根据收到的状态观察得到的，标记为 `RemoteObserved`；本机实际触发的事件标记为 `LocalAuthoritative`。
