# Changelog

## 1.0.3

- Restored the compact scan-settings layout and aligned the player-name switch with the scan-mode controls.
- Added a built-in `Player Placed` fallback preset containing every currently supported owner-labelled item.
- Fixed hammered pitons not receiving labels or player names when their PhotonView sits above or below the named prefab object.
- Added a global `Player names` switch for player-placed targets with reliable Photon creator information.
- Added client-side labels for placed checkpoint flags, shelf shrooms, bounce shrooms, cloud fungus, scout cannons, chain-shooter vines, pitons, and rope variants.
- Kept magic bean vines client-side without showing a planter name because their room-object lifecycle does not preserve that owner information.
- Removed duplicate placed-target entries from the selection window and migrated existing placed-target selections to their item entries.
- Fixed bounce shroom naming and separated item name, player name, and distance into stable label rows with matching shadows.
- Fixed successful rope-shooter and constructable placement cleanup so spent items do not remain unusable in hand or inventory.
- Improved placed-object owner resolution across nested PhotonViews and rope-anchor hierarchies.
- Corrected placed-object detection for shelf shrooms, cloud fungus, chain-shooter vines, scout cannons, pitons, and rope anchors.
- Kept the existing statue gem mapping and geyser positioning fixes.

## 0.1.1

- Added a `Statue` scan range for selected amulets.
- Added labels for visible, uncollected amulet fragments held by the chapter 1-4 amulet statues.
- Statue fragments use the selected amulet names and do not become separate selectable targets.
- Added safer PEAKLib.ModConfig localization that only changes WhereIsThing's own visible names and descriptions.
- Preserved the original configuration sections, keys, enum values, preset data, and multiplayer sharing behavior.

## 0.1.0

- Initial release of WhereIsThing.
- Added location labels for items, luggage, hazards, and the Gloom Bell Tower.
- Added local presets, host-shared presets, and read-only preset selection for clients.
- Added three built-in fallback presets: Survival Medical, Achievement, and Ascent 8.
- Added support for creating, editing, renaming, publishing, hiding, and deleting custom presets.
- Each player can independently choose a preset and configure scan locations, display mode, and duration.
- Added master-client switch handling and built-in fallback presets when the host does not have the mod.
- Added English and Chinese UI text, target search, and ModConfig localization.
- Added selectable game fonts for location labels with automatic Chinese glyph fallback.
- Made location labels thinner by disabling bold text by default and reducing the outline weight.
- Font, bold, size, and language changes now apply to existing labels without rescanning.
