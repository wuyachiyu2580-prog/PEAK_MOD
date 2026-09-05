# PlayersInfo Files

Last updated: 2026-09-06

## Paths

- Source: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo`
- Project: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo\PlayersInfo.csproj`
- Direct build output: `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\PlayersInfo.dll`
- Current release directory: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.2.3`

The project `<OutputPath>` now points directly to the 2.0.a profile above. Future PlayersInfo builds should write the DLL there; do not redirect it back to the workspace test environment.

## Current source 0.2.3

- Version chain currently verified: `.csproj <Version> = 0.2.3`, `PlayersInfoPlugin.PluginVersion = 0.2.3`, `AssemblyVersion/FileVersion = 0.2.3.0`.
- The current profile DLL at the path above reports `0.2.3.0`, size `98304` bytes, and SHA-256 `4DED67C58AC5F3AF6D56B172340E9F9006D298DC481F255141A8BD76EBC9C60F`; the project is configured to overwrite this file directly on future builds.
- Current implementation includes stableId-bound teammate bars, unified observed/local display-character resolution, an independent `TeammateBarAffliction`, local hunger countdown, teammate item durability bars, cooked-food icon coloring, dynamic TMP-width placement, native PEAK ownership of the local extra-stamina bar layout, three-mode affliction icon visibility, safe dead/downed distance positions, zero-stamina countdown centering, and the unified `0.25s` low-frequency refresh cadence.
- The old `Display.EnableInventoryRow` key is retained as a compatibility key but is now a three-level enum: `Disabled`, `ContentsOnly`, and `ContentsAndJetpackFuel`. Legacy `true` maps to `ContentsOnly`; legacy `false` maps to `Disabled`.
- Backpack contents use the actual backpack capacity: fanny packs show 2 slots, normal backpacks show 4, and backpacks without contents show no inner slots. Jetpack fuel is shown only in the third mode.

## Release 0.2.3

- Release directory: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.2.3`.
- Contents: `PlayersInfo.dll`, `README.md`, `CHANGELOG.md`, `manifest.json`, `icon.png`, and `wuyachiyu-PlayersInfo-0.2.3.zip`.
- Release DLL version is `0.2.3.0`, size `98304` bytes, SHA-256 `4DED67C58AC5F3AF6D56B172340E9F9006D298DC481F255141A8BD76EBC9C60F`; it matches the deployed profile DLL.
- README and changelog cover the three icon modes and localization, dead/downed range fixes, zero-stamina hunger countdown placement, unified refresh timing, and debug-log gating in addition to the retained 0.2.1 features.

## Release 0.2.0

- Release directory: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.2.0`.
- Version chain: `.csproj <Version> = 0.2.0`, `PlayersInfoPlugin.PluginVersion = 0.2.0`, `AssemblyInfo = 0.2.0.0`, `manifest.version_number = 0.2.0`.
- Loose release files: `README.md`, `CHANGELOG.md`, `manifest.json`, `PlayersInfo.dll`, and `icon.png`.
- The loose release files are current. An existing `wuyachiyu-PlayersInfo-0.2.0.zip` contains the pre-fix 66048-byte DLL and was intentionally not rebuilt; do not upload that zip as the repaired build.
- `PlayersInfo.dll` is the Release build copied from `测试环境/BepInEx/plugins/PlayersInfo.dll`; build result was 0 warnings / 0 errors.
- Default `Display.Anchor` is `BottomLeft`; old persisted `TopLeft` values migrate on startup, while the other anchor values are preserved.

## Release 0.1.1

- Version chain: `.csproj <Version> = 0.1.1`, `PlayersInfoPlugin.PluginVersion = 0.1.1`, `AssemblyInfo = 0.1.1.0`, `manifest.version_number = 0.1.1`.
- Loose release files currently present: `README.md`, `CHANGELOG.md`, `manifest.json`, `PlayersInfo.dll`, `icon.png`, `wuyachiyu-PlayersInfo-0.1.1.zip`.
- `PlayersInfo.dll` in the release directory is the 2026-05-24 64000-byte build containing the teammate temporary stamina value clipping fix.
- `README.md` and `CHANGELOG.md` were synced on 2026-05-30 to mention that clipping fix.
- `wuyachiyu-PlayersInfo-0.1.1.zip` exists as of 2026-06-04, size `158802`, timestamp `2026/6/4 19:58:03`.
- Zip contents verified on 2026-06-04: `CHANGELOG.md`, `README.md`, `manifest.json`, `PlayersInfo.dll`, `icon.png`.
- README/CHANGELOG inside the zip include the teammate temporary stamina clipping fix, and the release DLL hash matches the test-environment DLL hash.
- Expected zip content: `README.md`, `CHANGELOG.md`, `manifest.json`, `PlayersInfo.dll`, `icon.png`.
- Current icon file is `icon.png`; do not regenerate it without explicit user confirmation.

## Build

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo\PlayersInfo.csproj" -c Release
```

## Key Files

- `PlayersInfoPlugin.cs`: plugin entry, config, Harmony lifecycle.
- `Helpers\PluginLogger.cs`: unified logging and debug logging gate.
- `Helpers\ModConfigLocalization.cs`: optional PEAKLib.ModConfig display-name/section/description localization for Chinese language; must stay optional and not hard-depend on ModConfig.
- `Helpers\TeamRosterTracker.cs`: teammate tracking and ordering.
- `Helpers\TmpOutlineHelper.cs`: centralized TMP outline styling.
- `Helpers\FontHelper.cs`: shared CJK-capable TMP_FontAsset accessor with 4-tier fallback.
- `Helpers\AfflictionValueHelper.cs`: shared PEAK 2.0.a-aware normal affliction/petrify value reader.
- `Helpers\ExtraStaminaValueHelper.cs`: petrify-aware extra-stamina cap calculation used by teammate `current/cap` side values; the local extra bar now shows current only.
- `Helpers\DisplayCharacterHelper.cs`: resolves `observedCharacter` first and falls back to `localCharacter`.
- `Helpers\AfflictionTimeHelper.cs`: local hunger countdown and affliction timing helpers.
- `Helpers\IconSpriteCache.cs`: inventory icon sprite cache.
- `MonoBehaviours\TeammateBarsCoordinator.cs`: teammate HUD coordinator.
- `MonoBehaviours\TeammateBarDriver.cs`: per-teammate stamina and status driver.
- `MonoBehaviours\TeammateInventoryRow.cs`: teammate inventory row.
- `MonoBehaviours\TeammateBarAffliction.cs`: PlayersInfo-owned affliction renderer for cloned teammate bars.
- `MonoBehaviours\TeammateBarsCoordinator.cs`: teammate HUD coordinator, including stable/distance ordering and cloned teammate extra-bar suppression; it must not reparent the local `ExtraStaminaBar`.
- `Patches\GUIManagerReadyPatch.cs`: GUI readiness guard.
- `Patches\LocalStaminaBarPatch.cs`: local stamina HUD patch; resolves observed/local target and places current-only extra-stamina text inside the native `extraBarStamina` fill without taking over PEAK's sizing.
