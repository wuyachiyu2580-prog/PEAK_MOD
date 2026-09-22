# WhereIsMyAmulet Files

## 2026-09-21 发行文件准备完成

当前版本 `1.0.4` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsMyAmulet/发行/1.0.4`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：19→3 TMP、共享材质和刷新缓存、20000 排序、投影 Z/固定字号修正、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前产物

- 开发版本 `1.0.4` / 程序集 `1.0.4.0`；新增 `ModConfigUiAdapter.cs`（独立反射适配器）。
- profile 输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsMyAmulet.dll`，44544 字节，SHA-256 `70C24B097F1B943C80746DF64C8B6AD28372758238B915FA942A2EFE10860F04`。
- 旧发行 DLL/hash 属于历史，不再与当前 profile 相同；验证入口：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-08-31

## 2026-09-20 当前测试路径（优先于历史信息）

- DLL：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsMyAmulet.dll`，1.0.4.0，hash/命令见RECENT。
- 临时 `TfaUiDiagnostics.cs` 及初始化入口已移除，当前DLL为普通Release，不再采集诊断报告。既有报告未删除。
- 当前AmuletLabel类内嵌于WhereIsMyAmuletPlugin.cs，不是独立文件；发行1.0.3及ZIP保持历史状态。

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
