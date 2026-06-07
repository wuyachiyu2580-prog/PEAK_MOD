# PEAK Map Browser v0.1.0

PEAK Map Browser 是一个 BepInEx 插件，用于在游戏内浏览 peakmap.top 的社区地图，下载地图 JSON，上传本地 JSON 地图，并可选择封面图片。

## 安装

将本发布包中的 `PeakMapBrowser.dll` 复制到 PEAK 的 BepInEx 插件目录，确保最终文件路径类似：

```text
PEAK/BepInEx/plugins/PeakMapBrowser.dll
```

进入游戏后按 `F8` 打开或关闭地图库界面。

## 重要依赖说明

本 MOD 下载和上传的是 TerrainCustomiser 地图 JSON 文件。

这些 JSON 文件需要搭配以下任意一个 MOD 使用，才能在游戏中作为自定义地图加载和游玩：

- `TerrainCustomiser`
- `TerrainCustomiserCN`

PEAK Map Browser 本身只负责地图浏览、下载、上传和管理，不替代 TerrainCustomiser/TerrainCustomiserCN 的地图加载功能。

下载的 JSON 会保存到：

```text
Application.persistentDataPath/TerrainCustomiser/Map Saves
```

如果该目录不存在，插件会自动创建。

## 功能

- 游戏内浏览 peakmap.top 地图列表
- 搜索、排序、按 MOD 版本筛选
- 查看地图缩略图、作者、版本、介绍和下载量
- 下载地图 JSON 到本地 Map Saves
- 自动扫描本地 Map Saves JSON 并上传
- 上传时默认使用 Steam 名称作为作者
- 支持从安全白名单目录选择封面图片

## 配置

插件配置由 BepInEx 自动生成，常用配置项包括：

```text
ApiBaseUrl = https://peakmap.top
Language = auto
PageSize = 12
ToggleKey = F8
```

`Language = auto` 会优先跟随游戏当前语言；也可以手动改为 `zh` 或 `en` 强制使用中文或英文界面。

如果你之前安装过旧版并已经生成配置文件，请将旧配置里的 `Language = zh` 改为 `Language = auto`，或者删除旧配置让插件重新生成。
