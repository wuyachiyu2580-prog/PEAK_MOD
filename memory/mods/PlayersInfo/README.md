# PlayersInfo

Last updated: 2026-05-10

## Purpose

PlayersInfo is a read-only teammate HUD mod for PEAK. It displays nearby teammates' stamina, temporary stamina, status, distance, and inventory in a compact panel.

The mod should not change game business logic and should not actively send gameplay RPCs. It owns only HUD presentation, local config, and diagnostic logging.

## Current State

- Version is `0.1.0`.
- `0.1.0` is the first public release.
- Teammate HUD is coordinated through `TeammateBarsCoordinator`.
- Local stamina display is patched through `LocalStaminaBarPatch`.
- Teammate inventory display is handled by `TeammateInventoryRow`.
- TMP text readability is centralized through `TmpOutlineHelper`; font selection is centralized through `FontHelper`.
- Verbose diagnostics are behind `Diagnostics.DebugLogging=false` by default.

## Related Mods

- `WhySoLaggy`: use profiling there if HUD performance becomes suspicious.

## Handoff Notes

- Read `FILES.md` for source paths and build command.
- Read `RECENT.md` for latest verified work and release status.
- Read `DECISIONS.md` before changing architecture or gameplay boundaries.
- Read `TODO.md` before starting new PlayersInfo work.
