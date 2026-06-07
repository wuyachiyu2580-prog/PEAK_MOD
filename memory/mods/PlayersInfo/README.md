# PlayersInfo

Last updated: 2026-06-04

## Purpose

PlayersInfo is a read-only teammate HUD mod for PEAK. It displays nearby teammates' stamina, temporary stamina, status, distance, and inventory in a compact panel.

The mod should not change game business logic and should not actively send gameplay RPCs. It owns only HUD presentation, local config, and diagnostic logging.

## Current State

- Version is `0.1.1`.
- `0.1.0` was the first public release; `0.1.1` is the current maintenance/fix line.
- Teammate HUD is coordinated through `TeammateBarsCoordinator`.
- Local stamina display is patched through `LocalStaminaBarPatch`.
- Teammate inventory display is handled by `TeammateInventoryRow`.
- TMP text readability is centralized through `TmpOutlineHelper`; font selection is centralized through `FontHelper`.
- Config sections are now merged into `Display` and `Advanced`; old `General` / `Features` / `Layout` / `Nearby` / `Diagnostics` values migrate on load.
- Verbose diagnostics are behind `Advanced.DebugLogging=false` by default.
- If PEAKLib.ModConfig is installed and the game language is Chinese, PlayersInfo localizes its ModConfig section names, option names, enum values, and descriptions. Without ModConfig, the mod still works normally.
- 2026-05-24 fix is in the current `0.1.1` DLL: teammate temporary stamina values choose the HUD-safe side and should no longer be clipped to only the ones digit near the screen edge.
- 2026-05-30 release docs are synced with that fix. `发行/0.1.1/wuyachiyu-PlayersInfo-0.1.1.zip` exists as of 2026-06-04 and contains the synced docs plus the fixed 64000-byte DLL.

## Related Mods

- `WhySoLaggy`: use profiling there if HUD performance becomes suspicious.

## Handoff Notes

- Read `FILES.md` for source paths and build command.
- Read `RECENT.md` for latest verified work and release status.
- Read `DECISIONS.md` before changing architecture or gameplay boundaries.
- Read `TODO.md` before starting new PlayersInfo work.
