# ModConfig Diagnostics Decisions

更新时间：2026-09-09

## 已确认边界

- 诊断 MOD保持只读，不自动删除组件、不改配置、不刷新 ModConfig 全局缓存。
- `LOC: 0` 和 `log:0` 必须分别记录；当前实测报告只出现 `LOC: 0`。
- `Resources.FindObjectsOfTypeAll` 的结果不能直接等同于屏幕可见对象，报告必须同时记录 `activeInHierarchy`、场景和完整层级路径。
- `TotalSettingCount` 包含原版和 MOD 设置；重复判定以配置身份和包装设置身份为准，不能只比较总数。
- 公共 `SettingsCell` 故障不得归因给当前选中的单个 MOD，除非报告能把活动 cell、配置 entry 和补丁 owner 对应起来。

## 禁止回退

- 不在诊断 MOD中加入自动修复或全局 `RefreshCache()`。
- 不因为看到一个未激活预制体上的 `LOC: 0` 就宣称已经捕获活动 UI 故障。
- 不把诊断 MOD注册的自身配置项当成待诊断重复项。

