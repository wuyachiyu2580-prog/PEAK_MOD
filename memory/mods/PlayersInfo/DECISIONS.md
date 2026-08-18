# PlayersInfo Decisions

Last updated: 2026-08-17

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
- Starting with 0.2.0, the default `Display.Anchor` is `BottomLeft`. On upgrade, persisted `TopLeft` values migrate, while the other anchor values are preserved.
- Starting with 0.2.1, each teammate bar is permanently associated with the existing `stableId`; distance/range changes only control visibility and sibling order, so one player's bar cannot inherit another player's values or interpolation state.
- Display data must resolve through `observedCharacter` first and `localCharacter` second. This same target is used for stamina, extra stamina, afflictions, countdowns, and the spectator nearby-distance center. The spectator center is configurable as `LocalCharacter` or `ObservedCharacter`, with the latter as the default.
- Every cloned teammate bar owns a `TeammateBarAffliction` component. Vanilla `BarAffliction` is removed from the clone because it can read the global observer target implicitly; PlayersInfo computes the target value, width, and visibility itself.
- Treat every runtime UI reference copied from a partial Unity clone as potentially external. Before disabling or destroying a cloned `BarAffliction`, require `source.transform.IsChildOf(teammateCloneRoot)`; if a vanilla affliction lives outside the cloned `StaminaBar` subtree, clone its GameObject explicitly for the teammate entry. Never mutate or destroy the local HUD's original affliction components.
- Teammate inventory display is an ordered enum: `Disabled` (do not show the row), `ContentsOnly` (show actual backpack contents), and `ContentsAndJetpackFuel` (also show jetpack fuel). The old boolean config key is intentionally reused: `true` migrates to `ContentsOnly`, `false` to `Disabled`, and unrelated config entries are untouched.
- Backpack inner-slot count comes from the backpack visual/type, with explicit support for no contents, two-slot fanny packs, and four-slot normal backpacks. The data model may still contain four serialized item slots; unused capacity must remain hidden.
- Durability is rendered as a bottom horizontal progress bar, with the icon above it; cooked-food icon color follows `ItemCooking.GetCookColor()`. Dynamic text placement uses TMP preferred width to avoid overlapping stamina, names, and countdowns.
- Keep the local `ExtraStaminaBar` in PEAK's original hierarchy. Do not reparent it under `fullBar`, manually force its width, or override the game's extra-bar sequencing; PEAK's `StaminaBar.Update()` owns its indentation, animation, outline, icon, and petrify layout.
- `PI_LocalExtraStaminaValue` belongs inside `extraBarStamina` and displays only the rounded current value, for example `40`. Do not add `+`, `/cap`, or an outer side value to the local extra bar.
- Teammate main stamina bars, main stamina values, and ordinary affliction visuals remain enabled. Do not clone/show the teammate extra-stamina graphical bar or its `isPetrify` cap segment; show extra stamina only as the HUD-safe side value `current/cap` without `+`, including `0/cap` while alive. Petrify remains represented by the reduced cap.
- Remove teammate extra-stamina visuals only when each referenced object is owned by the teammate clone root. Runtime references copied by Unity may still point at the local vanilla HUD; never disable or destroy those external objects. The right-side `current/cap` text is independent and must remain visible.
- The compact teammate jetpack fuel bar in `TeammateInventoryRow` is an independent retained feature. Local extra-stamina layout work must not revert, resize, or otherwise alter that fuel display.
- `Display.OffsetY` defaults to `0` for every anchor. Do not move the whole HUD merely to make room for the local extra bar, and do not migrate zero offsets to a nonzero value. Existing user-selected offsets remain valid.

## Diagnostics

- Normal gameplay logs must stay quiet.
- Verbose logs are allowed only behind `Advanced.DebugLogging`.
- Important warnings/errors should still use `Warn`, `ThrottleWarn`, `Error`, or `ThrottleError`.

## Do Not Regress

- Do not revert to scattered teammate HUD display.
- Do not bypass `TmpOutlineHelper` for new TMP text.
- Do not duplicate `GetChineseCapableFont`-style logic in other files; always call `FontHelper.GetChineseCapable()`.
- Do not add gameplay behavior to PlayersInfo; keep it display-only.
- Do not bind a cloned teammate bar to display position or the global `Character.observedCharacter`; target identity and target data must remain explicit.
- Do not roll the whole project back to `0.2.0` to repair one HUD layout. Preserve the mature `0.2.1` spectator, stable binding, affliction, hunger, durability, inventory, and fuel behavior.
- Do not restore calls to `ConfigureLocalExtraBar()` or `KeepLocalExtraBarVisible()` unless new game evidence proves the native hierarchy cannot work.
- Do not pass `origCompOnClone.afflictions` directly to destructive conversion code. Unity does not remap references outside the instantiated subtree, and damaging the local array breaks `GUIManager.bar.ChangeBar()` and item actions that grant extra stamina.
