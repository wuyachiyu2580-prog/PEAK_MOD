# WhereIsThing Files

更新时间：2026-09-06

## 源码

- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.cs`：BepInEx 入口、配置、快捷键、公共状态、标签生命周期和场景清理。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.Discovery.cs`：增量 PhotonView 发现、有限重试、放置目标分类和 owner 缓存。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.SceneDiscovery.cs`：普通物品、行李箱、静态/动态场景目标的分帧发现。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.PlacedSources.cs`：从物品目录建立放置 prefab 正向来源索引，并维护纯客户端投掷证据。
- `MOD开发/WhereIsThing/WhereIsThing/ThingTypes.cs`：物品、行李箱和场景目标定义，动态数据库加载、名称、危险分类，以及 ItemSpawnerEnhanced 审计后加入的精确 prefab 分类覆盖。
- `MOD开发/WhereIsThing/WhereIsThing/ThingLabel.cs`：世界空间目标到屏幕标签的显示。
- `MOD开发/WhereIsThing/WhereIsThing/ThingSelectionWindow.cs`：物品选择窗口、范围复选框、仅显示已选、分类网格、自适应列数和鼠标状态。
- `MOD开发/WhereIsThing/WhereIsThing/ThingPresetPickerWindow.cs`：预设选择、共享和管理窗口。
- `MOD开发/WhereIsThing/WhereIsThing/ThingPresets.cs`：预设数据、内置预设和序列化逻辑。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/FontHelper.cs`：游戏 TMP 字体获取和场景缓存失效。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/ModConfigLocalization.cs`：ModConfig 分组、配置项、说明和枚举下拉选项的中英文本地化。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj`：net4.7.2 工程与 PEAK `2.4.b` DLL 引用。
- `memory/mods/WhereIsThing/PLAN.md`：分阶段研究、实机验证、收口和发布计划。

## 构建

在 `MOD开发/WhereIsThing/WhereIsThing` 执行：

```powershell
dotnet restore WhereIsThing.csproj
dotnet build WhereIsThing.csproj --configuration Release --no-restore
```

输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`。

当前测试 DLL：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`，程序集版本 `0.1.2.0`，大小 `158720` 字节，SHA-256 为 `AC1D98407D52BAA61E1B7B4EF33F5AB4B09CEC168E28D65E019587B048DCEFA9`。2026-09-06 Release 已成功覆盖该目录，结果为 `0 warnings / 0 errors`。

`MOD开发/WhereIsThing/发行/1.0.3` 是此前已有的发行目录，本轮 2.4.b 兼容修复没有更新该目录，也没有新建 ZIP；不得把目录内 DLL 当作当前测试构建。更早的 `发行/0.1.1` 及其 `0.1.1.0` DLL/hash 仅为历史产物。

当前依赖 `Assembly-CSharp.dll`、`Zorro.Core.Runtime.dll`、`Sirenix.Serialization.dll`、BepInEx、Harmony、Unity UI、TMP、TextRenderingModule 和 PhotonUnityNetworking。PEAK `2.4.b` 的 `Assembly-CSharp.dll` 位于 `C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed`，SHA-256 为 `21CBF3A6585A72778A2E5FB007E36CA246002DA5AE8209A6D998673633D82759`。ModConfig 是可选运行时集成，未安装时本地化补丁静默跳过。

分类参考源码：`引用参考代码/反编译/BepInEx/plugins/ItemSpawnerEnhanced/`。关键依据为 `VanillaItemCategories.cs`、`ItemCategoryResolver.cs` 和 `VanillaItemVisibility.cs`；该参考插件版本 `1.3.0` 自述基线为 PEAK 2.1.a，因此只作审计证据。
