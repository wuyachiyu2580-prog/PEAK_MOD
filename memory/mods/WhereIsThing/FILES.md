# WhereIsThing Files

更新时间：2026-08-14

## 源码

- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.cs`：BepInEx 入口、配置、快捷键、物品/行李箱/场景危险扫描和标签生命周期。
- `MOD开发/WhereIsThing/WhereIsThing/ThingTypes.cs`：物品、行李箱和场景目标定义，动态数据库加载、名称、危险分类和类别。
- `MOD开发/WhereIsThing/WhereIsThing/ThingLabel.cs`：世界空间目标到屏幕标签的显示。
- `MOD开发/WhereIsThing/WhereIsThing/ThingSelectionWindow.cs`：物品选择窗口、范围复选框、仅显示已选、分类网格、自适应列数和鼠标状态。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/FontHelper.cs`：游戏 TMP 字体获取和场景缓存失效。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/ModConfigLocalization.cs`：ModConfig 分组、配置项、说明和枚举下拉选项的中英文本地化。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj`：net4.7.2 工程与 PEAK 2.1.a DLL 引用。
- `memory/mods/WhereIsThing/PLAN.md`：分阶段研究、实机验证、收口和发布计划。

## 构建

在 `MOD开发/WhereIsThing/WhereIsThing` 执行：

```powershell
dotnet restore WhereIsThing.csproj
dotnet build WhereIsThing.csproj --configuration Release --no-restore
```

输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`。

当前依赖 `Assembly-CSharp.dll`、`Zorro.Core.Runtime.dll`、`Sirenix.Serialization.dll`、BepInEx、Harmony、Unity UI、TMP、TextRenderingModule 和 PhotonUnityNetworking。ModConfig 是可选运行时集成，未安装时本地化补丁静默跳过。发布前必须先关闭游戏，再确认 r2modman profile DLL 已被加载。
