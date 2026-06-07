# LanternShootZombiesNight

A companion mod for PEAK that reworks the lantern into a central survival resource. Fuel is configurable, actions restore warmth, cold resistance scales with a multiplier, drain speed adapts to context, and a HUD keeps everything visible.

Requires **BlackPeakRemix** and **ShootZombies**.

## What This Mod Changes

### Lantern Fuel

- Overrides lantern fuel capacity (30 / 60 / 90 / 120 / 240 seconds, or infinite)
- Campfire refill: lighting a campfire tops up the lantern; standing near one pauses fuel drain
- Reserve warmth pool: overflow from restores is banked and consumed before main fuel
- Auto re-light: if fuel is added to a snuffed lantern it re-lights automatically

### Warmth Restore

- Killing zombies, opening chests, using items, eating food, cooking, blowing the bugle (AoE), rescuing teammates, and team huddle all restore lantern fuel
- A unified restore value can replace per-source values for chest / item / consume / cooking
- Variety limiter: the same source type is blocked within the last N restores (bugle and self-limiting items are exempt)
- Cooked food bonus multiplier for properly cooked meals

### Cold Resistance

- Adjustable multiplier (0–1) controlling how much cold the lit lantern blocks
- Three-layer interception: cold addition scaling, heat-emission suppression, and status-field suppression work together for a linear feel
- Night-only: all three layers are bypassed during daytime so the game behaves naturally

### Dynamic Fuel Drain

- BPR flashlight mode increases drain (configurable multiplier)
- Companion proximity decreases drain (scales with up to 3 nearby players)
- Solo penalty increases drain after a grace period
- All sources multiply together; 1× sources are automatically removed

### HUD

- Code-built panel showing a fuel bar (green → yellow → red), multiplier line, last restore source, and day/night info
- Eight screen positions and four size presets
- Day/night tracker reads DayNightManager time and detects BPR darkness via reflection

### Hotkey

- Press a configurable key (default F8) to spawn a missing bugle and/or backpack

### Multiplayer

- The host controls gameplay-critical settings; clients receive them via Photon room properties
- Clients' local values are backed up and restored when leaving the room
- HUD position, size, and hotkey stay local

## Main ModConfig Sections

- `LanternCold` — fuel capacity, cold-resistance multiplier, campfire refuel
- `Restore` — master switch, unified value, per-source warmth, huddle settings
- `Advanced` — radius, cooldown, variety count, reserve ratio, HUD, day/night display, hotkey
- `DrainMultiplier` — flashlight, companion, solo multipliers and grace period

All config descriptions are bilingual (Chinese / English), auto-detected at startup.

## Installation

1. Install `BepInExPack PEAK`
2. Install `BlackPeakRemix`
3. Install `ShootZombies`
4. Install `ModConfig`
5. Place `Lantern_ShootZombies_Night.dll` into `PEAK/BepInEx/plugins/`

## Package Contents

- `Lantern_ShootZombies_Night.dll`

## Contact

- Author: 乌鸦吃鱼
- QQ Group: 1093172647

## Notes

- Cold-resistance multiplier requires `EnableWarmthReduction = true` to take effect
- In GameDefault fuel mode the mod compensates drain multiplier and campfire protection on top of the game's own fuel logic
- For multiplayer, use the same mod version on host and clients for best results
