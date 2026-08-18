# Changelog

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
