# PlayersInfo Files

Last updated: 2026-08-14

## Paths

- Source: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo`
- Project: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo\PlayersInfo.csproj`
- Test output: `C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\PlayersInfo.dll`
- Release directory: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.1.1`

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
- `Helpers\ExtraStaminaValueHelper.cs`: shared petrify-aware extra-stamina cap calculation for displayed current/cap values.
- `Helpers\IconSpriteCache.cs`: inventory icon sprite cache.
- `MonoBehaviours\TeammateBarsCoordinator.cs`: teammate HUD coordinator.
- `MonoBehaviours\TeammateBarDriver.cs`: per-teammate stamina and status driver.
- `MonoBehaviours\TeammateInventoryRow.cs`: teammate inventory row.
- `MonoBehaviours\TeammateBarsCoordinator.cs`: teammate HUD coordinator, including stable/distance ordering.
- `Patches\GUIManagerReadyPatch.cs`: GUI readiness guard.
- `Patches\LocalStaminaBarPatch.cs`: local stamina HUD patch.
