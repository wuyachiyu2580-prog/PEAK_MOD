# ModConfig Diagnostics Temporary Memory - 2026-09-09

- 已读取真实 profile 报告；环境为 PEAK 2.4.b、ModConfig 1.8.0、PEAKLib.UI 1.7.0、Core 1.7.2。
- 报告无重复 DLL、配置身份或包装设置；174 个可见配置项对应 174 个 ModConfig 包装项。
- 唯一 `LOC: 0` 来自 `SettingsCell/Text (TMP)` 且未激活，根因候选是原版 SettingsCell 上默认 `LocalizedText row=0` 与 ModConfig 直接写 TMP 文本的组合。
- 下一步先增强活动实例/模板分栏和 `LocalizedText` 字段输出，再在问题页面可见时按 F9；尚未修改任何业务 MOD或公共依赖。

