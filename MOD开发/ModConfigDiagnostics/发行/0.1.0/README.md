# ModConfig Diagnostics

This is a read-only troubleshooting mod for PEAK mod profiles.

Press `F9` in game to write `BepInEx/ModConfigDiagnostics-latest.txt`.
The report includes:

- loaded ModConfig, PEAKLib UI/Core versions and old/new menu type names;
- loaded plugin metadata and duplicate DLL file names under `BepInEx/plugins`;
- visible BepInEx config section/key identities;
- settings registered in the game's `SettingsHandler`, including duplicate wrapped entries;
- ModConfig's tracked sections;
- active ModConfig UI text, paths, and suspicious tokens such as `log:0`.

The mod does not change configuration values, register settings, patch ModConfig, or alter UI objects.
