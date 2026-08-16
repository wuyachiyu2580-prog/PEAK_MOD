# Changelog

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
