# Changelog

## [0.2.3] - 2026-09-06

### Added

- Added `AfflictionIconDisplayMode` with `ShowAll`, `HideTeammates`, and `HideAll` options.
- Added English and Chinese ModConfig labels, descriptions, and enum value names for status icon display.
- Added zero-stamina hunger countdown placement in the available stamina area.

### Fixed

- Fixed dead and downed teammates ignoring distance filtering after death or incapacitation.
- Dead teammates now use their last living position for distance checks instead of the death-space body position.
- Hunger countdown no longer jumps to a fixed position when the green stamina bar becomes too small or reaches zero.
- Unified low-frequency teammate and local HUD data refresh timing.
- Diagnostic snapshots and binding details now respect `Advanced.DebugLogging`; the default configuration no longer emits them.

### Compatibility

- Updated compatibility target to PEAK 2.4.b.
- PlayersInfo remains display-only and does not modify gameplay state or send gameplay RPCs.


## [0.2.2] - 2026-08-18

### Added

- Added `ShowExtraStaminaCap`, a teammate extra-stamina display option. On shows `XX/XX`; off shows `XX` only, hiding the extra-stamina cap.
- Added English and Chinese ModConfig labels/descriptions for the new option.

### Fixed

- Fixed teammate stamina numbers moving to the middle of the full stamina bar instead of staying with the green fill.
- Fixed teammate stamina bars overflowing while Infinite Stamina is active by freezing the displayed stamina until the synced Infinite Stamina affliction ends.

### Compatibility

- PlayersInfo remains display-only and does not modify gameplay state.


## [0.2.1] - 2026-08-17

### Added

- Added a configurable spectator nearby center: local character or observed character.
- Added a local hunger countdown with automatic text placement.
- Added teammate item durability bars and cooked-food color feedback.
- Added backpack-aware contents: no inner slots, two-slot fanny packs, and four-slot normal backpacks.
- Added an optional teammate jetpack fuel bar.

### Changed

- Teammate rows are now permanently bound to stable player IDs, preventing bars from swapping or jumping.
- Spectator stamina, extra stamina, status, and nearby-player data now use one consistent observed-player target.
- `EnableInventoryRow` is now a three-mode setting: `Disabled`, `ContentsOnly`, or `ContentsAndJetpackFuel`. Old boolean values migrate automatically.
- Teammate extra stamina now uses right-side `current/cap` text without `+`. The graphical extra bar and petrify cap segment are no longer cloned.
- The local extra-stamina bar uses PEAK's native layout, animation, outline, icon, and petrify presentation.
- UI text uses its measured width to reduce overlap, and unchanged UI values are updated less often.

### Fixed

- Fixed teammate bars showing another player's graphical state while keeping the correct numeric value.
- Fixed local values appearing while spectating another player and fixed nearby-player selection using the local corpse position.
- Fixed teammate extra-stamina and petrify visuals overlapping the local stamina HUD.
- Fixed status clones referencing and destroying local PEAK HUD components. This could stop extra-stamina consumables from applying their effect or being consumed.
- Fixed stale or overlapping values during death, revive, reconnect, and spectator transitions.
- Fixed small, unreadable durability percentages by replacing them with progress bars.

### Compatibility

- Updated for PEAK 2.1.a inventory, backpack, stamina, petrify, and spectator behavior.
- PlayersInfo remains display-only and does not modify gameplay state.


## [0.2.0] - 2026-08-14

### Changed
- Changed the default teammate HUD anchor from the top-left corner to the bottom-left corner.
- Existing 0.1.1 and earlier configurations that still use the old `TopLeft` value are migrated to `BottomLeft` on startup. The other anchor values are preserved.
- Extra stamina now shows the current value and the petrify-aware cap, such as `+45/70`.
- Added stable teammate bar ordering. Distance-based ordering remains available through configuration.

### Bugfixes
- Fixed the local extra-stamina bar being treated as a separate item by the teammate vertical layout. It now stays attached to the local stamina bar on the HUD-safe side instead of jumping to the bottom of the panel.
- Fixed teammate and local numeric stamina/status text remaining visible or overlapping during the death transition. Numeric overlays are now cleared while a character is dead.

### Compatibility
- Updated compatibility for PEAK 2.1.a, including petrify display and the newer backpack slot API.

### Notes
- PlayersInfo remains display-only and does not change gameplay or networking state.


## [0.1.1] - 2026-05-29

### Bugfixes
- Fixed teammate temporary stamina values being clipped when the teammate HUD was anchored near the left edge of the screen. The extra-stamina number now picks the safe side of the bar from the configured HUD anchor, and the text rect is wider to avoid hiding the `+` or higher digits.
- Fixed inactive configuration options. `EnableStaminaBar` now hides teammate bars, and `Anchor` / `OffsetX` / `OffsetY` now reposition the HUD. Config values from the old `General` / `Features` / `Layout` / `Nearby` / `Diagnostics` sections are migrated into the merged `Display` / `Advanced` layout.
- Added optional Chinese ModConfig localization for PlayersInfo section names, option names, enum values, and descriptions when PEAKLib.ModConfig is installed. The plugin remains fully usable without ModConfig.
- Hardened the teammate HUD against transient network jitter. The plugin now keeps a `viewID → actorNumber` cache so the same player keeps a stable identity even when Photon briefly reports `Owner == null`, and individual bars are retained for up to 1.5 s when a teammate momentarily disappears from the team list (Photon reconnect, teleport, scene transition). This prevents bars from blanking out and visibly "jumping" for one or two frames during network hiccups.
- Fixed teammate stamina bars all flickering / fully rebuilding whenever any config entry changed (e.g. dragging the OffsetX slider in BepInEx ConfigurationManager). The plugin now only rebuilds the HUD when truly structural toggles change (`Enabled`, `ShowStaminaValue`, `EnableInventoryRow`); runtime numeric values are read live each frame.
- Fixed individual teammate bars flickering when a teammate's distance hovered around the `NearbyRange` boundary. Already-shown teammates now use a 5 m hysteresis margin (`range + 5`), so small distance jitter at the edge no longer kicks bars in and out of the visible set.

### Performance
- Cached numeric stamina text values per teammate so the HUD only updates the TextMeshPro string when the displayed integer actually changes. Reduces per-frame string allocations and TMP mesh rebuilds.
- Throttled the reflective `CharacterData.isInvincible` lookup used to drive the shield icon to once per 0.25 seconds instead of every frame, eliminating the per-frame boxing allocation.
- `IconSpriteCache.Clear` now destroys cached `Sprite` instances before clearing the dictionary, and newly created sprites are flagged with `HideFlags.DontSave`. Prevents leftover sprite assets from accumulating across scene loads.

### Compatibility
- Built against PEAK 1.62.a. No gameplay or networking behavior changed; PlayersInfo remains display-only.

## [0.1.0] - 2026-05-10

### Initial release

- Added a teammate HUD that shows nearby players in a compact list.
- Added teammate stamina bars, including temporary extra stamina.
- Added teammate inventory rows for the main slots, temp slot, backpack slot, and backpack contents.
- Added local stamina value overlay.
- Added distance-based filtering and maximum teammate count options.
- Added a debug logging switch so normal gameplay logs stay quiet.

### Compatibility notes
- PlayersInfo is display-only. It does not send gameplay RPCs or change teammate state.