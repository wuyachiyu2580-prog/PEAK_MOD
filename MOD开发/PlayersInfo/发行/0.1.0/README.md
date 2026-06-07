# PlayersInfo 0.1.0

PlayersInfo gives you a small teammate panel in PEAK. It shows nearby teammates' stamina, temporary stamina, status, distance, and inventory, so you can quickly see who is struggling and what the team is carrying.

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
| General | Enabled | true | Master switch for the mod. |
| Features | EnableStaminaBar | true | Show teammate stamina bars. |
| Features | ShowStaminaValue | true | Show numeric stamina values. |
| Features | EnableInventoryRow | true | Show teammate inventory rows. |
| Features | RoundStaminaValue | true | Round stamina values to whole numbers. |
| Layout | Anchor | TopLeft | HUD anchor corner. |
| Layout | OffsetX | 0 | Extra horizontal offset. |
| Layout | OffsetY | 0 | Extra vertical offset. |
| Nearby | NearbyRange | 30 | Max teammate distance in meters. `0` means unlimited. |
| Nearby | MaxNearbyCount | 3 | Maximum number of nearest teammates shown. |
| Diagnostics | DebugLogging | false | Enable verbose diagnostic logs. |

## Notes

- PlayersInfo only displays information. It does not change stamina, inventory, health, or teammate state.
- In very large lobbies, you may want to lower `MaxNearbyCount` or `NearbyRange` to keep the HUD compact.
