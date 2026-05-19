# PlayersInfo Recent

Last updated: 2026-05-17

## 2026-05-17 网络波动加固（现象 D2：单条偶发闪一下）

- 用户描述："其他玩家的体力条有时会莫名其妙跳一下"。排查思路：让用户在 D1（槽位切换）/D2（短暂消失）/D3（数值跳变）/D4（UI 内部抖动）之间精准选择，确认是 D2。
- 根因：`photonView.Owner` 在 Photon 网络瞬间抖动期间会短暂为 null。原代码在 `RefreshNearby` 行 161 直接 `if (c.photonView.Owner == null) continue;` 把玩家剔除一帧；同时 `GetStableCharacterId` 在 Owner==null 时 fallback 到 ViewID，使同一玩家在 ActorNumber/ViewID 之间漂移 → 被识别成不同 stableId → ResolveDisplayOrder 看到成员变化 → ApplyDisplayOrder 立即重建 → 体力条整条切换/熄灭一下。
- 修法（三管齐下）：
  1. **stableId 防漂缓存**：`GetStableCharacterId` 内部维护 `static Dictionary<int viewId, int actor> s_viewIdToActor`。Owner != null 时记录 `s_viewIdToActor[viewId] = actor`；Owner == null 时优先回查缓存返回原 ActorNumber，回查不中再 fallback 到 viewId（首次见到 + 一直未拿到 Owner 的极端情况）。
  2. **放宽 Owner null 剔除**：删 `if (c.photonView.Owner == null) continue;`，只保留 `c.photonView == null` 的硬剔除。`sidForRange` 也改用 `GetStableCharacterId(c)` 走缓存（原本是直接读 `c.photonView.Owner.ActorNumber`，Owner null 时会 NRE 或漂移）。死亡时距离表达式简化（删除 `c.photonView != null ? ... : i` 三元，photonView != null 已在前面校验）。
  3. **成员丢失保留窗口（RetainOnLossDelay = 1.5f）**：新增 `static Dictionary<int sid, RetainedEntry> s_retainedById`（含 `Character ch` + `float lostTime`）。`RefreshNearby` 在 top-N 截断之后、`ResolveDisplayOrder` 之前插入补全循环：遍历 `_displayOrder`，本帧不在 `s_visibleById` 里的 stableId 视为"丢失"——首次丢失时从 `mates` 列表反查 `Character` 引用做快照塞 `s_retainedById`；之后每帧检查失踪时长，未超 1.5s 就用快照 Character 补回 `s_visibleScratch`/`s_visibleById`。本帧重新出现时 `s_retainedById.Remove(sid)` 取消保留。
- 关键不变量：补全是 `Add` 到 `s_visibleScratch` 末尾，`HasSameMembers` 依然为 true（双向包含），`ApplyDisplayOrder` 不会立即触发。顺序差异最多走 `_pendingOrder` + `ReorderDelay 0.75s` 后再切，对用户感受是平滑的。bar 渲染层（行 220-241）按 `_displayOrder[i]` 在 `s_visibleById` 里取 c → 补全后取得到原 Character → `BindTarget` 不变 → bar 不熄灭。
- 收尾：`ClearAll` 同步清空 `s_retainedById` 和 `s_viewIdToActor`，避免跨场景/配置重建时脏数据残留。
- 文件：`MOD开发/PlayersInfo/PlayersInfo/MonoBehaviours/TeammateBarsCoordinator.cs`，6 处 search_replace 一次性完成（字段定义 + 行 161 删硬剔除 + sidForRange 改用 GetStableCharacterId + ResolveDisplayOrder 前补全循环 + GetStableCharacterId 改写 + ClearAll 清缓存）。+69/-7 行。
- 构建与打包：Release 0 警告 0 错误，dll 50176 → 50688 字节落入测试环境；同步 `发行/0.1.1/CHANGELOG.md` 加第三条英文 Bugfix、`README.md` What's new 顶部加一行；删旧 zip 重打 `wuyachiyu-PlayersInfo-0.1.1.zip` 152552 字节（5 件套）。
- 0.1.1 包内累计 3 条 Bugfix：①SettingChanged 全订阅 ②NearbyRange 边缘 hysteresis ③网络波动加固。

## 2026-05-17 合补重打 0.1.1 发行包（Bugfixes入包）

- 今日两条 BUG 修复同步到 0.1.1 发行包：
  - `MOD开发/PlayersInfo/发行/0.1.1/CHANGELOG.md` 在 `[0.1.1] - 2026-05-17` 顶部加 `### Bugfixes` 小节两条（SettingChanged 全订阅 → 只订 3 个结构性开关；NearbyRange 边缘拖动 → 5m hysteresis margin）。
  - `README.md` 的 "What's new in 0.1.1" 顶部加两行 Bugfix 描述。
  - `manifest.json` 未变：`version_number` 仍 `0.1.1`、`description` 仍原文、`dependencies` 仍 `BepInEx-BepInExPack_PEAK-5.4.2403`。
- 构建与打包：
  - `dotnet build PlayersInfo.csproj -c Release` → 0 warnings、0 errors，dll 落入 `测试环境/BepInEx/plugins/PlayersInfo.dll`。
  - `Copy-Item` 同一份 dll 到 `发行/0.1.1/PlayersInfo.dll`（50176 字节）。
  - 删除旧 `wuyachiyu-PlayersInfo-0.1.1.zip` 后 `Compress-Archive` 重打五件（PlayersInfo.dll/README.md/CHANGELOG.md/manifest.json/icon.png） → 包大小 151816 字节。
- 最终 `发行/0.1.1/` 目录 6 件：CHANGELOG.md(2030)、README.md(2708)、manifest.json(279)、PlayersInfo.dll(50176)、icon.png(126224)、wuyachiyu-PlayersInfo-0.1.1.zip(151816)。
- 说明：本次是"同版本号覆盖"重打，Thunderstore 上传 0.1.1 需人工覆盖处理（官方一般不允许同版本号二次上传）。更规范的做法是发 0.1.2，但用户明确要求更新 0.1.1。下次不要随意另升。

## 2026-05-17 修复体力条边缘拖动闪烁（现象 B）

- 用户补说：除了现象 C（全体跳），现象 B（某个玩家体力条不停闪烁）也存在。
- 定位：`TeammateBarsCoordinator.RefreshNearby` 过滤距离时用同一个硬阈值 `range`，玩家距离恶在 `NearbyRange`（默认 30m）边缘抖动时每 0.25s 反复进/出可见集 → 对应 driver 反复 `BindTarget`/`SetActive(false)` → 闪烁。`ResolveDisplayOrder` 在成员变化时立即 `ApplyDisplayOrder`，也会加重这个现象。
- 修复：加入距离滞回（hysteresis），只是最小担入。
  - 新增 `private const float HysteresisMargin = 5f`。
  - 新增 `s_displayOrderSet` 静态 HashSet（复用，避免 GC），每帧 `RefreshNearby` 开头预算为当前 `_displayOrder` 的快照。
  - 距离过滤改为：已在集合里的玩家阈值 `range + HysteresisMargin`，不在集合里的玩家阈值仍为 `range`。
  - 效果：玩家走到 30m∲5m 边缘抖动时不会被拉出集合；只有连续拉远超过 35m 才被剔除，避免闪烁。
- 未动 `ResolveDisplayOrder` 的成员变化逻辑：玩家退游戏、死亡转灵魂、距离稳定拉远 5m 以上这类真变动仍需立即响应，不应延迟 0.75s。
- 构建：`dotnet build PlayersInfo.csproj -c Debug` → 0 warnings、0 errors，dll 直接落入测试环境。
- 仍为 0.1.1 内的修复（线下源码 fix），未升版、未重打发行包。

## 2026-05-17 修复体力条整体闪烁/重刷 BUG

- 用户反馈"部分玩家体力条会重复刷新"，确认现象 C：每隔几秒所有体力条一起跳一下。
- 根因：`PlayersInfoPlugin.BindConfig` 末尾的 `Config.SettingChanged += (s, a) => OnAnyConfigChanged()` 绑了**整张配置文件**的事件 → 任意配置项变化（包括 `OffsetX`/`OffsetY` 滑块拖动、`NearbyRange`/`MaxNearbyCount` 修改、ConfigurationManager 实时事件、BepInEx 启动时把配置文件再读一次的回写）都会触发 `OnAnyConfigChanged → coord.OnConfigChanged → ClearAll → 全部 _pool Destroy 重建`，视觉上所有体力条同步消失重建。
- 配置项分类：
  - **每帧/低频读的（无需事件）**：`CfgModEnabled`、`CfgMaxNearbyCount`、`CfgNearbyRange`、`CfgRoundStamina`、`CfgDebugLogging` —— `RefreshNearby` / `Driver.Update` 自己读 `Cfg.Value`。
  - **构建时一次性的（需事件）**：`CfgShowStaminaValue`（决定 `staminaValueText` 是否构建）、`CfgEnableInventoryRow`（决定 `InventoryRow` 子节点是否构建）、`CfgModEnabled`（关闭时立即 `ClearAll`）。
  - **当前未生效的死配置（不动）**：`CfgEnableStaminaBar`、`CfgAnchor`、`CfgOffsetX`、`CfgOffsetY` —— ConfigurationManager 能改但代码没读；本次不一并修，遵守"不引入超出本任务范围的修改"。
- 修复：删除全局 `Config.SettingChanged` 订阅，改为对 3 个有意义的开关分别绑 `XXX.SettingChanged += OnStructuralConfigChanged;`。新增 `OnStructuralConfigChanged(object sender, EventArgs e)` 转发到 `OnAnyConfigChanged()`。运行时数值类拖滑块/批量改不再重建池。
- 构建：`dotnet build PlayersInfo.csproj -c Debug` → 0 warnings、0 errors，dll 直接落入 `测试环境/BepInEx/plugins/PlayersInfo.dll`。
- 本次为 0.1.1 内的修复（线下源码 fix），未升版本号、未重打发行包。下次发版前会随其它改动一并打 0.1.2。

## 2026-05-17 发布 0.1.1（安静维护版）

- 三处版本号同步升级：`PlayersInfoPlugin.PluginVersion = "0.1.1"`、`AssemblyInfo.cs` 的 `AssemblyVersion`/`AssemblyFileVersion` 都是 `0.1.1.0`、`PlayersInfo.csproj <Version>0.1.1</Version>`。
- `MOD开发/PlayersInfo/发行/0.1.1/` 新建目录与补齐五件套：
  - `manifest.json` 复制 0.1.0 改 `version_number = 0.1.1`，依赖不变（`BepInEx-BepInExPack_PEAK-5.4.2403`）。
  - `CHANGELOG.md` 增加 `[0.1.1] - 2026-05-17` 区块，以 Performance / Compatibility 两小节描述三条优化与 PEAK 1.62.a 兑现。
  - `README.md` 标题改 0.1.1，额外加一节 "What's new in 0.1.1" 写明纯性能维护、无行为变更。Configuration / Features 表与 0.1.0 一致。
  - `icon.png` 从 `发行/0.1.0/` 复用。
  - `PlayersInfo.dll` 从 `测试环境/BepInEx/plugins/PlayersInfo.dll` 复制（上一次 Release 构建产出，同一份代码）。
  - `wuyachiyu-PlayersInfo-0.1.1.zip` 以 `PlayersInfo.dll`、`README.md`、`CHANGELOG.md`、`manifest.json`、`icon.png` 五件打包，总包 ~148 KB。
- 本版为“安静维护版”：发布内容完全等同于 2026-05-17 三条性能优化（脏检查 + 反射降频 + Sprite 销毁），没有新功能 / 新配置 / 行为变更。与 PEAK 1.62.a 兼容，不发 RPC、不改队友状态。

## 2026-05-17 性能优化：脏检查 + 反射降频 + Sprite 销毁

- 外部 AI 给出 8 条性能建议+1 条 BUG 报告，对照实际代码分档：3 条值得做、3 条已经做对、3 条夸大或微优化。
- **采纳 1（Sprite 资源管理）**：`Helpers/IconSpriteCache.cs` 中 `Sprite.Create` 后追加 `sp.hideFlags = HideFlags.DontSave`；`Clear()` 由原来的 `_cache.Clear()` 改为遍历 `Object.Destroy(sp)` 后再清字典。Clear 调用点（`PlayersInfoPlugin.OnSceneLoaded` / `OnAnyConfigChanged` 关闭 MOD 分支）语义不变。
- **采纳 2（数值文本脏检查）**：`MonoBehaviours/TeammateBarDriver.cs` 新增 `_lastStaminaShownInt`、`_lastStaminaShownTenth`、`_lastStaminaWasFloat`、`_lastExtraShownInt` 四个缓存字段。`UpdateValueTexts` 里 round 模式按整数比较、F1 模式按 value*10 比较，命中时跳过 `text` 赋值；`extraValueText` 同样按整数缓存。模式切换（round↔F1）会强制刷一次。
- **采纳 3（反射结果缓存）**：`GetIsInvincible` 不再每帧 `FieldInfo.GetValue` 装箱。新增 `_cachedInvincible` + `_nextInvincibleProbeTime`，`DoUpdate` 中按 0.25s 间隔探一次，shield 显隐对延迟不敏感。
- `BindTarget` 切目标时把上述 6 个缓存字段统一重置为 `int.MinValue` / `false` / `0f`，避免显示上一个玩家的状态或文字。
- **未采纳**：所谓“对象池泄漏导致长时间游玩卡顿”——`_pool.Count` 受 `CfgMaxNearbyCount`（默认 3）天然兜住，且 `OnConfigChanged → ClearAll` 已经在配置变化时全销毁重建，无真实膨胀路径，不动。
- **AI 已经做对没看到**：`RefreshNearby` 的 `[Dist]` StringBuilder 已在 `if (DebugEnabled && ...)` 内部；`[ExtraVal]` 同样；`TeamRosterTracker` 的 `DontDestroyOnLoad` 与销毁/掉线判断均已存在。
- **微优化未采纳**：`Component.gameObject` 缓存、`Target.data` 重复访问缓存、`Mathf.Sin` 复用——maxN=3 的循环里每帧只多几十纳秒，性价比极低。
- 构建：`dotnet build ... PlayersInfo.csproj -c Release` → 0 warnings、0 errors，dll 直接落入 `测试环境/BepInEx/plugins/PlayersInfo.dll`。

## 2026-05-13 PEAK 1.62.a compatibility check

- 用户更新 PEAK 1.62.a 反编译后复核 PlayersInfo。`Assembly-CSharp`、`Assembly-CSharp-firstpass`、`Photon*`、`Unity.TextMeshPro` 等关键程序集与 1.61.b 反编译源码哈希一致。
- PlayersInfo 相关类未变化：`GUIManager`、`StaminaBar`、`CharacterData`、`ThornOnMe`、`Item`、`Character` 相关公开访问路径未在反编译源码层面改变。
- Release 构建通过：`dotnet build ... PlayersInfo.csproj -c Release`，0 warnings，0 errors。暂不需要代码更新。

## 2026-05-10 发布 0.1.0

- `0.1.0` 是 PlayersInfo 首个公开发布版本，发布文档按当前功能写首发说明，不写未发布实验历史。
- 按其他 MOD 发行目录格式补齐 `发行\0.1.0\README.md`、`CHANGELOG.md`、`manifest.json`、`PlayersInfo.dll` 和 `wuyachiyu-PlayersInfo-0.1.0.zip`。
- 当前发布目录内有 `icon.png`；本轮未重新处理图标内容。
- 同步 `AssemblyInfo.cs` 版本为 `0.1.0.0`，与 `.csproj <Version>` 和 `PlayersInfoPlugin.PluginVersion` 的 `0.1.0` 保持一致。
- release 文档只描述队友 HUD、体力、状态、物品栏、距离过滤和诊断开关。
- 构建结果：Release 通过，0 warnings，0 errors；发布包包含 `README.md`、`CHANGELOG.md`、`manifest.json`、`PlayersInfo.dll`、`icon.png`。

## 2026-05-08 FontHelper 抽取

- 新建 `Helpers\FontHelper.cs`，将 CJK 字体兜底逻辑从 `LocalStaminaBarPatch` 和 `TeammateBarsCoordinator` 两处重复实现抽成共享工具。
- 四级兜底：`AscentUI.text.font` -> `GUIManager.heroDayText.font` -> 全场 TMP -> `TMP_Settings.defaultFontAsset`。即便都拿不到中文字体也不返 null，避免 TMP fallback 到系统字体变粗或丢描边。
- `LocalStaminaBarPatch` 和 `TeammateBarsCoordinator` 全部文本 font 调用统一走 `FontHelper.GetChineseCapable()`，删除重复方法定义和 `_cachedCnFont` 静态缓存。
- 编译通过：`dotnet build ... PlayersInfo.csproj -c Release`，0 error。

## Latest Log Review

Source logs: `C:\Users\Administrator\Desktop\MOD\PEAK\主客机日志\Log (3).txt`

Findings:

- No PlayersInfo exception stack was found in the recent host/client log.
- Unity/game/other-mod errors exist, including Photon object removal and inventory/full item issues, but they are not attributable to PlayersInfo from the available stack traces.
- PlayersInfo generated high-volume normal-play diagnostics: `[Dist]`, `[ExtraVal]`, and related HUD internals.

Action taken:

- Added `Diagnostics.DebugLogging=false` config.
- Routed verbose distance, extra stamina, clone dump, clone extra, and extra-bar build logs through `PluginLogger.Debug`.
- Build passed and output `PlayersInfo.dll` to the test environment.

## Stable Features

- Teammate HUD aggregation through `TeammateBarsCoordinator`.
- Teammate inventory row through `TeammateInventoryRow`.
- Local stamina and extra stamina display through `LocalStaminaBarPatch`.
- GUI initialization guarded by `GUIManagerReadyPatch`.
- Distance filtering and max nearby teammate count config.

## Current Verification

- Small party testing is considered stable.
- Scene reload, death/revive, and teammate HUD rebuild paths have prior successful tests.
- Latest third-party log review did not show PlayersInfo runtime failures.
