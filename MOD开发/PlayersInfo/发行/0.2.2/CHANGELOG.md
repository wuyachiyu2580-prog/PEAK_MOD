# Changelog

## [0.2.2] - 2026-08-18

### Added

- Added `ShowExtraStaminaCap`, a teammate extra-stamina display option. On shows `XX/XX`; off shows `XX` only, hiding the extra-stamina cap.
- Added English and Chinese ModConfig labels/descriptions for the new option.

### Fixed

- Fixed teammate stamina numbers moving to the middle of the full stamina bar instead of staying with the green fill.
- Fixed teammate stamina bars overflowing while Infinite Stamina is active by freezing the displayed stamina until the synced Infinite Stamina affliction ends.

### Compatibility

- PlayersInfo remains display-only and does not modify gameplay state.
