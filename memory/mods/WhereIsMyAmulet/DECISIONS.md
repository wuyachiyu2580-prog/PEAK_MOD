# WhereIsMyAmulet Decisions

## 1.0.3 映射和显示决策

- Scout Statue 的权威映射固定为 `hasAmulets[slot] -> amuletObjects[type]`；不能按 `amuletObjects` 数组位置直接推断护符名称。
- Scout Statue 标签以 statue instance ID + slot 区分，运行时有效性必须重新确认当前 slot/type 和对应 GameObject，避免场景状态变化后保留错误标签。
- 双行标题下的距离文字和阴影使用 anchored Y `-40`；标题/目标位置不随之移动。
- 启动日志保持安静，普通加载提示不再输出；本地化、字体回退和运行时异常等可行动信息仍可记录。
- 惊喜模式不是 1.0.3 的现有功能；如后续实现，必须单独确认显示范围、默认值和多人/本地显示边界，不能把历史需求写成当前能力。

## 显示生命周期

- 默认 `General.ScanMode=Persistent`，保持常驻显示行为。
- `General.ScanMode=Timed` 时，扫描完成后以 `Time.unscaledTime` 记录结束时间，使用不受游戏暂停影响的时间，到期调用现有 `ClearLabels()`。
- `General.DisplayDurationSeconds` 最低按 `0.5` 秒处理，避免无效或负数配置造成异常；配置值仍以 BepInEx 原始数值保存。
- 定时逻辑只放在 `WhereIsMyAmuletPlugin`，不复制 WhereIsThing 的选择窗口、预设、范围编辑或 ModConfig 全局注册流程。

## 目标识别边界

- 普通雕像上的碎片使用 `PropSpawner_AmuletStatues` 生成的 `FakeItem`。
- Scout Statue 上的碎片使用 `ScoutStatue.amuletObjects`。
- 标签只读真实场景对象，不生成 fake item、不修改网络状态、不拾取碎片。

## 配置本地化

- `General` / `Display`、配置 key 和 `Persistent` / `Timed` 序列化值保持英文原值。
- 中文只通过 `ModConfigLocalization` 替换界面显示名和说明。
- PEAKLib.ModConfig 未安装或未初始化时必须静默降级。
