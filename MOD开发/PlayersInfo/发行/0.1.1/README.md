# PlayersInfo 0.1.1

PlayersInfo gives you a small teammate panel in PEAK. It shows nearby teammates' stamina, temporary stamina, status, distance, and inventory, so you can quickly see who is struggling and what the team is carrying.

## What's new in 0.1.1

- Bugfix: teammate bars no longer briefly blank out / "jump" during transient network jitter. Player identity is cached across momentary `Owner == null` blips, and bars are kept visible for up to 1.5 s when a teammate temporarily drops off the team list (Photon reconnect, teleport, scene transition).
- Bugfix: `EnableStaminaBar`, `Anchor`, `OffsetX`, and `OffsetY` now actually affect the teammate HUD. Config sections are merged into `Display` and `Advanced`; existing old-section values are migrated on load.
- Compatibility: when PEAKLib.ModConfig is installed and the game language is Chinese, PlayersInfo localizes its ModConfig sections/options into Chinese. Without ModConfig installed, the mod works normally.
- Bugfix: teammate temporary stamina values no longer get clipped at the screen edge. The extra-stamina number now chooses the safe side of the bar based on the configured HUD anchor.
- Bugfix: teammate stamina bars no longer flicker / fully rebuild when any unrelated config entry changes (only structural toggles like `Enabled`, `ShowStaminaValue`, `EnableInventoryRow` rebuild the HUD now).
- Bugfix: a single teammate bar no longer flickers in and out when their distance is right at the `NearbyRange` boundary (5 m hysteresis margin added).
- Lower per-frame overhead: the teammate HUD no longer rewrites stamina text or runs reflective lookups every frame when nothing has changed.
- Cleaner scene transitions: cached teammate item icons are now properly released when scenes reload or the mod is disabled.
- No gameplay changes versus 0.1.0; PlayersInfo remains a display-only HUD mod.

## Installation

1. Install BepInEx for PEAK.
2. Put `PlayersInfo.dll` into `BepInEx/plugins`.
3. Start the game once to generate the config file.

## Features

- Teammate stamina bars with optional numeric values.
- Temporary stamina display, including extra stamina from status effects.
- Teammate inventory row: main slots, temp slot, backpack slot, and backpack contents.
- Nearby teammate filtering by distance.
- Limit how many nearest teammates are shown.
- Debug logging switch for troubleshooting without flooding normal logs.

## Configuration

The config file is generated at:

`BepInEx/config/com.players.info.cfg`

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| Display | Enabled | true | Master switch for the mod. |
| Display | EnableStaminaBar | true | Show teammate stamina bars. |
| Display | ShowStaminaValue | true | Show numeric stamina values. |
| Display | EnableInventoryRow | true | Show teammate inventory rows. |
| Display | RoundStaminaValue | true | Round stamina values to whole numbers. |
| Display | Anchor | TopLeft | HUD anchor corner. |
| Display | OffsetX | 0 | Extra horizontal offset. |
| Display | OffsetY | 0 | Extra vertical offset. |
| Display | NearbyRange | 30 | Max teammate distance in meters. `0` means unlimited. |
| Display | MaxNearbyCount | 3 | Maximum number of nearest teammates shown. |
| Advanced | DebugLogging | false | Enable verbose diagnostic logs. |

## Notes

- PlayersInfo only displays information. It does not change stamina, inventory, health, or teammate state.
- In very large lobbies, you may want to lower `MaxNearbyCount` or `NearbyRange` to keep the HUD compact.
