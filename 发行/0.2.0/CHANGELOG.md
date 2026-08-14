# Changelog

## [0.2.0] - 2026-08-14

### Changed
- Changed the default teammate HUD anchor from TopLeft to BottomLeft.
- Existing configurations from PlayersInfo 0.1.1 and earlier that still use the old default TopLeft anchor are migrated to BottomLeft on first launch. Other explicitly selected anchors are preserved.
- Extra stamina values now show current amount and petrify-aware cap together, for example `+45/70`.
- Added a stable teammate bar order option. `Stable` is the default; `Distance` remains available for distance-based ordering.

### Compatibility
- Updated and built against PEAK 2.1.a.
- Fixed PEAK 2.0.a+ petrify value reading and the changed `BackpackSlot` API used by teammate inventory display.
- PlayersInfo remains display-only and does not change gameplay or networking state.
