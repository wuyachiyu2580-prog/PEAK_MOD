# Changelog

## [0.1.4] - 2026-04-24

- Fixed zombie-hit warmth restore distance check being slightly off
  - The distance was measured from your torso to the zombie's root transform, but in PEAK the root transform does not move with the zombie
  - Now both sides use the torso position, matching the existing behavior on the player side
  - Effect: hit-distance gating is now accurate, especially for fast-moving or airborne zombies
- Fixed capacity upgrade not taking effect until you re-lit the lantern
  - Previously, buying a Capacity upgrade only raised the fuel cap on the next lantern spawn / re-light
  - Now existing lanterns are refreshed in place: `startingFuel` and the cached tracked fuel are bumped to the new cap immediately

**Known behavior (documented, not a bug):**
- When `EnableWarmthReduction` is ON and you are holding a lit regular lantern at night, the mod fully zeroes out scene warmth zones (`StatusField` with Cold < 0) so that the linear `LanternWarmthMultiplier` is the only cold-resistance knob. Side effects:
  - **Faerie Lantern**'s cold-cleansing aura is also suppressed in this window (it works via the same `StatusField` system). Turn `EnableWarmthReduction` OFF, or put the regular lantern away, to let it drain cold again.
  - **Milk (HealingDart)**: vanilla PEAK does not clear Cold at all — it only grants invincibility. This is unchanged by the mod.
  - **Huddle (teammates nearby)**: unaffected — huddle adds fuel to your lantern, it does not subtract Cold directly, so the reduction switch does not block it.

**Known issues:**
- Presets have not been re-tuned for the new config layout; use "Custom" for now
- Multiplayer sync has not been tested in this version yet

## [0.1.3] - 2026-04-21

- Config changes now take effect right away instead of waiting up to 5 seconds
- Fixed a bug where switching between multiple lanterns could show the wrong fuel level
- Upgrades are now validated by the host in multiplayer — no more client-side cheating
  - Non-host players see a "Host only" label on upgrade buttons
- Campfire detection is now much lighter on performance (no more scanning every 2 seconds)
- Fuel sync messages are sent less often, reducing network traffic during busy moments
- Presets (Casual / Balanced / Hardcore) now properly include the Upgrade System, 3D Fuel Indicator, and Mute Zombie Tornado settings
- Fixed a crash-on-load error caused by a missing game method (CampfireDestroyPatch)

**Known issues:**
- Presets have not been re-tuned for the new config layout; use "Custom" for now
- Multiplayer sync has not been tested in this version yet

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
