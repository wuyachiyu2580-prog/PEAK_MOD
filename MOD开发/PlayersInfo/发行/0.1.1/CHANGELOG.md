# Changelog

## [0.1.1] - 2026-05-17

### Bugfixes
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
