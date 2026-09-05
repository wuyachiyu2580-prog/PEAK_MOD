# Changelog

## 0.1.2

- Corrected item categories using the reviewed ItemSpawnerEnhanced prefab list: fixed Jetpack, Rocketpack, Heat Pack, Healing Dart, healing fungus, deployables, player-placement tools, mystical variants, creatures, honeycomb, and chess pieces without changing the existing category UI or placement behavior.
- Updated player-placed target discovery for PEAK 2.4.b: targets now use catalog-derived spawn prefabs, system ropes are excluded, delayed PhotonViews receive bounded retries, and thrown mushrooms only show a player name when a unique client-side throw record matches.
- Fixed beach breakable bridges being misidentified as player-placed chain launchers because both use `JungleVine` internally.
- Refined the four-TMP label style with thin outlines and shared SDF underlay shadows, using lighter styling for player names and distances while leaving direction arrows unoutlined.
- Fixed rope and rope-shooter labels being rejected when the rope owner PhotonView and the separate anchor-position PhotonView differ.
- Restored delayed rope classification when the Rope component or creator information is not ready during the first PhotonView pass.
- Split network discovery and scene discovery into focused internal partial modules and removed unreachable legacy scanners and unused helper parameters.
- Reworked target discovery into a frame-budgeted PhotonView index that processes at most 128 objects or 0.75 ms per frame while retaining the 0.5-second discovery interval.
- Cached item definitions and player-placed target metadata, moved hierarchy validation out of the per-frame label path, and staggered unregistered scene-target scans across frames.
- Reduced each location label from fifteen TMP text objects to three shared-material text rows plus one direction arrow, with a private shared outline material that leaves game font materials untouched.
- Kept the four location toggles and placed `Player names` beside them in one compact row.
- Removed the redundant `Locations` heading and added a top-right maximum-distance slider with 20-metre steps; `0` remains unlimited.
- Restored the compact scan-settings layout and aligned the player-name switch with the scan-mode controls.
- Added a built-in `Player Placed` fallback preset containing every currently supported owner-labelled item.
- Fixed hammered pitons not receiving labels or player names when their PhotonView sits above or below the named prefab object.
- Added a global `Player names` switch for player-placed targets with reliable Photon creator information.
- Added client-side labels for placed checkpoint flags, shelf shrooms, bounce shrooms, cloud fungus, scout cannons, chain-shooter vines, pitons, and rope variants.
- Kept magic bean vines client-side without showing a planter name because their room-object lifecycle does not preserve that owner information.
- Removed duplicate placed-target entries from the selection window and migrated existing placed-target selections to their item entries.
- Fixed bounce shroom naming and separated item name, player name, and distance into stable, consistently outlined label rows.
- Removed custom rope-shooter and constructable inventory cleanup so the game's native consume and placement flow remains authoritative.
- Reduced owner-name label overhead by caching player-name lookups and avoiding unchanged per-frame TMP text writes.
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
