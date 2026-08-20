# PlayersInfo

Last updated: 2026-08-19

## Purpose

PlayersInfo is a read-only teammate HUD mod for PEAK. It displays nearby teammates' stamina, temporary stamina, status, distance, and inventory in a compact panel.

The mod should not change game business logic and should not actively send gameplay RPCs. It owns only HUD presentation, local config, and diagnostic logging.

## Current State

- Version is `0.2.1`.
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
- Direct build output DLL path: `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\PlayersInfo.dll`. Future PlayersInfo builds write here directly.
- The 2026-05-24 temporary-stamina clipping fix is retained as historical `0.1.1` release context; the current DLL is the `0.2.1` profile deployment listed above.
- 2026-05-30 release docs are synced with that fix. `发行/0.1.1/wuyachiyu-PlayersInfo-0.1.1.zip` exists as of 2026-06-04 and contains the synced docs plus the fixed 64000-byte DLL.
- `发行/0.2.1/` is prepared with the current DLL, manifest, concise README, complete changelog, and icon. No ZIP was created per user request.

## Related Mods

- The 0.2.0 release notes remain historical. The 0.2.1 DLL is deployed to the current 2.0.a profile path above; in-game verification is still required for the restored native local extra-bar layout, spectator target switching, teammate extra-bar suppression, and backpack/fuel cases.

- `WhySoLaggy`: use profiling there if HUD performance becomes suspicious.

## Handoff Notes

- Read `FILES.md` for source paths and build command.
- Read `RECENT.md` for latest verified work and release status.
- Read `DECISIONS.md` before changing architecture or gameplay boundaries.
- Read `TODO.md` before starting new PlayersInfo work.
