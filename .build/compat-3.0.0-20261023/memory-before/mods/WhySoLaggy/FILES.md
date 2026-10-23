# WhySoLaggy Files

## 2026-09-21 发行文件准备完成

当前版本 `1.0.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhySoLaggy/发行/1.0.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 语言监听、配置归属和补丁警告；重新核对诊断配置及日志说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前产物

- 开发版本 `1.0.5` / 程序集 `1.0.5.0`；新增 `Helpers/ModConfigUiAdapter.cs`（独立反射适配器）。
- profile 输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhySoLaggy.dll`，160256 字节，SHA-256 `946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550`。
- 旧发行 DLL/hash 属于历史，不再与当前 profile 相同；验证入口：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-08-31

## 路径

- 源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy`
- 项目文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy\WhySoLaggy.csproj`
- AssemblyName：`WhySoLaggy`
- Version：`1.0.4` / AssemblyVersion `1.0.4.0`
- 输出路径：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\`
- 当前发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\发行\1.0.4`
- 当前发行 ZIP：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\发行\1.0.4\wuyachiyu-WhySoLaggy-1.0.4.zip`
- 发行 DLL：`1.0.4.0`、`157184` 字节，SHA-256 `CC9FCC52E825088AE254754BF30BF5E58A6A1FA8FA78C5E3DDA0A121B3B71431`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy\WhySoLaggy.csproj" -c Release
```

Release 构建会直接覆盖上述 `2.0.a` profile 中的 `WhySoLaggy.dll`。

## 已识别关键文件

- `WhySoLaggyPlugin.cs`
- `Helpers\LagLogger.cs`
- `Helpers\FpsTracker.cs`
- `Helpers\RpcMonitor.cs`
- `Helpers\NetworkAbuseDetector.cs`
- `Helpers\PerformanceDashboard.cs`
- `Helpers\PatchProfiler.cs`
- `Helpers\PluginProfiler.cs`
- `Helpers\HarmonyScanner.cs`
- `Helpers\FieldProbe.cs`
- `Helpers\StructuredLogger.cs`
- `Helpers\BoundedConcurrentQueue.cs`
- `Helpers\MethodKey.cs`
- `Helpers\OwnershipEventParser.cs`

## 测试

- 测试项目：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\WhySoLaggy\WhySoLaggy.Tests\WhySoLaggy.Tests.csproj`
- 测试入口：`WhySoLaggy.Tests\CoreBehaviorTests.cs`
- 覆盖 Ownership payload、有界队列、Profiler 估算/精确调用数、方法键、FieldProbe 静态根、日志批处理/schema 轮转、重复初始化、ModConfig 本地化和 PEAK 2.3.a watched RPC 名单；当前文件共有 18 个 `[TestMethod]`。
