# 更新日志

## [0.1.2] - 2026-05-29

### 更新

- 同步原版 TerrainCustomiser `0.3.2`。
- 修复同一地图中 Caldera 和 Volcano 自定义变体不能同时生效的问题。
- 地图保存和读取目录改为 PEAK 持久化数据目录 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`。
- 继续保留 `*.json.old` 只读备份在机场“开始”列表中的读取支持。
- TerrainCustomiserCN 主 DLL 版本号更新为 `0.1.2`。

## [0.1.1] - 2026-05-28

### 更新

- 更新 TerrainCustomiserCN 和 TerrainCustomiserCNCollector 的版本号到 `0.1.1`。
- 同步原版 TerrainCustomiser `0.3.1` 的修复，继续保持地图数据和联机兼容策略。
- 机场“开始”选择地图时支持读取 `*.json.old` 只读备份文件，方便 r2modman 禁用原版 TerrainCustomiser 后继续游玩备份地图。
- 编辑器加载、保存和原版式 `SaveFiles` 列表仍只处理正常的 `*.json` 文件，不会修改 `*.json.old`。

## [0.1.0] - 2026-05-23

### 初版发布

- 增加 TerrainCustomiser 编辑器窗口、按钮、字段、类型、枚举、右键菜单、保存/加载弹窗、资源名和地形标签的中文 UI 翻译。
- 保留原版 TerrainCustomiser 的网络管理器 ID、房间属性键和玩家属性键，用于维持联机交叉游玩兼容性。
- 增加序列化兼容处理，让 TerrainCustomiserCN 保存和读取地图时继续使用原版 TerrainCustomiser 的地图数据类型名。
- 保持与原版 TerrainCustomiser `Map Saves` 文件夹的地图存档兼容。
- 增加 `TerrainCustomiserCNCollector.dll`，用于把缺失的 UI 翻译收集到 `TerrainCustomiserCN_missing_translations.tsv`。
- 资源搜索现在同时支持英文原始资源名和中文显示名。
