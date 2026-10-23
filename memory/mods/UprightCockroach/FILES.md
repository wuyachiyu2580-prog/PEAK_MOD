# UprightCockroach Files

## 当前源码与产物

- `MOD开发/UprightCockroach/UprightCockroach/UprightCockroach.csproj`：net472 BepInEx 工程，目标 PEAK 2.6.b。
- `MOD开发/UprightCockroach/UprightCockroach/Plugin.cs`：插件入口和 Harmony 生命周期。
- `MOD开发/UprightCockroach/UprightCockroach/CharacterPatch.cs`：`Character.Start` 后挂载视觉组件。
- `MOD开发/UprightCockroach/UprightCockroach/UprightCockroachVisual.cs`：导入模型骨骼代理、原 Renderer 隐藏、第一人称可见性、生命周期和清理。
- `MOD开发/UprightCockroach/README.md`：开发版说明和构建命令。
- `MOD开发/UprightCockroach/TEST_PLAN.md`：单人、多人、生命周期和不改联机协议的验收步骤。
- 构建 DLL：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\UprightCockroach.dll`（2026-10-14 23:38:30，12,261,376 bytes，SHA-256 `163D9AF10E58B0FB9C33498B0C173916F5603661CFD4D18B21E12A844F412566`）。
- `UprightCockroach/AntennaConfiguration.cs`（源码目录内）：48 个左右独立触须参数；插件每秒检查文件重载。
- 配置：`2.0.a/BepInEx/config/wuyachiyu.peak.uprightcockroach.cfg`；说明 `MOD开发/UprightCockroach/ANTENNA_CONFIG.md`。
- 候选构建与13项测试：`.build/uprightcockroach-config/{candidate,checks,validation}/`；旧 DLL 备份 `.build/uprightcockroach-config/backups/20261014-212020/`。
- `MOD开发/UprightCockroach/3d66.com_JDH5455253615.fbx`：用户源文件，23,051,232 bytes，SHA-256 `8D8F6567677E0C55F1180A92EEA78EC2C598E5B7FCCCC86586C645650F36C72E`，未改。
- `MOD开发/UprightCockroach/FBX_INSPECTION_2026-10-14.md`：本地 FBX 实测、模型差异、绑定前决策、配置与部署记录。
- `model-import/`：FBX 结构 JSON、提取贴图、清理预览、`rig-landmarks.json`、`uprightcockroach.roach`、`rigged-manifest.json`、`pose-validation.json`。`tools/build_model.py` 和 `tools/validate_poses.py` 可复跑转换和离线压力检查。
- `MOD开发/UprightCockroach/scout-reference.json`：从 PEAK `data.unity3d` 提取的 120 根 Scout 骨骼名和 bind pose 研究快照。\n- `MOD开发/UprightCockroach/UprightCockroach/RoachModelData.cs` / `RoachModelAsset.cs`：版本化资源读取、贴图解码、SkinnedMeshRenderer 创建和 shader 选择。\n- `MOD开发/UprightCockroach/tests/ModelChecks.cs`：47 项模型、嵌入资源、配置和损坏输入检查。

构建命令：`dotnet build MOD开发/UprightCockroach/UprightCockroach/UprightCockroach.csproj --configuration Release --nologo`；最近一次构建 0 warnings / 0 errors。

## 调研依据

- `MOD开发/UprightCockroach/MODEL_RESEARCH_2026-10-14.md`：正式模型候选、来源、外观核验、下载/授权限制与后续适配步骤。
- `MOD开发/UprightCockroach/model-candidates-2026-10-14.json`：三个候选的公开 Sketchfab API 元数据摘录；不含模型网格/下载凭据。

- 游戏版本：`C:\SteamLibrary\steamapps\common\PEAK\version.txt`（2.6.b）。
- 游戏资源：`C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\data.unity3d`。
- 反编译参考：`C:\Users\Administrator\Desktop\MOD\PEAK\引用参考代码\反编译\2.6.b\Assembly-CSharp`。
- 关键参考类：`CharacterCustomization.cs`、`CustomizationRefs.cs`、`HideTheBody.cs`、`PlayerCustomizationDummy.cs`、`Character.cs`、`CharacterSpawner.cs`。
- 客户端自定义模型参考：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\AttackStudio-PeakCats`（当前文件为 `.old` 备份，只作只读参考）。

## 后续工程

- 用正式蟑螂网格/材质替换当前程序化外壳，优先复用 `scout-reference.json` 的 Scout 骨骼命名。
- 本地/远端可见性、死亡/昏迷/搬运和场景销毁清理。
- 记录单人/多人实机验收。
