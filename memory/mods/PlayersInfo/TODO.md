# PlayersInfo TODO

Last updated: 2026-09-06

## 2026-09-06 0.2.4 verification

- [ ] In a clean PEAK 2.4.b session, switch `AfflictionIconDisplayMode` between `ShowAll`, `HideTeammates`, and `HideAll`; verify skull/cold/hunger and similar icons toggle in real time while status bars, colors, widths, and numbers remain visible.
- [ ] Verify local, teammate, and spectator icon ownership independently. Extra stamina, shield, campfire, and inventory icons must not be hidden by the affliction setting.
- [ ] Test normal, `passedOut`, and `fullyPassedOut` teammates entering/leaving range. Confirm dead teammates never show a bar, out-of-range roster members are not retained by the missing-roster grace, the 5 m hysteresis still works, and `NearbyRange=0` remains unlimited.
- [ ] Test max count and both stable/distance sorting while teammates die, revive, reconnect, or cross the range boundary.
- [ ] Drain local stamina from a wide bar to `<= 0.005`. Confirm the hunger countdown stays after the stamina number while there is room, hides when the green fill is too narrow, and only at true zero appears centered in the available `maxStaminaBar` region after status widths are excluded.
- [ ] Change hunger/cold/injury widths while the zero-stamina countdown is visible; confirm it follows the native available region, does not overlap status values, and hides immediately after stamina recovery.
- [ ] Verify countdown suppression for hidden stamina values, local death, remote spectator targets, no hunger growth, immunity, and airport scenes.
- [ ] Confirm a normal log with `Advanced.DebugLogging=false` has no diagnostic snapshot/binding/layout spam; investigate only PlayersInfo warnings/errors, excluding unrelated `WhySoLaggy` and PEAK save errors.
- [ ] Recheck PEAK 2.4.b multiplayer and spectator edge cases before treating the 0.2.4 profile build as fully verified.

## 2026-08-19 Confirmed fixes

- [x] Teammate main-stamina number is visible on the green `staminaBar` layer instead of the outer `fullBar` node.
- [x] Teammate green stamina fill no longer appears centered at the middle of the full 100% bar.
- [x] Teammate infinite-stamina display freezes the pre-effect main-stamina value and restores live synchronization after the synchronized `InfiniteStamina` affliction ends.

## Historical 0.2.1 follow-up verification

- [ ] With PlayersInfo enabled, use airplane food, granola, cooked food, and another item that grants extra stamina; verify the progress completes, extra stamina is applied, and consumable items disappear normally.
- [ ] After teammate bars are created, verify local hunger/injury/petrify changes still call `GUIManager.bar.ChangeBar()` without exceptions and that all original local affliction visuals remain present.
- [ ] Verify explicitly cloned teammate affliction visuals align with each teammate main bar and continue to follow their fixed stable-ID owner.
- [ ] Verify a clean config uses `Display.OffsetY=0` for every anchor and no startup migration changes it to `138`; confirm persisted custom offsets still work.
- [ ] In PEAK 2.1.a, run a clean game test for local/teammate stamina, petrify, inventory sync, and the stable bar order.
- [ ] In PEAK 2.1.a, verify teammate extra-stamina side text shows `current/cap` without `+`, for example `45/70`, and shows `0/cap` while alive when current extra stamina is empty.
- [ ] In PEAK 2.1.a, verify local petrify percentage follows the in-game petrify bar.
- [ ] In PEAK 2.1.a, verify the local extra-stamina bar stays beside the local stamina bar when extra stamina appears/disappears and when the HUD anchor changes.
- [ ] In PEAK 2.1.a, verify the local extra-stamina bar is below the main bar for all HUD anchors, its outer outline/fill no longer overlap the main bar, and no separate local `current/cap` text appears.
- [ ] Verify teammate main stamina bars/numbers and affliction visuals remain visible while only the teammate extra-stamina graphical bar is absent.
- [ ] After restarting PEAK with the 22:35 build, verify the teammate purple `isPetrify` cap segment no longer overlaps the local main stamina row while the right-side `current/cap` text remains.
- [ ] In PEAK 2.1.a, verify local and teammate numeric overlays disappear cleanly on death and reappear without overlap after revival.
- [ ] In PEAK 2.1.a, verify teammate petrify reduces the denominator in `current/cap` without creating a teammate petrify graphical bar; ordinary teammate affliction bars must still render.
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
