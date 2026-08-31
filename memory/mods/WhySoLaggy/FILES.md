# WhySoLaggy Files

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
