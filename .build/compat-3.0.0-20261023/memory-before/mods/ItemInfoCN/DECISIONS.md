# ItemInfoCN Decisions

更新时间：2026-05-08

## 架构决策

- **只读 HUD，不改业务**：所有补丁都是 Postfix 风格读取原版数据后刷 TMP 文本，不修改物品效果、数值、RPC。
- **挂载点固定**：`Canvas_HUD/Prompts/ItemPromptLayout` 下新建子物体 `ItemInfoDisplay`，不动原版层级。
- **双前置判空**：场景查找 + 组件查找两层保护，任一失败直接 return，不抛异常、不刷错。
- **原子化赋值**：`itemInfoDisplayTextMesh` / `guiManager` 必须在全部实例化完成后最后一步赋值，避免孤儿。

## 翻译策略

- **有中文映射走中文**：`GetEffectChineseName` 的 `switch` 分支（12 项）命中走中文。
- **未命中走大写英文**：`default: effect.ToUpper()`，让用户一眼看出是未翻译的。
- **颜色正负兜底**：`InitEffectColors` 提供 `ItemInfoDisplayPositive` / `ItemInfoDisplayNegative` 两条兜底色，未指定具体效果色时用。

## 补丁边界

| 补丁 | 目标 | 作用 |
| --- | --- | --- |
| ItemInfoUpdatePatch | ItemInstanceData.Update 相关 | 每帧/节流更新显示 |
| ItemInfoEquipPatch | 装备/切手持 | 切物品时强制刷一次 |
| ItemInfoFinishCookingPatch | 烹饪完成 | 烹饪改变属性后刷新 |
| ItemInfoReduceUsesRPCPatch | 次数变化 RPC | 联机同步使用次数变化 |

## 兼容

- **EasyBackpack**：启动时检测 `nickklmao.easybackpack`，按 `EasyBackpack` 布尔在显示逻辑里走不同分支。
- **其他 MOD 新增的 Action**：走 `GetComponentEffectInfo` 的 `else` 兜底分支，`Value=0` + 类名英文。

## 禁止回退

- 禁止去掉 `AddDisplayObject` 的前置判空 → 会导致每帧 NRE 刷 LogError。
- 禁止在未完成实例化前赋值 `itemInfoDisplayTextMesh` → 会留孤儿 GameObject。
- 禁止修改原版 Action 的数值/行为 → 本 MOD 只做中文化 HUD。
- 禁止砍掉 `EasyBackpack` 兼容分支 → 会破坏已发布 1.0.0 的兼容承诺。
- 禁止直接用 `gm.heroDayText.font` 或 `TMP_Settings.defaultFontAsset` 给 `tm.font` 赋值 → 必须走 `Helpers.FontHelper.GetChineseCapable()`，违者会在原版改动 `heroDayText` 时直接丢中文字体。规范见 `common/06_UI与字体规范.md`。
