# Changelog

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
