# Changelog

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
