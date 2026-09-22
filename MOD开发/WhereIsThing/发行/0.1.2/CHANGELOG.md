# Changelog

## 0.1.2

- Clarified that player-name labels are for fun only: late joins and missing placement information can leave names unavailable, while some objects only show their item name and distance.
- Added a built-in `Player Placed` preset and a global `Player names` switch for supported placed targets.
- Added labels for placed checkpoint flags, shelf shrooms, bounce shrooms, cloud fungus, scout cannons, chain-shooter vines, pitons, and rope variants.
- Merged duplicate placed-target entries into their item entries and migrated existing selections. Magic bean vines show names and distances without guessing the planter.
- Improved hammered-piton and rope detection, including delayed object initialization and separate rope/anchor ownership.
- Updated placed-target detection for PEAK 2.4.b, excluded system ropes, and fixed beach breakable bridges being mistaken for chain-shooter vines.
- Thrown mushrooms only show a player name when a unique local throw record identifies the player; other targets retain their name and distance labels.
- Removed custom inventory cleanup for rope shooters and constructables, leaving item consumption and placement to the game.
- Corrected categories for jetpacks, rocketpacks, healing items, deployables, mystical variants, creatures, honeycomb, and chess pieces.
- Kept the statue fragment name mapping and geyser positioning fixes.
- Added a maximum-distance slider with 20-metre steps (`0` means unlimited) and tidied the scan settings and player-name controls.
- Split item names, player names, and distances into stable rows with lighter outlines and shared shadows.
- Spread target discovery across frames and cached item definitions, placed-target data, player names, and unchanged label text to reduce repeated work.
- Reduced each label from fifteen text objects to three text rows and one arrow, without modifying the game's font materials.
- Lowered the UI canvas sorting order to improve compatibility with TrueFinalAscent dropdown lists while keeping the selection window interactive.
- Updated ModConfig menu and language-change support. Translations only affect this mod's labels and dropdown text; saved values, presets, and multiplayer sharing remain compatible.

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
