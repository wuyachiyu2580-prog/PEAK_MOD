# PlayersInfo 0.2.2

PlayersInfo adds a compact, read-only teammate HUD to PEAK. It shows nearby players' stamina, status, distance, and inventory without changing gameplay or sending gameplay RPCs.

## What's new in 0.2.2

### Fixes

- Fixed teammate stamina numbers moving to the middle of the full stamina bar instead of staying with the green fill.
- Fixed teammate stamina bars overflowing while Infinite Stamina is active. The bar now freezes at the last reliable stamina value until the effect ends.

### Configuration

- Added `ShowExtraStaminaCap`. When enabled, teammate extra stamina is shown as `XX/XX`; when disabled, it is shown as `XX` only, hiding the extra-stamina cap.

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

Old boolean `EnableInventoryRow` values migrate automatically: `true` becomes `ContentsOnly`, and `false` becomes `Disabled`.

## Notes

- PlayersInfo is built for PEAK 2.1.a.
- PEAKLib.ModConfig is optional. When installed, PlayersInfo provides English and Chinese configuration labels.
- For large lobbies, reduce `MaxNearbyCount` or `NearbyRange` to keep the HUD compact.
