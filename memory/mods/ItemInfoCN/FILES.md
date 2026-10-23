# ItemInfoCN Files

## 2026-10-23 PEAK 3.0.0 兼容性审核

必须适配：当前源码访问已删除 Mandrake.aoe，Release 3个CS1061；旧发行/terrain 1.0.0 DLL还含失效 StatusField 签名。Mandrake问题2.6.b已存在。本轮未修代码或更新DLL。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

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
