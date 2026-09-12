# ModConfig 1.8.0 / PEAKLib.UI 1.7.0 修改建议

更新时间：2026-09-09

本文件只记录建议和实施顺序。除特别注明外，以下修改均尚未实施。

## P0：修复公共 `LOC: 0` 根因

建议由 PEAKLib.ModConfig 或 PEAKLib.UI 在克隆 `Templates.SettingsCellPrefab` 后处理克隆实例上的 `SettingsUICell.localizedText`：

1. 保留组件引用，但将 `autoSet=false`，避免游戏语言刷新时 `LocalizedText.RefreshAllText()` 再次覆盖普通配置名称。
2. 随后再把 `item.GetDisplayName()` 写入 `component.m_text.text`。
3. 不建议让每个业务 MOD分别修改共享模板；修复应归公共依赖所有。
4. 不应向原版本地化表伪造索引 `0`，这只会掩盖错误来源并可能影响其他原版 UI。

`LocalizedText.OnEnable()` 在 `Instantiate` 返回前可能已经写过一次 `LOC: 0`，因此顺序应保证禁用自动刷新后再写最终文本。单纯修改 `index` 或再次写文字，不能阻止后续语言刷新覆盖。

同时建议 PEAKLib.UI 不再使用：

```text
Resources.FindObjectsOfTypeAll<GameObject>().First(name == "SettingsCell")
```

来选择模板。应优先从明确的 `SharedSettingsMenu.m_settingsCellPrefab` 或已知菜单层级取得目标，并记录来源路径；如果需要跨场景持有模板，应克隆、规范化后再 `DontDestroyOnLoad`，不要长期保存场景对象引用。

## P0：移除危险的全局重注册

- `PlayersInfo`：删除语言变化后的 `ModConfigLocalization.RefreshCache()` 调用和实现。
- `Lantern&ShootZombies&Night`：删除语言变化后的 `RefreshCache()` 调用和实现。
- 语言变化只更新本 MOD自己的 `ConfigDescription` 和当前可见 UI，不清空 `EntriesProcessed`，不反射调用 `ProcessModEntries()` 或 `LoadModSettings()`。

本次报告没有发现重复注册，说明当前会话尚未触发这些危险路径；这不代表旧实现可以保留。

## P1：迁移新版菜单类型

以下项目仍查找旧类型 `PEAKLib.ModConfig.Components.ModdedSettingsMenu`：

- `PlayersInfo`
- `Lantern&ShootZombies&Night`
- `WhereIsThing`
- `WhereIsMyAmulet`
- `WhySoLaggy`

统一改为先解析 `PEAKLib.ModConfig.Components.ModSettingsMenu`，仅把旧类型作为旧版兼容回退。补丁必须按新版实际签名匹配：`ShowSettings()` 为无参方法；`SetSection(string)` 和 `UpdateSectionTabs(string)` 分别处理。

扫描 `GetDisplayName()` 时应只补丁声明该方法的实现，或按 `MethodBase` 去重；不要对继承得到的同一个泛型基类方法重复 Harmony patch。当前日志中 PlayersInfo、WhereIsThing 和 WhySoLaggy 已出现“only patch implemented methods”警告。

## P1：增强诊断 MOD

下一版报告建议增加：

- `[ActiveUI]`：只扫描 `activeInHierarchy=true` 的 TMP 文本。
- `[ResourcesAndTemplates]`：单独列未激活对象、预制体和 `DontDestroyOnLoad` 对象。
- 每个可疑文本对应的 `LocalizedText.index`、`row`、`autoSet`、`enabled`、`currentText`、场景名和 `hideFlags`。
- 当前 `ModSettingsMenu` 的 `selectedMod`、`selectedSection`、`search`、`FilterValue` 和 `m_spawnedCells` 数量。
- 每个活动 `SettingsUICell` 的显示文本、配置文件、section/key 和包装设置类型。
- `LocalizedText.RefreshAllText`、`ModSettingsMenu.ShowSettings/SetSection/UpdateSectionTabs` 及 `GetDisplayName` 的 Harmony owner。
- 精确区分 `LOC:`、`log:`、`unknown`，不要只给合并后的可疑计数。

诊断 MOD仍只输出报告，不自动修复对象。

## 验证顺序

1. 保持出现 `LOC: 0` 的 ModConfig 页面打开，按 `F9` 生成报告。
2. 冷启动后依次打开截图中的各 MOD 页面，确认活动 cell 的配置身份和文本。
3. 连续切换中英文至少 5 次，每次检查 `VisibleConfigEntryCount`、`BepInExWrappedSettingCount` 和重复项均不增长。
4. 分别验证搜索、五类过滤器、切换 MOD、切换 section、关闭并重新打开菜单。
5. 验证移除 ModConfig 后各业务 MOD仍可加载，确保本地化是软依赖。
6. 公共依赖修复前后各留一份报告，确认活动实例不再出现 `LOC: 0`，而配置名称与值未改变。

