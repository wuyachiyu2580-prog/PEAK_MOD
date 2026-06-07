# TerrainCustomiserCN 0.1.2

TerrainCustomiserCN 是 `TerrainCustomiser` 的中文界面版本。
本 MOD 的目标是只替换 UI 显示文字，并同步对应原版的必要修复；不改动原版地图数据、联机同步键和存档结构。

感谢 snozz 制作并维护原版 `TerrainCustomiser`。TerrainCustomiserCN 只是基于原版功能做的中文化版本。

## 重要说明

- 请不要和原版 `TerrainCustomiser` 同时启用。
- 地图存档使用 PEAK 的持久化数据目录 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`，方便与原版 TerrainCustomiser `0.3.2` 共用同一份地图。
- 联机同步继续使用原版 TerrainCustomiser 的网络管理器 ID、房间属性和玩家属性，尽量保证 TerrainCustomiserCN 与原版 TerrainCustomiser 可以交叉游玩。
- 中文化只作用于界面显示文字；地图内部参数、资源名、枚举值和序列化数据不做中文化改名。
- 本包附带 `TerrainCustomiserCNCollector.dll`，用于收集漏翻的 UI 文本。
- 机场“开始”选择地图时支持读取 `*.json.old` 文件，这类文件通常是 r2modman 在禁用原版 TerrainCustomiser 后留下的备份。
- `*.json.old` 会在游玩列表中标记为 `[只读备份]`，TerrainCustomiserCN 只会读取它，不会复制、改名、删除或恢复它。
- 编辑器加载和保存仍只处理正常的 `*.json` 地图文件，不能直接编辑 `*.json.old`。

## 版本对应

`TerrainCustomiserCN 0.1.0` 对应原版 `TerrainCustomiser 0.3.0`。
`TerrainCustomiserCN 0.1.1` 对应原版 `TerrainCustomiser 0.3.1`。
`TerrainCustomiserCN 0.1.2` 对应原版 `TerrainCustomiser 0.3.2`。
两人实际测试中，一人使用 TerrainCustomiserCN、另一人使用原版 TerrainCustomiser 可以交叉游玩并共用地图数据。后续每次 TerrainCustomiserCN 更新时，都会在这里说明对应的原版 TerrainCustomiser 版本。

注意：不要在同一个客户端里同时启用 TerrainCustomiserCN 和原版 TerrainCustomiser。

## 前置 MOD

请先安装以下依赖：

- `BepInExPack PEAK`
- `PhotonCustomPropsUtils`
- `UBImGui`

## 安装方式

1. 确认已经安装上面的前置 MOD。
2. 将本包内容放入 `BepInEx/plugins`，或通过 r2modman 安装。
3. 确认原版 `TerrainCustomiser` 没有同时启用。
4. 启动游戏后，在机场 kiosk 菜单中打开 TerrainCustomiserCN。

## 基本使用

### 游玩模式

1. 选择地图存档。
2. 设置种子、LightMap 烘焙等选项。
3. 点击游玩后，地图数据会同步给房间内玩家并开始载入。

### 存档目录

从 `0.1.2` 开始，TerrainCustomiserCN 跟随原版 TerrainCustomiser `0.3.2`，地图保存和读取目录改为：

`%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`

旧版本放在插件目录下的地图不会自动迁移。需要继续使用旧地图时，请先备份，再手动复制到上面的新目录。

### 编辑模式

1. 编辑模式建议离线使用。
2. 在层级、资源和检查器窗口中编辑地图内容。
3. 右键可打开上下文菜单，进行创建、重命名、删除等操作。
4. 资源窗口支持用英文原始名或中文显示名筛选。
5. 保存后的地图会尽量保持与原版 TerrainCustomiser 兼容。

## 地图备份与分享

可以把做好的地图保存到 PEAK 地图仓库：

https://peakmap.top/

这个网站目前只做了网页界面的中文翻译。地图介绍、地图名和作者名不会调用翻译 API，上传者输入什么语种，页面就显示什么语种。

强烈建议使用 mod 管理器制作地图时多做备份，也可以把地图保存到上面的仓库。mod 管理器更新、禁用或重装 MOD 时，极有可能改名、移动或删除辛苦做好的地图文件。

## 漏翻收集

如果界面中仍出现英文，运行游戏后查看同目录生成的文件：

`TerrainCustomiserCN_missing_translations.tsv`

这个文件只记录缺失的 UI 翻译，不会导出游戏完整文本表。
反馈漏翻时，请直接提供这个 TSV 文件内容。

也可以通过 QQ 群反馈翻译缺失：

- QQ 群: 1093172647

## 包内容

- `TerrainCustomiserCN.dll`
- `TerrainCustomiserCNCollector.dll`
- `manifest.json`
- `README.md`
- `CHANGELOG.md`
- `icon.png`

## 已知限制

- 可能仍有零星漏译。
- 个别术语可能需要根据游戏内实际显示继续校准。
- 为保证兼容性，部分内部英文名称会保留，不会强行翻译。
