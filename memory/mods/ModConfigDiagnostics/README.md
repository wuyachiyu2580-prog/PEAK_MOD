# ModConfig Diagnostics

更新时间：2026-09-09

## 项目定位

`ModConfig Diagnostics` 是 PEAK ModConfig 的只读诊断 MOD。当前版本为 `0.1.0`，用于输出运行时依赖版本、已加载插件、重复 DLL、BepInEx 配置项、`SettingsHandler` 包装项、ModConfig section 和可疑 UI 文本。

它不修改配置值、不注册额外游戏设置、不 Harmony patch ModConfig，也不主动修复 UI。

## 当前结论

- 实测环境为 PEAK `2.4.b`、PEAKLib.ModConfig `1.8.0`、PEAKLib.UI `1.7.0`、PEAKLib.Core `1.7.2`。
- 报告中没有重复 DLL、`DuplicateConfigIdentity` 或 `DuplicateWrappedSetting`。
- `VisibleConfigEntryCount=174`，`BepInExWrappedSettingCount=174`；`TotalSettingCount=207` 还包含原版设置，不能把 207 与 174 的差值当成重复注册。
- 唯一捕获的异常文本为 `SettingsCell/Text (TMP)` 上的 `LOC: 0`，对象为 `Active=False`，不属于某个 MOD 的配置项。
- PEAK 2.4.b 的 `LocalizedText.OnEnable()` 会在索引为空时使用默认 `row=0`，缺少本地化条目时返回 `LOC: 0`。
- PEAKLib.UI 1.7.0 从全局资源中取得原版 `SettingsCell` 作为模板；ModConfig 1.8.0 克隆后直接写 `m_text.text`，但没有禁用克隆上的 `LocalizedText`。这是多个 MOD 共用的潜在 UI 故障点。
- 当前报告是在 ModConfig 页面未激活时生成，只能确认预制体潜在故障，不能单凭这一份报告证明屏幕上的活动实例已经被覆盖；复现时应保持问题页面打开后按 `F9`。

## 接手入口

1. 先读 `PLAN.md`，按 P0/P1 顺序处理。
2. 再读 `RECENT.md` 和 `DECISIONS.md`，不要把建议误记成已实施修复。
3. 文件和报告路径见 `FILES.md`。

