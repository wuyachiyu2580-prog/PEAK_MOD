# 更新日志

## [0.1.0] - 2026-05-23

### 初版发布

- 增加 TerrainCustomiser 编辑器窗口、按钮、字段、类型、枚举、右键菜单、保存/加载弹窗、资源名和地形标签的中文 UI 翻译。
- 保留原版 TerrainCustomiser 的网络管理器 ID、房间属性键和玩家属性键，用于维持联机交叉游玩兼容性。
- 增加序列化兼容处理，让 TerrainCustomiserCN 保存和读取地图时继续使用原版 TerrainCustomiser 的地图数据类型名。
- 保持与原版 TerrainCustomiser `Map Saves` 文件夹的地图存档兼容。
- 增加 `TerrainCustomiserCNCollector.dll`，用于把缺失的 UI 翻译收集到 `TerrainCustomiserCN_missing_translations.tsv`。
- 资源搜索现在同时支持英文原始资源名和中文显示名。
