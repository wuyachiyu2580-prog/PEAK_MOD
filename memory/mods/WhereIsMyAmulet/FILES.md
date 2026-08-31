# WhereIsMyAmulet Files

更新时间：2026-08-31

## 路径

- 源码：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet`
- 工程：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet\WhereIsMyAmulet.csproj`
- 测试输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WUYACHIYU-WhereIsMyAmulet\WhereIsMyAmulet.dll`
- 当前发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\发行\1.0.3`
- 当前 ZIP：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\发行\1.0.3\wuyachiyu-WhereIsMyAmulet-1.0.3.zip`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhereIsMyAmulet\WhereIsMyAmulet.slnx" -v:minimal
```

结果：1.0.3 发行 DLL 程序集版本为 `1.0.3.0`，大小 `40960` 字节，SHA-256 为 `77AA45DA6A90664910172596649F8E567F3369961D4EC19D7A5634E5FBDC7984`；profile DLL 与发行 DLL 一致。当前记忆更新未重新构建。

## 关键文件

- `WhereIsMyAmuletPlugin.cs`：插件入口、配置、扫描、定时/常驻标签生命周期。
- `ModConfigLocalization.cs`：可选 PEAKLib.ModConfig 的中英文配置显示文本。
- `AmuletLabel.cs`：世界坐标标签、距离和屏幕外方向提示。
- `FontHelper.cs`：游戏 TMP 字体获取和字体兜底。
