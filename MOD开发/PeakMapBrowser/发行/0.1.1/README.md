# PEAK Map Browser

A BepInEx plugin for browsing peakmap.top community maps inside PEAK.

## Usage

Press `/` on the main keyboard to open or close the map browser.

This is the regular slash key (`Slash`), not the numpad divide key (`KeypadDivide`).

The plugin can browse maps, open map details, download map JSON files, upload local JSON saves, and manage maps owned by the signed-in account.

## Account Security

- The plugin never saves the account password.
- The access token is kept in memory only.
- The refresh token is protected with Windows DPAPI in `session.json` and is bound to the current Windows user.
- Tokens are refreshed automatically before expiry.
- Sign out clears the local session and attempts to call `POST /api/auth/sign-out`.
- The matching remote session revocation endpoint is deployed on peakmap.top and requires a valid access token.

If the local session file is copied to another Windows user account, the DPAPI-protected refresh token cannot normally be decrypted. Treat the original Windows account and its files as trusted, and sign out from the account page if the file may have been exposed.

## JSON Map Requirement

Downloaded and uploaded files are TerrainCustomiser map JSON files.

To load and play these maps, use one of the following mods:

- `TerrainCustomiser`
- `TerrainCustomiserCN`

PEAK Map Browser does not load custom maps by itself.

Downloaded JSON files are saved to:

```text
Application.persistentDataPath/TerrainCustomiser/Map Saves
```

Cover images use a local URL-keyed cache under:

```text
Application.persistentDataPath/PeakMapBrowser/ImageCache
```

When the server returns a new image URL, the new cover is downloaded automatically.

## Config

```text
ApiBaseUrl = https://peakmap.top
Language = auto
PageSize = 12
ToggleKey = Slash
```

`Language = auto` follows the game's language. Set it to `zh` or `en` to force a language.
