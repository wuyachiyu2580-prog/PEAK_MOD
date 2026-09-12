# PlayersInfo 0.2.4

PlayersInfo adds a compact, read-only teammate HUD to PEAK. It shows nearby players' stamina, status, distance, and inventory without changing gameplay or sending gameplay RPCs.

## What's new in 0.2.4

### Fixes

- Dead teammates are no longer shown by the teammate stamina bar display.(maybe?)

## What's new in 0.2.3

### Status icons

- Added `AfflictionIconDisplayMode` with three choices: show all icons, hide teammate icons, or hide all icons.
- Icon visibility changes apply at runtime while preserving status bars, colors, widths, and numbers.

### Fixes

- Fixed dead and downed teammates remaining visible after moving out of range.
- Dead teammates use their last living position for distance checks; normal and downed teammates use their current body position.
- Fixed hunger countdown placement when stamina reaches zero. The countdown is centered in the available stamina area after status bars are excluded, instead of moving to a separate fixed position.
- Unified low-frequency HUD data refreshes to reduce unnecessary updates.
- Normal diagnostic output now follows `DebugLogging`; it is quiet by default.

## Installation

1. Install BepInEx for PEAK.
2. Put `PlayersInfo.dll` into `BepInEx/plugins`.
3. Start the game once to generate the configuration file.

## Configuration

The configuration file is generated at:

`BepInEx/config/com.players.info.cfg`

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| Display | Enabled | true | Enable or disable all PlayersInfo features. |
| Display | EnableStaminaBar | true | Show teammate stamina and status rows. |
| Display | ShowStaminaValue | true | Show numeric stamina, status, and countdown values. |
| Display | ShowExtraStaminaCap | true | Show teammate extra stamina as `XX/XX`; off shows `XX` only. |
| Display | AfflictionIconDisplayMode | ShowAll | `ShowAll`, `HideTeammates`, or `HideAll`. |
| Display | EnableInventoryRow | ContentsOnly | `Disabled`, `ContentsOnly`, or `ContentsAndJetpackFuel`. |
| Display | Anchor | BottomLeft | HUD corner. |
| Display | OffsetX | 0 | Horizontal offset in pixels. |
| Display | OffsetY | 0 | Vertical offset in pixels. |
| Display | NearbyRange | 30 | Maximum teammate distance in metres. `0` means unlimited. |
| Display | MaxNearbyCount | 3 | Maximum number of nearby teammates shown. |
| Display | TeammateSortMode | Stable | Keep rows stable or order them by distance. |
| Display | SpectatorNearbyCenter | ObservedCharacter | Use the local or observed character as the spectator distance center. |
| Display | RoundStaminaValue | true | Show whole numbers instead of one decimal place. |
| Advanced | DebugLogging | false | Enable verbose troubleshooting logs. |

`AfflictionIconDisplayMode` values are localized by PEAKLib.ModConfig when available. In Chinese they are shown as `不隐藏图标`, `隐藏队友图标`, and `隐藏全部图标`.

Old boolean `EnableInventoryRow` values migrate automatically: `true` becomes `ContentsOnly`, and `false` becomes `Disabled`.

## Notes

- PlayersInfo is built for PEAK 2.4.b.
- PEAKLib.ModConfig is optional. When installed, PlayersInfo provides English and Chinese configuration labels.
- PlayersInfo remains display-only and does not modify character state or send gameplay RPCs.
- For large lobbies, reduce `MaxNearbyCount` or `NearbyRange` to keep the HUD compact.
