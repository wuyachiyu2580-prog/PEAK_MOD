# TerrainCustomiserCN Decisions

更新时间：2026-05-30

## 核心边界

- **以中文 UI 为主，不做 CN 专属玩法改造**：TerrainCustomiserCN 的目标是中文 UI，并同步对应原版 TerrainCustomiser 版本中必要的兼容修复；不自创地图格式、资源参数语义、联机协议或 CN 专属生成逻辑。
- **不以原版 DLL 为基底**：源码来自反编译代码整理和工程重建，不直接改当前可用的原版 `TerrainCustomiser.dll`。
- **原版功能优先保持**：除中文显示、字体/显示适配、Collector、兼容 binder、`.json.old` 只读游玩列表和同步原版修复外，不做无关重构。
- **与 DreamyAscent 历史切开**：旧 memory 中 `TerrainCustomiserCN -> DreamyAscent` 是 2026-05-12 的历史改名；当前 `TerrainCustomiserCN` 是新的独立中文 UI 版项目，不继承 DreamyAscent 的功能修复路线。

## 兼容性决策

- 单客户端互斥：`BepInIncompatibility("com.snosz.terraincustomiser")`，避免同一客户端同时加载 CN 版和原版。
- 联机兼容：继续使用原版 PhotonCustomPropsUtils 管理器 ID `com.snosz.terraincustomiser`。
- 房间/玩家属性键保持原版：
  - `mapData`
  - `propViews`
  - `playerInfo`
  - `TC_inCustomMap`
- Sirenix 序列化通过 `TerrainCustomiserCompatibilityBinder` 保存原版 `TerrainCustomiser.*` 类型名，读取时映射回 `TerrainCustomiserCN.*` 类型。
- CN 0.1.2 跟随原版 0.3.2，地图保存目录为 `Application.persistentDataPath\TerrainCustomiser\Map Saves`，Windows 通常路径为 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`。
- 旧的“优先指向原版插件目录 `Snosz-TerrainCustomiser\Map Saves`”只适用于 0.1.0/0.1.1 路线，已被 0.1.2 的原版 0.3.2 保存路径取代。
- 旧地图不自动迁移；用户要自己备份并手动复制，避免 MOD 管理器启用/禁用时 `.old` 处理和自动复制产生冲突。
- `.json.old` 仅在机场开始游玩列表中作为只读备份显示和读取，显示为 `[只读备份]`；编辑器加载/保存、`MapSerializer.SaveFiles` 和正常保存逻辑仍只处理 `*.json`。

## 版本对应关系

- `TerrainCustomiserCN 0.1.0` 对应原版 `TerrainCustomiser 0.3.0`。
- `TerrainCustomiserCN 0.1.1` 对应原版 `TerrainCustomiser 0.3.1`。
- `TerrainCustomiserCN 0.1.2` 对应原版 `TerrainCustomiser 0.3.2`。
- 后续每次原版更新，都必须重新通读反编译差异，确认 changelog 是否漏项，再决定 CN 版本号和发布说明。

## 翻译策略

- UI 显示文字翻译为中文；内部参数名、枚举值、资源原始名、序列化字段和网络键不改名。
- 短标签只在必要处缩短，例如按用户要求只把 `C`、`D` 这类单字母显示项保持单字，避免 UI 变形；普通英文单词不为了变短强行缩成一个字。
- 动态对象/层级名优先走精确词典；拆词翻译只用于显示，不写回地图数据。
- 资源筛选必须支持中文和英文，保证玩家输入中文也能筛到资源。
- Collector 的缺失标记用于避免运行时无谓地反复查总翻译表；后续若重构性能策略，必须保持缺失收集可用。

## 发布决策

- `.deps.json` 禁止进入发布包，两个 csproj 均设置 `<GenerateDependencyFile>false</GenerateDependencyFile>`。
- `TerrainCustomiserCNCollector.dll` 当前版本 `0.1.1`，随主包发布，方便玩家反馈漏翻。
- 发布包必须包含：`TerrainCustomiserCN.dll`、`TerrainCustomiserCNCollector.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、`icon.png`。
- README / CHANGELOG / manifest 均使用中文说明；manifest 的依赖 ID 不翻译。
- 0.1.2 主 DLL 版本必须保持 `0.1.2`，不要因为对应原版 0.3.2 而把 CN 主版本改成 `0.3.2`。

## 禁止回退

- 禁止把 CN 版网络管理器 ID 改成 `com.wuyachiyu.terraincustomisercn`，否则会破坏与原版 TerrainCustomiser 的联机交叉兼容。
- 禁止把 `mapData`、`propViews`、`playerInfo`、`TC_inCustomMap` 改成中文或 CN 专属键。
- 禁止移除 `TerrainCustomiserCompatibilityBinder` 或保存 CN 类型名；否则原版 TerrainCustomiser 可能读不了 CN 版保存/同步的数据。
- 禁止把 0.1.2 保存路径改回 CN 专属目录或旧插件目录；0.1.2 必须跟随原版 0.3.2 的持久化目录。
- 禁止自动迁移、复制、删除、改名旧地图或 `.json.old` 文件；只允许读取 `.json.old` 作为只读游玩备份。
- 禁止同时加载原版和 CN 版。
- 禁止把 Collector 改回导出完整游戏中英文表；目前只保留缺失 UI 翻译收集。
- 禁止发布 `.deps.json` 或 `.OLD` 文件。
