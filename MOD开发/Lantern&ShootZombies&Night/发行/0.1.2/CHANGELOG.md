# Changelog

## [0.1.2] - 2026-04-20

- Reorganized config into 4 clear sections: LanternCold / Restore / DrainMultiplier / Display
- Removed cosmetic/local settings (MuteZombieTornado, ActivePreset) from multiplayer sync
- Added upgrade system: automatic level-up from warmth-restore events
  - Capacity and Efficiency alternate (capacity first when tied)
  - Fixed cost per level: 50 / 100 / 150 / 200 / 250
  - Each warmth-restore event grants +1 point; upgrades trigger automatically
- Added HUD 4th line: shows upgrade progress (level, cost to next, points)
- Fixed point inflation (was awarding warmth-seconds, now flat +1 per event)

**Known issues:**
- Presets have not been re-tuned for the new config layout; use "Custom" for now
- Multiplayer sync has not been tested in this version yet

## [0.1.1] - 2026-04-19

- Internal performance optimizations based on WhySoLaggy profiling data
- Reduced redundant lantern-slot lookups (multiple patches in the same frame now only search once)
- Consolidated repeated reflection reads in the fuel patch (4 reads → 1)
- Cached zombie and lantern component references to avoid per-frame lookups

## [0.1.0] - 2026-04-17

- Compatible with game version 1.61.b

## [0.0.3] - 2026-04-12

- Added night cold warning: one-time notification at nightfall when ShootZombies Night Cold is off or ascent < 5
- Added day/night tracker with optional HUD display and BPR darkness detection
- Reduced debug log flooding

## [0.0.1] - 2026-04-12

- Initial release
