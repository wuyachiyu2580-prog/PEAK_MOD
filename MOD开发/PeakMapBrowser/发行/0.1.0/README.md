# PEAK Map Browser

A BepInEx plugin for browsing peakmap.top community maps inside PEAK.

## Usage

Press `/` on the main keyboard to open or close the map browser.

This is the regular slash key (`Slash`), not the numpad divide key (`KeypadDivide`).

The plugin can browse maps, view details, download map JSON files, and upload local JSON saves.

## JSON Map Requirement

Downloaded and uploaded files are TerrainCustomiser map JSON files.

To load and play these maps, use one of the following mods:

- `TerrainCustomiser`
- `TerrainCustomiserCN`

PEAK Map Browser does not load custom maps by itself.

Downloaded JSON files are saved to:

```text
Application.persistentDataPath/TerrainCustomiser/Map Saves
```

## Config

```text
ApiBaseUrl = https://peakmap.top
Language = auto
PageSize = 12
ToggleKey = Slash
```

`Language = auto` follows the game's language. Set it to `zh` or `en` to force a language.
