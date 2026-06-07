# ItemInfoCN

A Chinese-localized item info display mod for PEAK. When holding any item, a detailed tooltip appears showing effects, afflictions, weight, uses, cooking status, and more — all translated into Chinese with color-coded text.

Based on **ItemInfoDisplayForkedCN** by chuxiaaaa, rewritten and restructured.

## What This Mod Does

### Item Effect Display

- Shows all item effects when held: hunger, stamina, poison, thorns, injury, cold, heat, drowsy, curse, shield, spores
- Effect values displayed with color-coded text matching the game's status colors
- Positive effects shown in green, negative in red
- Per-second and over-time effects calculated and displayed clearly

### Affliction Display

- Displays detailed affliction info: speed boost, infinite stamina, clear status, adjust status, sunscreen, invincibility, etc.
- Compound afflictions (e.g. speed boost → drowsy aftereffect) shown with full chain

### Special Item Descriptions

- Unique descriptions for 40+ item types: bugle, compass, lantern, torch, rope, backpack, dynamite, scorpion, mushroom, magic bean, rescue hook, vine shooter, and many more
- Scorpion: shows poison damage based on current health
- Mushroom: shows current random effect if MushroomManager is available
- Lantern: shows remaining fuel and heat field effects
- Cooking: shows cook count, overcook warning, explosion warning
- Rope: shows remaining length and max length

### AOE & Range Effects

- Processes AOE objects and their children recursively
- Shows range, duration, minimum falloff factor
- Displays per-second effects when TimeEvent is present

### Weight & Uses

- Shows item carry weight (with Ascent weight modifier)
- Shows remaining uses for multi-use items

### EasyBackpack Compatibility

- Detects EasyBackpack mod and adjusts backpack description accordingly

## Config Options

All options are under the `ItemInfoDisplay` section in BepInEx config:

| Option | Default | Description |
|--------|---------|-------------|
| Font Size | 20 | Text font size |
| Outline Width | 0.08 | Text outline width |
| Line Spacing | -35 | Line spacing |
| Size Delta X | 550 | Horizontal container width |
| Force Update Time | 1s | Interval for forced tooltip refresh |

## Installation

1. Install `BepInExPack PEAK`
2. Place `ItemInfoCN.dll` into `PEAK/BepInEx/plugins/`

## Package Contents

- `ItemInfoCN.dll`

## Contact

- Author: 乌鸦吃鱼
- QQ Group: 1093172647

## Notes

- Compatible with game version **1.61.a**
- This mod is a Chinese-localized rewrite of ItemInfoDisplayForkedCN; it uses a different plugin GUID (`com.wuyachiyu.ItemInfoCN`) and can coexist or replace the original
- No hard dependencies on other mods; EasyBackpack is soft-detected at runtime
