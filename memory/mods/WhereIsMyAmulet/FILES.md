# WhereIsMyAmulet Files

更新时间：2026-08-20

## 路径

- 源码：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet`
- 工程：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet\WhereIsMyAmulet.csproj`
- 测试输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsMyAmulet.dll`
- 当前发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\发行\1.0.2`
- 当前 ZIP：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\发行\1.0.2\wuyachiyu-WhereIsMyAmulet-1.0.2.zip`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet.slnx" -v:minimal
```

结果：0 个警告、0 个错误。发行 DLL 程序集版本为 `1.0.2.0`，大小 `40448` 字节。

## 关键文件

- `WhereIsMyAmuletPlugin.cs`：插件入口、配置、扫描、定时/常驻标签生命周期。
- `ModConfigLocalization.cs`：可选 PEAKLib.ModConfig 的中英文配置显示文本。
- `AmuletLabel.cs`：世界坐标标签、距离和屏幕外方向提示。
- `FontHelper.cs`：游戏 TMP 字体获取和字体兜底。
