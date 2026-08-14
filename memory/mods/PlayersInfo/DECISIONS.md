# PlayersInfo Decisions

Last updated: 2026-08-14

## Architecture

- Keep PlayersInfo read-only for gameplay state.
- Do not send gameplay RPCs from PlayersInfo.
- Use `TeammateBarsCoordinator` as the single coordinator for teammate HUD entries.
- Keep specialized rendering in dedicated MonoBehaviours, not in the coordinator.
- Treat `0.1.0` as the first public release; do not describe old unreleased experiments in release docs.

## Scope Boundary

- PlayersInfo owns teammate HUD presentation, local config, and diagnostics only.
- Do not hook other mods' private fields or consume undocumented cross-mod state.
- If future cross-mod data is needed, define a clear public data contract first.

## Display Rules

- Local player remains fixed at the top of the HUD ordering.
- Other teammate entries follow `TeamRosterTracker` ordering.
- Dead players should keep a dimmed entry instead of disappearing immediately.
- Affliction timers use countdown format and expire automatically.
- TMP text should go through `TmpOutlineHelper` for consistent readability.
- TMP font must go through `FontHelper.GetChineseCapable()` to avoid CJK tofu blocks. Never leave `tmp.font` unset or assign `TMP_Settings.defaultFontAsset` directly; never return null from font accessors.
- Config sections should stay compact: `Display` for normal user-facing HUD options and `Advanced` for diagnostics/rare controls. Do not re-split simple PlayersInfo options back into `General` / `Features` / `Layout` / `Nearby` / `Diagnostics`.
- `EnableStaminaBar` is a real teammate HUD switch. `Enabled` disables all PlayersInfo HUD features, while `EnableStaminaBar=false` hides only teammate bars and leaves local stamina value overlay governed by `ShowStaminaValue`.
- `Anchor`, `OffsetX`, and `OffsetY` must be wired to actual HUD placement, not just config file entries.
- PEAKLib.ModConfig localization is optional. PlayersInfo may patch ModConfig display names when the plugin exists, but must run normally when ModConfig is absent.
- In PEAK 2.0.a, petrify is a separate `CharacterData.petrifyAmount` value. Any bar marked `BarAffliction.isPetrify` must use that value instead of `CharacterAfflictions.GetCurrentStatus()`.
- In PEAK 2.0.a, `CharacterData.extraStamina` is already clamped by petrify. PlayersInfo must display it as supplied and must not apply the petrify reduction again.
- Teammate bar ordering defaults to `Stable`: distance selects the nearest teammates, while the existing displayed order determines their vertical positions. `Distance` remains available for users who explicitly want distance order.
- PEAK 2.1.a review found no breaking changes in PlayersInfo's referenced HUD, stamina, affliction, character sync, or inventory APIs. Do not add compatibility code until an in-game regression is observed.

## Diagnostics

- Normal gameplay logs must stay quiet.
- Verbose logs are allowed only behind `Advanced.DebugLogging`.
- Important warnings/errors should still use `Warn`, `ThrottleWarn`, `Error`, or `ThrottleError`.

## Do Not Regress

- Do not revert to scattered teammate HUD display.
- Do not bypass `TmpOutlineHelper` for new TMP text.
- Do not duplicate `GetChineseCapableFont`-style logic in other files; always call `FontHelper.GetChineseCapable()`.
- Do not add gameplay behavior to PlayersInfo; keep it display-only.
