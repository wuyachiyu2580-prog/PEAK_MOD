# WhereIsMyAmulet 1.0.1

Shows the distance to dropped amulets, backpacks containing amulets, and amulet fragments held by statues in PEAK.

Press `C` to scan the current scene. Labels use the game's localized amulet names and follow the current game language.

Config file: `BepInEx/config/com.wuyachiyu.WhereIsMyAmulet.cfg`

- `General.Enabled`: enable or disable labels.
- `General.ScanKey`: scan key, default `C`.
- `Display.MaxDistance`: maximum display distance; `0` means unlimited.
- `Display.FontSize`: label font size.
- `Display.LabelFont`: font used by English labels only. Available choices are Auto, GameDefault, TmpDefault, KoreanBinggrae, and Crazk. Chinese text may display as tofu boxes.
![141555](https://bee-reg-ab.imagency.cn/p/e3c94f7dc727e947802d8d67d3314f42.png)
- `Display.ShowStatueFragments`: show fragments held by amulet statues and fragments mounted on Scout Statues.
- `Display.ShowOffscreenDirection`: show direction for offscreen targets.

PEAKLib.ModConfig is optional. When installed, WhereIsMyAmulet provides English and Chinese names and descriptions without rebuilding ModConfig's global registry.
