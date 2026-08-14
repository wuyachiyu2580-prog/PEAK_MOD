# WhereIsMyAmulet

Shows the distance to dropped amulet objects and backpacks containing amulets in PEAK.

Press `C` to scan the current scene. Objects whose GameObject name contains `amulet` are displayed when they are on the ground. Dropped backpacks containing an item whose prefab GameObject name contains `amulet` are also displayed. Backpacks worn by players are not displayed.

Existing labels continue to update their position and distance. They disappear when the item is picked up, removed, or no longer on the ground.

Config file: `BepInEx/config/com.wuyachiyu.WhereIsMyAmulet.cfg`

- `General.Enabled`: enable or disable labels.
- `General.ScanKey`: scan key, default `C`.
- `Display.MaxDistance`: maximum display distance; `0` means unlimited.
- `Display.FontSize`: label font size.
- `Display.ShowOffscreenDirection`: show direction for offscreen amulet objects and backpacks.
