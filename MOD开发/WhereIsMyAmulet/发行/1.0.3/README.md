# WhereIsMyAmulet 1.0.3

Shows dropped amulets, backpacks containing amulets, and uncollected amulet fragments held by statues, along with their distances.

## Installation

Place `WhereIsMyAmulet.dll` in the game's `BepInEx/plugins` folder.

## Usage

Press `C` after entering a scene to scan the current scene. Labels use the game's current language for amulet names.

## Configuration

Configuration file: `BepInEx/config/com.wuyachiyu.WhereIsMyAmulet.cfg`

- `General.Enabled`: enable or disable the mod. Default: enabled.
- `General.ScanKey`: scan hotkey. Default: `C`.
- `General.ScanMode`: `Persistent` or `Timed`. Default: `Persistent`.
- `General.DisplayDurationSeconds`: number of seconds labels remain visible in Timed mode. Default: `8` seconds. Each scan starts the timer again.
- `Display.MaxDistance`: maximum label display distance in metres. Set to `0` for unlimited.
- `Display.FontSize`: label font size.
- `Display.LabelFont`: font used by English labels only. Chinese text may display as tofu boxes when the selected font does not contain Chinese characters.
![141555](https://bee-reg-ab.imagency.cn/p/e3c94f7dc727e947802d8d67d3314f42.png)

- `Display.ShowStatueFragments`: show amulet fragments held by regular amulet statues and Scout Statues.
- `Display.ShowOffscreenDirection`: show a direction indicator when a target is offscreen.

`PEAKLib.ModConfig` is an optional dependency. When installed, the configuration interface provides bilingual names and descriptions, as well as the `Persistent/Timed` dropdown.

## Notes

Scanning only reads scene objects that already exist. It does not create, pick up, or modify amulet fragments.
