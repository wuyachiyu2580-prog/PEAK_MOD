# TerrainCustomiserCN Temp - 2026-05-30

## 0.3.2 差异复核

- 对比原版 `TerrainCustomiser 0.3.1` 与 `0.3.2` 时，原始 diff 有约 24 处变化，但大部分是反编译 Token/RVA/metadata 噪声。
- 有意义的代码变化集中在四类：
  - 插件版本号从 `0.3.1` 到 `0.3.2`。
  - `MapSerializer.ResolveSavePath()` 改到 `Application.persistentDataPath\TerrainCustomiser\Map Saves`。
  - `MapLoader.ApplySave()` 对 Caldera/Volcano 自定义变体切换加 active biome 判断。
  - `MapManager.ChangeBiome()` 移除内部 same-biome guard，让 Caldera 和 Volcano 自定义变体可同时保持有效。

## CN 0.1.2 同步

- `TerrainCustomiserCN 0.1.2` 已同步原版 `TerrainCustomiser 0.3.2` 的保存路径和 Caldera/Volcano 修复。
- CN 主 DLL 版本保持 `0.1.2`，不要误改为 `0.3.2`。
- `TerrainCustomiserCNCollector` 保持 `0.1.1`，本轮只随包重建，功能没有进一步变化。
- Release 构建通过：`0` warnings，`0` errors。
- 发布包：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2\wuyachiyu-TerrainCustomiserCN-0.1.2.zip`。

## 地图文件策略

- 0.1.2 保存目录跟随原版 0.3.2：`%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`。
- 旧插件目录地图不自动迁移；玩家需要备份后手动复制。
- `.json.old` 支持仍只在机场开始游玩列表中读取，显示为 `[只读备份]`。
- `.json.old` 不进入编辑器、保存列表或自动处理流程，不复制、不改名、不删除、不恢复。

## 翻译与发布文档

- 最新缺失翻译中实际需要补的是 `Dynamic / Spires / HierarchyNode`，已加 `Spires -> 尖塔`。
- README / CHANGELOG / manifest 已按 0.1.2 发布口径更新为中文。
- README 中已补峰图地图仓库 `https://peakmap.top/`、QQ群反馈、感谢 snozz、`.json.old` 说明、备份提醒和 0.1.2 对应原版 0.3.2。
- 发布 zip 已在文档修改后重新生成，包内不包含 `.deps.json`。
