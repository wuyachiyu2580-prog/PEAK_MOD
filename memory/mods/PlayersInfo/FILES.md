# PlayersInfo Files

Last updated: 2026-05-10

## Release 0.1.0

- Release directory: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.1.0`
- Release files currently present: `README.md`, `CHANGELOG.md`, `manifest.json`, `PlayersInfo.dll`, `icon.png`, `wuyachiyu-PlayersInfo-0.1.0.zip`.
- Current icon file is `icon.png`; do not regenerate it without explicit user confirmation.
- Version chain: `.csproj <Version> = 0.1.0`, `PlayersInfoPlugin.PluginVersion = 0.1.0`, `AssemblyInfo = 0.1.0.0`, `manifest.version_number = 0.1.0`.
- Zip content: `README.md`, `CHANGELOG.md`, `manifest.json`, `PlayersInfo.dll`, `icon.png`.

## Paths

- Source: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo`
- Project: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo\PlayersInfo.csproj`
- Output: `C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\PlayersInfo.dll`
- Version: `0.1.0`
- Release: `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\发行\0.1.0`

## Build

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PlayersInfo\PlayersInfo\PlayersInfo.csproj" -c Release
```

## Key Files

- `PlayersInfoPlugin.cs`: plugin entry, config, Harmony lifecycle.
- `Helpers\PluginLogger.cs`: unified logging and debug logging gate.
- `Helpers\TeamRosterTracker.cs`: teammate tracking and ordering.
- `Helpers\TmpOutlineHelper.cs`: centralized TMP outline styling.
- `Helpers\FontHelper.cs`: shared CJK-capable TMP_FontAsset accessor with 4-tier fallback.
- `Helpers\IconSpriteCache.cs`: inventory icon sprite cache.
- `MonoBehaviours\TeammateBarsCoordinator.cs`: teammate HUD coordinator.
- `MonoBehaviours\TeammateBarDriver.cs`: per-teammate stamina and status driver.
- `MonoBehaviours\TeammateInventoryRow.cs`: teammate inventory row.
- `Patches\GUIManagerReadyPatch.cs`: GUI readiness guard.
- `Patches\LocalStaminaBarPatch.cs`: local stamina HUD patch.
