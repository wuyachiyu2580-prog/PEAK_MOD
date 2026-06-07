# ItemInfoCN Recent

更新时间：2026-05-13

## 2026-05-13 — PEAK 1.62.a 兼容性检查

- 用户更新游戏 1.62.a 反编译后复核本 MOD。`Assembly-CSharp` 与 1.61.b 反编译源码哈希一致，ItemInfoCN 依赖的 `CharacterItems`、`ItemCooking`、`Action_ReduceUses`、`Item`、`Lantern`、`Mob` 等类未变化。
- Release 构建通过：`dotnet build ... ItemInfoCN.csproj -c Release`，0 warnings，0 errors。暂不需要代码更新。

## 2026-05-08 — FontHelper 根治

- 新建 `Helpers\FontHelper.cs`，与 PlayersInfo 同源同结构的四级兜底实现（AscentUI → heroDayText → 全场TMP → defaultFontAsset），内置 `_cached` 静态缓存和 `InvalidateCache()`。
- `Plugin.AddDisplayObject` 的字体赋值改为 `FontHelper.GetChineseCapable()`，以 `gm.heroDayText.font` 作为 `null` 回退，保留原判空门檻。
- 编译通过：`dotnet build ... ItemInfoCN.csproj -c Release`，0 error，2 旧有无关警告保留（签名配置相关）。
- 通用规范同步落盘到 `memory/common/06_UI与字体规范.md`，本 MOD DECISIONS 禁止回退新增一条不得直指 heroDayText.font。

## 1.0.0 发布（稳定态）

- 四个 Harmony 补丁全部接入：`ItemInfoUpdatePatch` / `ItemInfoEquipPatch` / `ItemInfoFinishCookingPatch` / `ItemInfoReduceUsesRPCPatch`。
- `AddDisplayObject` 加前置判空：`GAME/GUIManager`、`heroDayText.font`、`ItemPromptLayout` 任一为空直接 return，不再每帧 NRE 刷 LogError。
- 所有实例化完成后**最后**才赋值 `itemInfoDisplayTextMesh` 和 `guiManager`，保证"要么全有要么全没"，不留孤儿。
- `EasyBackpack` 兼容通过 `Chainloader.PluginInfos.ContainsKey("nickklmao.easybackpack")` 判定。

## 可调项（BepInEx Config · Section=`ItemInfoDisplay`）

| Key | 默认 | 作用 |
| --- | --- | --- |
| Font Size | 20 | TMP 字号 |
| Outline Width | 0.08 | 描边粗细 |
| Line Spacing | -35 | 行距（负值紧凑） |
| Size Delta X | 550 | 容器横向宽度 |
| Force Update Time | 1.0 | 强制刷新秒数 |

## 翻译覆盖

- 12 种状态的中文映射已落地，未覆盖的状态走 `effect.ToUpper()` 兜底。
- 效果色 17 条已枚举；`Hot` 与 `Heat` 统一走 `#C80918`，`Drowsy` 与 `Sleepy` 统一走 `#FF5CA4`。

## 已知边界

- 新增原版 Action 组件类型时必须扩 `GetComponentEffectInfo`，否则 `Value=0` 只显示英文名。
- 所有 MOD 新增的物品 Action 必须有可映射的 `EffectKey`，否则 HUD 显示原类名。
