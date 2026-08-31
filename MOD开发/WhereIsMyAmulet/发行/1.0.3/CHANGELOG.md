# Changelog

## 1.0.3

- Fixed incorrect gem name display on statues.
- Trimmed startup log noise while keeping actionable warnings for localization and font fallback.

## 1.0.2

- Restored the label display mode setting: keep labels visible or hide them automatically after a timed scan.
- Changed the default display mode to `Persistent`.
- Added a configurable timed display duration, with a default of 8 seconds.
- Each scan restarts the timer in Timed mode.
- Added bilingual ModConfig names, descriptions, and `Persistent/Timed` enum labels.
- Applied the selected label font to both the amulet name.

## 1.0.1

- Added labels for active medallions on Scout Statues.
- Replaced the generic `Amulet` label with the game's localized amulet names.
- Added English label font choices: Auto, GameDefault, TmpDefault, KoreanBinggrae, and Crazk.
- These font choices are English-only; Chinese text may display as tofu boxes.
- Added optional bilingual PEAKLib.ModConfig localization.
- Added `Display.ShowStatueFragments` to control statue fragment labels.
- Kept the existing configuration keys and profile output path compatible.
