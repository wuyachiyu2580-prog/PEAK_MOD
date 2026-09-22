# PlayersInfo

## 2026-09-21 发行文件准备完成

当前版本 `0.2.5` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/PlayersInfo/发行/0.2.5`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：ModConfig 新菜单与语言刷新、简中/繁中识别；沿用完整队友 HUD 使用说明。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前更新

开发/测试版本 `0.2.5`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。移除全局配置重注册并修正简中/繁中检测和语言通知。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.2.5` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

构建/测试/产物详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。下方旧日期发布和 hash 为历史记录。

Last updated: 2026-09-21

## Purpose

PlayersInfo is a read-only teammate HUD mod for PEAK. It displays nearby teammates' stamina, temporary stamina, status, distance, and inventory in a compact panel.

The mod should not change game business logic and should not actively send gameplay RPCs. It owns only HUD presentation, local config, and diagnostic logging.

## Current State

- Development version is `0.2.5`, targeting PEAK `2.4.b`.
- `0.1.0` was the first public release and `0.1.1` is retained as the previous maintenance/fix line.
- Teammate HUD is coordinated through `TeammateBarsCoordinator`.
- Local stamina display is patched through `LocalStaminaBarPatch`.
- Teammate inventory display is handled by `TeammateInventoryRow`.
- TMP text readability is centralized through `TmpOutlineHelper`; font selection is centralized through `FontHelper`.
- Config sections are now merged into `Display` and `Advanced`; old `General` / `Features` / `Layout` / `Nearby` / `Diagnostics` values migrate on load.
- Verbose diagnostics are behind `Advanced.DebugLogging=false` by default.
- If PEAKLib.ModConfig is installed and the game language is Chinese, PlayersInfo localizes its ModConfig section names, option names, enum values, and descriptions. Without ModConfig, the mod still works normally.
- 0.2.0 changes the default HUD anchor to `BottomLeft`. Persisted `TopLeft` values from older configurations migrate to `BottomLeft` on startup; other anchor values are preserved.
- 0.2.0 also includes PEAK 2.0.a/2.1.a compatibility fixes, petrify-aware current/cap extra-stamina display, and stable teammate bar ordering.
- 0.2.1 fixes the remaining display-target and UI stability gaps: teammate bars stay bound to stable player IDs, spectator data uses one resolved character target, and cloned bars use PlayersInfo-owned affliction components instead of vanilla global-observer state.
- 0.2.1 adds a local hunger countdown, teammate item durability bars, cooked-food icon coloring, and TMP-width-aware text placement. Teammate backpack content now follows the actual backpack capacity, including two-slot fanny packs and four-slot normal backpacks; an optional third inventory mode adds jetpack fuel.
- The local extra-stamina bar keeps PEAK's original hierarchy and runtime sizing. PlayersInfo does not reparent or manually stretch it; PEAK remains responsible for its indentation, black outline, lightning icon, animation, and petrify layout. `PI_LocalExtraStaminaValue` is attached inside `extraBarStamina` and displays only the current integer, for example `40`.
- Teammates retain their main stamina bars and values, but their cloned extra-stamina graphical bars stay disabled; the HUD-safe side value remains `current/cap` without `+`.
- `Display.OffsetY` defaults to `0` for every HUD anchor. PlayersInfo does not apply an automatic whole-HUD vertical shift; persisted user values remain respected.
- The spectator nearby center is a dropdown with `LocalCharacter` / `ObservedCharacter` (本机角色 / 被观看角色), defaulting to `ObservedCharacter`.
- Teammate main-stamina values are attached to the green `staminaBar` fill layer, so the number follows the visible fill instead of being hidden by or centered within the outer `fullBar` layout layer.
- Teammate infinite-stamina effects are detected through the synchronized `InfiniteStamina` affliction. PlayersInfo freezes the last reliable pre-effect teammate stamina value for the green fill and number, then resumes live synchronization when the effect ends. This is display-only and does not alter remote gameplay state.
- The previously reported teammate green-bar centering/overlap issue has been fixed and confirmed; future changes must preserve per-target fill sizing and must not use the full 100% bar center as the fill position.
- The retained `Display.EnableInventoryRow` key is migrated from the old boolean into an enum display mode. Existing `true` and `false` values map to `ContentsOnly` and `Disabled` respectively, without changing unrelated settings.
- 0.2.3 adds `Display.AfflictionIconDisplayMode`, defaulting to `ShowAll`. `ShowAll` keeps all status icons, `HideTeammates` hides only teammate icons, and `HideAll` hides teammate and local icons. The icon-only toggle preserves status bars, colors, widths, and numbers, and its three enum values plus descriptions are localized in English/Chinese through the optional ModConfig integration.
- 0.2.3 fixes distance filtering for dead and downed teammates. Normal and `passedOut`/`fullyPassedOut` entries use the current torso position; dead entries are now excluded entirely, so a corpse never produces a teammate bar. Invalid coordinates skip only that player. Roster members beyond range are not retained by the 1.5-second missing-roster grace window, while the existing 5 m hysteresis remains.
- 0.2.3 keeps the hunger countdown in the green stamina text while there is room. When stamina is truly zero (`<= 0.005`), it uses a separate yellow text centered in the live `maxStaminaBar` region after status width is excluded, and hides it when that region is inactive or too narrow. It never moves early because the green bar is temporarily small.
- Low-frequency local and teammate HUD refreshes use the unified `0.25s` cadence. High-frequency visual updates remain separately controlled where needed for responsive bar animation.
- Direct build output DLL path: `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\PlayersInfo.dll`. Future PlayersInfo builds write here directly.
- The 2026-05-24 temporary-stamina clipping fix is retained as historical `0.1.1` release context; the current DLL is the `0.2.4` release/profile deployment listed above.
- 2026-05-30 release docs are synced with that fix. `发行/0.1.1/wuyachiyu-PlayersInfo-0.1.1.zip` exists as of 2026-06-04 and contains the synced docs plus the fixed 64000-byte DLL.
- `发行/0.2.4/` contains the trial release DLL, manifest, README, CHANGELOG, icon, and `wuyachiyu-PlayersInfo-0.2.4.zip`. The release DLL is version `0.2.4.0`, size `98304` bytes, SHA-256 `973E9279F70ED5CC25CBC481673D2942394A35100001D4B027C3BD4E1E850BBB`; the zip SHA-256 is `C64A4154F7903EF4E1FA2CDBAC030AE11B8689C2BAD480987AB0DE2C330F95FA`.
- `发行/0.2.3/` remains the previous `0.2.3` release and was not overwritten.

## Related Mods

- The 0.2.0, 0.2.1, and 0.2.3 release notes remain historical. The 0.2.4 trial release is built cleanly, but a clean PEAK 2.4.b session is still required for the three icon modes, dead/downed range transitions, Book of Bones skeleton teammates, spectator targets, zero-stamina countdown placement, and multiplayer edge cases.

- `WhySoLaggy`: use profiling there if HUD performance becomes suspicious.

## Handoff Notes

- Read `FILES.md` for source paths and build command.
- Read `RECENT.md` for latest verified work and release status.
- Read `DECISIONS.md` before changing architecture or gameplay boundaries.
- Read `TODO.md` before starting new PlayersInfo work.
