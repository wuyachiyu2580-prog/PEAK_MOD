# ModConfig Diagnostics Recent

更新时间：2026-09-09

## 0.1.0

- 已创建只读诊断 MOD，并生成 `0.1.0` 发行目录和 ZIP。
- 游戏内按 `F9` 写入 `BepInEx/ModConfigDiagnostics-latest.txt`。
- 2026-09-09 的实测报告确认运行时使用新版 `ModSettingsMenu`，旧 `ModdedSettingsMenu` 类型不存在。
- 报告确认当前 profile 没有重复 DLL、配置身份或 ModConfig 包装项。
- 唯一 `LOC: 0` 位于未激活的原版 `SettingsCell` 模板文本，不可据此归因到 An0n Fair Storms、Better Player Distance、Easy Backpack、Item Spawner Enhanced、Remote Alive Helper、WhereIsThing 或其他单个 MOD。
- 当前扫描使用 `Resources.FindObjectsOfTypeAll`，会包含未激活对象和预制体。下一版应把活动 UI 与模板/资源对象分栏，减少误判。

## 未完成

- 尚未增强 `LocalizedText` 字段、当前 ModConfig 选择状态和 Harmony owner 输出。
- 尚未在 `LOC: 0` 正在屏幕显示时生成第二份报告。
- 尚未修改任何现有 MOD、PEAKLib.ModConfig 或 PEAKLib.UI。

