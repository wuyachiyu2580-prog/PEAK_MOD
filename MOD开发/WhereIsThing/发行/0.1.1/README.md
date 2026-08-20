![141594](https://bee-reg-ab.imagency.cn/p/3ee253f22bdd98f1539ea9fcb7ac9ba4.png)
![141595](https://bee-reg-ab.imagency.cn/p/a3aafb5894408fa1f5bda16726df6594.png)
![141596](https://bee-reg-ab.imagency.cn/p/66d3bfa86df766b77969e0306d9953eb.png)

# WhereIsThing 0.1.1

WhereIsThing places location labels on selected items, luggage, hazards, landmarks, and visible amulet fragments on statues in PEAK.

## Installation

Install BepInEx for PEAK, then place `WhereIsThing.dll` in the game's `BepInEx/plugins` folder.

## How to use

Press `Alt+C` to open the preset window. Choose a built-in or custom preset, then close the window and press `C` to scan.

The scan range controls in the lower part of the window support:

- Ground items
- Held items
- Items inside dropped backpacks
- Amulet fragments shown on statues

Each player controls their own scan range, display mode, and display duration. The host does not override these personal settings.

## Presets and multiplayer

The three built-in presets are available even when the host does not have WhereIsThing installed:

1. Survival Medical
2. Achievement
3. Ascent 8 amulets

Hosts can create, edit, rename, publish, hide, and delete custom presets. Clients receive published presets when the sharing mode is set to `Share: Published presets`.

After a master-client switch, clients briefly keep the previous shared presets while waiting for the new host. They fall back to the built-in presets when compatible shared data is unavailable.

## Configuration

The configuration file is `BepInEx/config/com.wuyachiyu.WhereIsThing.cfg`.

- `General.Enabled`: enable or disable the mod. Default: enabled.
- `General.ScanKey`: scan hotkey. Default: `C`.
- `General.WindowKey`: window hotkey used with Alt. Default: `C`.
- `General.ScanMode`: `Persistent` or `Timed`. Default: `Persistent`.
- `General.DisplayDurationSeconds`: label duration in Timed mode. Default: `8` seconds.
- `Display.NameLanguage`: follow the game language, or force English/Simplified Chinese.
- `Display.MaxDistance`: maximum label distance in metres. `0` means unlimited.
- `Display.FontSize`: label font size.
- `Display.LabelFont`: font used by English labels. Chinese text uses a Chinese-capable game font when needed.
- `Display.ShowOffscreenDirection`: show a direction indicator for targets outside the screen.

PEAKLib.ModConfig is optional. When installed, WhereIsThing provides localized configuration names, descriptions, and enum choices without changing the original configuration keys or values.

## Notes

WhereIsThing only reads existing game objects. It does not create, move, pick up, or modify items, luggage, hazards, landmarks, or statue fragments.

Large presets can create many labels and may reduce frame rate. Selecting only the targets you need is recommended.
