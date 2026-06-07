# LanternShootZombiesNight

A PEAK mod that turns the lantern into a core survival tool. Configurable fuel, warmth restore from various actions, cold resistance while holding a lit lantern, dynamic drain speed, and a HUD to keep track of everything.

Requires **BlackPeakRemix** and **ShootZombies**.

---

## Features

### Lantern Fuel

- Choose fuel capacity: 30 / 60 / 90 / 120 / 240 seconds, or infinite
- Lighting a campfire refills the lantern; standing near one pauses fuel drain
- Reserve warmth pool: overflow from restores is banked and used before main fuel runs out
- Auto re-light: a snuffed lantern re-lights automatically when fuel is added

### Warmth Restore

The following actions restore lantern fuel:

- Kill zombies (area-based), open chests, use items, eat food, cook, blow the bugle (AoE), rescue teammates, huddle with teammates
- Unified value mode: chest / item / eat / cook can share a single restore amount
- Variety limiter: the same source type is blocked within the last N restores (bugle and rope items are exempt)
- Cooked food bonus multiplier for properly cooked meals

### Cold Resistance

- Adjustable multiplier (0–1): 1 = full cold block, 0.5 = half cold, 0 = no protection
- Night-only — daytime behavior is untouched
- Requires `EnableWarmthReduction = true` in config

### Night Cold Warning

A one-time screen warning at nightfall (auto-hides after 15s) when cold resistance cannot work:

- ShootZombies "Night Cold" is turned off
- Current ascent is below 5 (no night cold in the base game)

### Dynamic Fuel Drain

- BPR flashlight mode: faster drain (configurable multiplier)
- Teammates nearby: slower drain (scales with up to 3 players)
- Going solo: faster drain after a grace period

### Upgrade System

Automatic passive upgrades earned through warmth-restore events:

- Each restore event grants +1 upgrade point
- Capacity and Efficiency trees alternate (capacity first when tied)
- Cost per level: 50 → 100 → 150 → 200 → 250
- Capacity: +20% fuel cap per level (up to ×2.0 at Lv5)
- Efficiency: −8% fuel drain per level (up to ×0.6 at Lv5)
- Progress displayed on HUD line 4
- In multiplayer, the host handles all upgrades to prevent cheating

### HUD Panel

- Fuel bar (green → yellow → red), drain multiplier, last restore source, day/night info
- Upgrade progress (level, next cost, points)
- 8 screen positions × 4 size presets
- Optional day count, time, and BPR darkness display

### Hotkey

- Press F8 (rebindable) to spawn a missing bugle and/or backpack

### Multiplayer Sync

- The host controls gameplay settings; clients sync automatically within 1 second
- Upgrade requests go through the host for validation
- HUD position, size, and display preferences stay local to each player

### Mute Option

- Toggle to silence all zombie and tornado sounds

### Performance Fixes

This mod also patches ShootZombies and BlackPeakRemix for better performance:

- Inventory icons only refresh when the item actually changes (not every frame)
- Zombie UI refresh rate is throttled to prevent lag with many zombies
- Eliminates redundant per-frame reflection calls and component lookups
- Campfire proximity checks use an event-driven list instead of scanning every 2 seconds
- Fuel sync messages are batched to avoid flooding the network

---

## Config Sections

All settings are managed through **ModConfig**. Descriptions auto-detect Chinese / English.

| Section | Contents |
|---------|----------|
| `LanternCold` | Fuel capacity, cold-resistance toggle & multiplier, campfire refuel, reserve warmth max |
| `Restore` | Master switch, unified value, per-source warmth, huddle settings, radius, cooldown, variety count |
| `DrainMultiplier` | Flashlight / companion / solo drain multipliers and grace period |
| `Display` | HUD preset/position/size, day/night display, mute toggle, 3D indicator, upgrade toggle, hotkey |

---

## Installation

1. Install `BepInExPack PEAK`
2. Install `BlackPeakRemix`
3. Install `ShootZombies`
4. Install `ModConfig`
5. Place `Lantern_ShootZombies_Night.dll` into `PEAK/BepInEx/plugins/`

## FAQ

**Cold resistance not working?**

Check the following:
1. Game version is **1.61.b or later** (older versions have a bug that prevents night cold entirely)
2. ShootZombies config has **Night Cold** enabled
3. Current ascent is **5 or above** (no night cold below ascent 5)
4. `EnableWarmthReduction` is set to `true`

**Multiplayer settings not syncing?**

Make sure the host and all clients are running the same mod version.

---

## Contact

- Author: 乌鸦吃鱼
- QQ Group: 1093172647
