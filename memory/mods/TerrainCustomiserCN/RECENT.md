# TerrainCustomiserCN Recent

更新时间：2026-05-30

## 2026-05-30 - 0.1.2 发布收口

- 当前发布版本为 `TerrainCustomiserCN 0.1.2`，对应原版 `TerrainCustomiser 0.3.2`。主 DLL 版本已纠正为 `0.1.2`，不是 `0.3.2`。
- Release 构建通过：`0` warnings，`0` errors。
- 当前测试输出 DLL：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\Snosz-TerrainCustomiserCN\TerrainCustomiserCN.dll`。
- 发布包已生成：`MOD开发\TerrainCustomiserCN\发布\0.1.2\wuyachiyu-TerrainCustomiserCN-0.1.2.zip`。
- 发布包包含：`TerrainCustomiserCN.dll`、`TerrainCustomiserCNCollector.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、`icon.png`。

## 0.3.2 同步内容

- `MapSerializer.ResolveSavePath()` 已同步原版 0.3.2，保存目录改为 `Application.persistentDataPath\TerrainCustomiser\Map Saves`。
- Windows 通常路径为：`%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`。
- 这使 CN 0.1.2 和原版 TerrainCustomiser 0.3.2 使用同一个地图目录。
- 不自动迁移旧插件目录地图；README 已提醒玩家先备份，再手动复制旧地图。
- 已同步 Caldera/Volcano 自定义变体修复：`MapLoader.ApplySave()` 只在目标 biome 与当前 active biome 不同时调用 `MapManager.ChangeBiome(...)`，`MapManager.ChangeBiome()` 内部不再用 same-biome guard 提前返回。

## 0.1.1 继承状态

- `TerrainCustomiserCN 0.1.1` 对应原版 `TerrainCustomiser 0.3.1`，包含 Mesa 自定义地图游玩修复、字符串列表创建修复、隐藏/移除部分无实用价值字段与类型的同步。
- `.json.old` 支持仍保留：机场开始游玩列表使用独立内部列表，同时显示 `*.json` 和 `*.json.old`；`.json.old` 标记为 `[只读备份]`，只用于游玩读取。
- 编辑器加载/保存和 `MapSerializer.SaveFiles` 仍只处理正常 `*.json`，避免改变原版编辑器语义。

## 中文化与 Collector

- `TerrainCustomiserCNCollector` 当前版本为 `0.1.1`，随 0.1.2 发布包一起上传；本轮仅随主项目重建，功能未改。
- Collector 只写 `TerrainCustomiserCN_missing_translations.tsv`，不再导出 `TerrainCustomiserCN_game_zh_en.tsv`。
- Collector 已改为避免新游戏开始后直接覆盖旧缺失文件，降低误删玩家反馈文件风险。
- 最新运行时 TSV 中实际漏翻项为 `Dynamic / Spires / HierarchyNode`，已补入 `UI\DisplayNameTranslator.cs`：`Spires -> 尖塔`。
- 旧 TSV 中可能仍残留已补项，不会自动删除；新 DLL 不应再追加 `Spires`。

## 发布文档

- `发布\0.1.2\README.md` 已复查并更新为中文说明，保存路径显示为 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`。
- README 已写明峰图地图仓库 `https://peakmap.top/`、QQ群反馈、感谢 snozz、0.1.2 对应原版 0.3.2、`.json.old` 只读备份策略、旧地图需手动备份迁移。
- `发布\0.1.2\CHANGELOG.md` 已加入 0.1.2 小节。
- zip 已在 README/CHANGELOG 修改后重新生成，发布包不包含 `.deps.json`。

## 当前风险

- 若玩家从 0.1.0/0.1.1 或原版 0.3.0/0.3.1 升到对应 0.3.2 路线后找不到旧地图，优先检查旧插件目录 `Map Saves` 和新持久化目录；不要自动复制或删除。
- 第四关若仍出现预览可用但游玩恢复正常地图，需要先和原版 0.3.2 对照；CN 只同步对应原版修复，不单独扩展地形生成逻辑。
- 后续仍可能有零星漏译，继续通过 Collector TSV 补 `DisplayNameTranslator.cs`。
