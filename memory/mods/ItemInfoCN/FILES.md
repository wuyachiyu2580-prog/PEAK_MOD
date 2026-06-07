# ItemInfoCN Files

更新时间：2026-05-07

## 路径

- 源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\ItemInfoCN\ItemInfoCN`
- 项目文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\ItemInfoCN\ItemInfoCN\ItemInfoCN.csproj`
- AssemblyName：`ItemInfoCN`
- Version：`1.0.0`
- 输出路径：`c:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\ItemInfoCN\ItemInfoCN\ItemInfoCN.csproj" -c Release
```

## 已识别关键文件

- `Plugin.cs`
- `Plugin.Processing.cs`
- `Patches\ItemInfoPatches.cs`
- `Helpers\FontHelper.cs`：CJK 字体四级兜底访问器（AscentUI → heroDayText → 全场TMP → defaultFontAsset），与 PlayersInfo.Helpers.FontHelper 同源同结构；规范见 `memory/common/06_UI与字体规范.md`。
- `Properties\AssemblyInfo.cs`
