# PlayersInfo Decisions

Last updated: 2026-05-10

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

## Diagnostics

- Normal gameplay logs must stay quiet.
- Verbose logs are allowed only behind `Diagnostics.DebugLogging`.
- Important warnings/errors should still use `Warn`, `ThrottleWarn`, `Error`, or `ThrottleError`.

## Do Not Regress

- Do not revert to scattered teammate HUD display.
- Do not bypass `TmpOutlineHelper` for new TMP text.
- Do not duplicate `GetChineseCapableFont`-style logic in other files; always call `FontHelper.GetChineseCapable()`.
- Do not add gameplay behavior to PlayersInfo; keep it display-only.
