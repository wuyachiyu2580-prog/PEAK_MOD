# Lantern_ShootZombies_Night Files

Last updated: 2026-05-10

## 2026-05-10 Key File Changes

- `Patches\LanternFuelOverridePatch.cs`: enforces local-player fuel authority, primary-lantern-only reserve/fuel drain, ignores remote fuel decreases for local lanterns.
- `Helpers\LanternFuelSync.cs`: caches remote fuel snapshots for HUD, but ignores snapshots whose GUID belongs to the local player lantern.
- `Helpers\LanternFuelBroadcaster.cs`: broadcasts only the primary local lantern to avoid duplicate-lantern fuel snapshots.
- `Helpers\LanternHelper.cs`: includes local-slot lantern authority, primary local lantern GUID resolution, and fuel-gain relight helper.

## 2026-05-09 Key File Changes

- `Patches\DispelFogFieldGuardPatch.cs`: guards BRP `DispelFogField.Update/OnDisable` NRE spam.
- `Helpers\ExtraLanternPurger.cs`: duplicate purge prioritizes destroyable candidates before fuel amount.

更新时间：2026-05-08

## 路径

- 开发目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\Lantern&ShootZombies&Night`
- 源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\Lantern&ShootZombies&Night\Lantern&ShootZombies&Night`
- 项目文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\Lantern&ShootZombies&Night\Lantern&ShootZombies&Night\Lantern&ShootZombies&Night.csproj`
- AssemblyName：`Lantern_ShootZombies_Night`
- Version：`0.2.1`
- 输出路径（OutputPath）：`c:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\`
- 发行目录：`MOD开发\Lantern&ShootZombies&Night\发行\<版本号>\`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\Lantern&ShootZombies&Night\Lantern&ShootZombies&Night\Lantern&ShootZombies&Night.csproj" -c Release
```

构建完成后 DLL 会自动同步到测试环境 `BepInEx\plugins\`。**构建前必须先退出游戏**，否则 DLL 被锁定会失败。

## 关键源码

### 入口与配置
- `LanternShootZombiesNight.cs`：主 Plugin，`[BepInPlugin]` 与生命周期。
- `ConfigPreset.cs`：配置预设集合。
- `HudDisplayMode.cs`：HUD 显示模式枚举。
- `LanternFuelOption.cs`：灯笼燃料相关配置。
- `ReserveWarmthRatio.cs`：暖值备用池的比例模式。

### 业务 Helper
- `Helpers\RoomConfigSync.cs`：主机→客户端配置同步。
- `Helpers\LanternHelper.cs`：灯笼查找、燃料增量、备用池和本地灯权威判断。
- `Helpers\LanternHud.cs` / `Helpers\LanternHudPanel.cs`：灯笼相关 HUD 渲染。
- `Helpers\NightColdWarning.cs`：夜间寒冷提示。
- `Helpers\ModIntegration.cs`：外部 MOD（BPR 等）兼容检测。

## 发行包（manifest 三件套）

每次发版必须在 `发行\<版本号>\` 下产出：
- `README.md`：面向玩家的白话中文。
- `CHANGELOG.md`：本版改动。
- `manifest.json`：description ≤ 135 字符；依赖只声明 `BepInEx-BepInExPack_PEAK`。

详见 `common/05_发布与版本规范.md`。

## 诊断日志

BepInEx 默认日志位置：
- `c:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\LogOutput.log`

深度联机诊断可配合 WhySoLaggy 的 `RpcMonitor` 和 `StructuredLogger`，详见 `mods/WhySoLaggy/`。
