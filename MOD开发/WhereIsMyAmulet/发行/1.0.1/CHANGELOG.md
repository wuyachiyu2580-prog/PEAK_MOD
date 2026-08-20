# Changelog

## 1.0.1

- Added labels for active medallions on Scout Statues.
- Replaced the generic `Amulet` label with the game's localized amulet names.
- Added English label font choices: Auto, GameDefault, TmpDefault, KoreanBinggrae, and Crazk.
- These font choices are English-only; Chinese text may display as tofu boxes.
- Added optional bilingual PEAKLib.ModConfig localization.
- Added `Display.ShowStatueFragments` to control statue fragment labels.
- Kept the existing configuration keys and profile output path compatible.

## 1.0.0

- Added hotkey-triggered scanning, default key `C`.
- Only GameObject names containing `amulet` are matched.
- Shows distance and offscreen direction for dropped amulet objects.
- Shows backpacks containing an `amulet`, including backpacks worn by players.
- Player-worn backpacks are no longer displayed; only dropped backpacks are tracked.
