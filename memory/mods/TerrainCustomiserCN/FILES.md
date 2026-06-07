# TerrainCustomiserCN Files

更新时间：2026-05-30

## 路径

- 解决方案：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\TerrainCustomiserCN.slnx`
- 主项目：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\TerrainCustomiserCN\TerrainCustomiserCN.csproj`
- Collector 项目：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\TerrainCustomiserCNCollector\TerrainCustomiserCNCollector.csproj`
- 输出目录：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\Snosz-TerrainCustomiserCN`
- 发布目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2`
- 发布包：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2\wuyachiyu-TerrainCustomiserCN-0.1.2.zip`
- 原版反编译参考：
  - `C:\Users\Administrator\Desktop\MOD\PEAK\引用参考代码\反编译\BepInEx\plugins\TerrainCustomiser`
  - `C:\Users\Administrator\Desktop\MOD\PEAK\引用参考代码\反编译\BepInEx\plugins\TerrainCustomiser0.3.1`
  - `C:\Users\Administrator\Desktop\MOD\PEAK\引用参考代码\反编译\BepInEx\plugins\TerrainCustomiser0.3.2`
- 游戏 Managed DLL：`C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed`
- 本地前置 MOD：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\Snosz-PhotonCustomPropsUtils`、`Snosz-UBImGui`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\TerrainCustomiserCN.slnx" -c Release
```

Release 输出直接覆盖 r2modman terrain profile 的 `Snosz-TerrainCustomiserCN` 插件目录。

## 关键源码文件

- `Plugin.cs`：BepInEx 入口，声明依赖、互斥原版、插件版本和 Harmony patch。
- `Managers\NetworkManager.cs`：原版网络管理器 ID 与 `mapData` / `propViews` / `playerInfo` / `TC_inCustomMap` 属性键。
- `Utils\TerrainCustomiserSerialization.cs`：CN 类型名与原版 `TerrainCustomiser.*` 类型名的双向序列化 binder。
- `Map\Serialization\MapSerializer.cs`：地图保存/读取、原版 0.3.2 持久化保存路径、Sirenix 序列化上下文；另有机场游玩列表用的 `*.json.old` 只读备份支持。
- `Map\MapLoader.cs`：同步原版 0.3.2 Caldera/Volcano 自定义变体修复，只在 active biome 不同的时候切换 biome。
- `Managers\MapManager.cs`：同步原版 0.3.2，移除 `ChangeBiome()` 内部 same-biome guard。
- `UI\DisplayNameTranslator.cs`：UI、字段、类型、枚举、动态对象名和资源名翻译表；缺失翻译事件来源；当前已补 `Spires -> 尖塔`。
- `UI\Windows\ResourceWindow.cs`：资源窗口中英文筛选逻辑。
- `Managers\ChineseFontManager.cs` / `Patches\ImGuiPatches.cs`：中文显示与 ImGui 字体相关逻辑。
- `TerrainCustomiserCNCollector\Plugin.cs`：缺失翻译收集器，输出 `TerrainCustomiserCN_missing_translations.tsv`；避免新游戏开始后直接覆盖旧缺失文件。

## 项目配置要点

- 主项目和 Collector 均为 `netstandard2.1`。
- 主项目版本：`0.1.2`；Collector 版本：`0.1.1`。
- 两个项目都设置 `<GenerateDependencyFile>false</GenerateDependencyFile>`，避免生成 `.deps.json`。
- 前置依赖：`BepInExPack PEAK`、`PhotonCustomPropsUtils`、`UBImGui`。
- Unity Jobs 位于 `UnityEngine.CoreModule`；`RaycastCommand` / `OverlapBoxCommand` / `QueryParameters` 位于 `UnityEngine.PhysicsModule`。
- `System.IO.Compression` 使用 PEAK Managed 目录中的 DLL，已确认可见 Brotli 相关类型。

## 发布包内容

- `TerrainCustomiserCN.dll`
- `TerrainCustomiserCNCollector.dll`
- `manifest.json`
- `README.md`
- `CHANGELOG.md`
- `icon.png`

## 常用验证命令

```powershell
[Reflection.AssemblyName]::GetAssemblyName("C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2\TerrainCustomiserCN.dll").Version.ToString()
[Reflection.AssemblyName]::GetAssemblyName("C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2\TerrainCustomiserCNCollector.dll").Version.ToString()
Get-ChildItem -Path "C:\Users\Administrator\Desktop\MOD\PEAK" -Recurse -Filter *.OLD -File
Get-ChildItem -Path "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\TerrainCustomiserCN\发布\0.1.2" -Recurse -Include *.deps.json,*.OLD -File
```
