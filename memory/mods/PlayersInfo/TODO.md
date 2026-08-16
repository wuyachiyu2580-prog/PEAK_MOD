# PlayersInfo TODO

Last updated: 2026-08-16

## 2026-08-16 follow-up verification

- [ ] In PEAK 2.1.a, run a clean game test for local/teammate stamina, petrify, inventory sync, and the stable bar order.
- [ ] In PEAK 2.1.a, verify teammate extra-stamina side text shows `current/cap` without `+`, for example `45/70`, and shows `0/cap` while alive when current extra stamina is empty.
- [ ] In PEAK 2.1.a, verify local petrify percentage follows the in-game petrify bar.
- [ ] In PEAK 2.1.a, verify the local extra-stamina bar stays beside the local stamina bar when extra stamina appears/disappears and when the HUD anchor changes.
- [ ] In PEAK 2.1.a, verify the local extra-stamina bar is below the main bar for all HUD anchors, its outer outline/fill no longer overlap the main bar, and no separate local `current/cap` text appears.
- [ ] Verify teammate main stamina bars/numbers and affliction visuals remain visible while only the teammate extra-stamina graphical bar is absent.
- [ ] In PEAK 2.1.a, verify local and teammate numeric overlays disappear cleanly on death and reappear without overlap after revival.
- [ ] In PEAK 2.1.a, verify teammate petrify percentage and extra stamina display when petrify reduces the available extra-stamina cap.
- [ ] In PEAK 2.1.a, verify teammate main slots, temporary slot, backpack type, actual backpack capacity (none/two/four), and contents update after inventory changes.
- [ ] In PEAK 2.1.a, verify the three `Display.EnableInventoryRow` modes, including legacy boolean migration and jetpack fuel visibility.
- [ ] In PEAK 2.1.a, verify teammate durability bars for continuous-use and discrete-use items, 0%/partial/100%, `hideFuel`, and icon visibility.
- [ ] In PEAK 2.1.a, verify cooked-food icon colors reset correctly when an item is replaced or a slot is emptied.
- [ ] In PEAK 2.1.a, verify spectator nearby center works for both `LocalCharacter` and `ObservedCharacter`; switching/death/invalid observed targets must hide or safely fall back without local data leaking into the target display.
- [ ] In PEAK 2.1.a, verify default `Display.TeammateSortMode=Stable` keeps bars in place while teammates move, join, leave, die, or reconnect.
- [ ] In PEAK 2.1.a, verify `Display.TeammateSortMode=Distance` preserves the previous nearest-to-farthest behavior.
- [ ] Perform final in-game verification of the 0.2.1 build in PEAK 2.1.a, including performance with inventory rows enabled and disabled.

## Pending

- Verify large parties with more than 4 players: HUD overflow and ordering still need real-session validation.
- Check high-DPI and unusual resolution readability for TMP outlines.
- If future logs still contain PlayersInfo spam with `Advanced.DebugLogging=false`, audit for new direct `PluginLogger.Info` diagnostics.
