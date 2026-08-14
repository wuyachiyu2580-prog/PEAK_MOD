# PlayersInfo TODO

Last updated: 2026-08-14

## 2026-08-14 follow-up verification

- [ ] In PEAK 2.1.a, run a clean game test for local/teammate stamina, petrify, inventory sync, and the stable bar order.
- [ ] In PEAK 2.1.a, verify extra-stamina text shows current/cap correctly, for example `+45/70` at 30% petrify.
- [ ] In PEAK 2.1.a, verify local petrify percentage follows the in-game petrify bar.
- [ ] In PEAK 2.1.a, verify teammate petrify percentage and extra stamina display when petrify reduces the available extra-stamina cap.
- [ ] Verify teammate main slots, temporary slot, backpack slot state, and four backpack contents update after inventory changes.
- [ ] In PEAK 2.1.a, verify default `Display.TeammateSortMode=Stable` keeps bars in place while teammates move, join, leave, die, or reconnect.
- [ ] In PEAK 2.1.a, verify `Display.TeammateSortMode=Distance` preserves the previous nearest-to-farthest behavior.
- [ ] After in-game verification, decide whether to release these changes as 0.1.2 and rebuild the release package.

## Pending

- Verify large parties with more than 4 players: HUD overflow and ordering still need real-session validation.
- Check high-DPI and unusual resolution readability for TMP outlines.
- If future logs still contain PlayersInfo spam with `Diagnostics.DebugLogging=false`, audit for new direct `PluginLogger.Info` diagnostics.
