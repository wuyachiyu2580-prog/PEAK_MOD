![141594](https://bee-reg-ab.imagency.cn/p/3ee253f22bdd98f1539ea9fcb7ac9ba4.png)
![141595](https://bee-reg-ab.imagency.cn/p/a3aafb5894408fa1f5bda16726df6594.png)
![141596](https://bee-reg-ab.imagency.cn/p/66d3bfa86df766b77969e0306d9953eb.png)

# WhereIsThing 0.1.3

WhereIsThing places location labels on selected items, luggage, hazards, landmarks, and visible amulet fragments on statues in PEAK.

## What's new in 0.1.3

- Distinguish terrain-grown mandrakes from medicinal roots before pickup, using their actual pickup identity. Both follow the Ground filter.
- Preserve custom selections while correcting the untouched Survival Medical preset so hidden mandrakes are not selected as medicinal roots.
- Group ordinary luggage into one entry, with expandable region and loot-pool filters. Revival statues now appear under Landmarks.
- Remove duplicate trap-chest labels and hide opened chests once their traps are no longer active.
- Add Current Segment / Whole Map scope. Current Segment includes the current segment and its entry camp, even when the distance limit is unlimited.
- Fix mixed-language names, backpack/statue suffixes, variant counts, and target summaries. Open windows and existing labels refresh after language changes.

## Installation

Install BepInEx for PEAK, then place `WhereIsThing.dll` in the game's `BepInEx/plugins` folder.

## How to use

Press `Alt+C` to open the preset window. Choose a built-in or custom preset, then close the window and press `C` to scan.

The scan range controls in the lower part of the window support:

- Ground items, including terrain-grown mandrakes and medicinal roots
- Held items
- Items inside dropped backpacks
- Amulet fragments shown on statues

Use `Current Segment` to limit labels to the current segment and its entry camp, or `Whole Map` to scan across segments. The maximum-distance setting applies within the selected scope; `0` removes the distance limit, not the segment filter. Carried items are checked at their current position.

Ordinary luggage is grouped into one entry. Expand the luggage section's advanced filters to choose individual regions and loot pools. Ancient, cursed, clown, and trap chests remain separate; revival statues are listed under Landmarks. Existing partial luggage selections are preserved.

Each player controls their own scan range, map scope, display mode, and display duration. The host does not override these personal settings.

## Presets and multiplayer

The four built-in presets are available even when the host does not have WhereIsThing installed:

1. Survival Medical
2. Achievement
3. Ascent 8 amulets
4. Player Placed

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
- `Display.MapScope`: `CurrentSegment` or `WholeMap`. Default: `CurrentSegment`, including the current segment and its entry camp.
- `Display.MaxDistance`: maximum label distance in metres within the selected map scope. `0` means unlimited distance within that scope. The window slider uses 20-metre steps.
- `Display.FontSize`: label font size.
- `Display.LabelFont`: font used by English labels. Chinese text uses a Chinese-capable game font when needed.
- `Display.ShowOwnerNames`: show player names for supported placed targets when the source can be identified. Default: enabled.
- `Display.ShowOffscreenDirection`: show a direction indicator for targets outside the screen.

PEAKLib.ModConfig is optional. When installed, WhereIsThing provides localized configuration names, descriptions, and enum choices without changing the original configuration keys or values.

## Notes

WhereIsThing only reads existing game objects. It does not create, move, pick up, or modify items, luggage, hazards, landmarks, or statue fragments.

Large presets can create many labels and may reduce frame rate. Selecting only the targets you need is recommended.

On custom maps without usable segment boundaries, objects that cannot be assigned to a segment are limited to the nearby 40 metres in `CurrentSegment` mode. Use `WholeMap` if you need labels beyond that fallback range.

Custom UI text is available in English and Chinese. Other game languages use English fallback text for custom labels, while item names follow the game's available localization.

**Player-name labels are for fun only, not reliable proof of who placed an object.** They depend on the information available to your client and may be missing or incomplete.

- If you join a session in progress, objects placed before you joined may not show a player name, especially mushrooms whose names depend on locally observed throws.
- Some placed objects may show only the item name and distance, without a player name. Magic bean vines do not show a planter name.
- Shelf shrooms, bounce shrooms, and cloud fungus only show a player name when your client can match the spawned object to a single recent throw. Missing or ambiguous records leave the player name blank.

Built against PEAK 3.0.1. Final in-game checks for 0.1.3, including plant labels, chest states, segment boundaries, language switching, and multiplayer display, are still pending.
