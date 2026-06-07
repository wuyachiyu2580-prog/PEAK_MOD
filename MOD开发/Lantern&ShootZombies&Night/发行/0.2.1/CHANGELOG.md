# Changelog

## [0.2.1] - 2026-05-03

### Bugfixes & observability

#### Warmth now fills the lantern body first, even when it's snuffed
Zombie-kill and campfire refuel no longer care whether your lantern is currently lit — warmth flows straight into the lantern (held / tempSlot / backpack), RPC-synced to other clients.
- If the lantern is already full, zombie-hit overflow spills into the reserve pool (campfire is a "refill to max" action so no overflow by design).
- **Why this matters**: the reserve pool caps at a fraction of lantern max (`Half` / `Quarter`), so dumping everything straight into it used to waste warmth. Filling the lantern first preserves the full value.
- Only truly lantern-less players fall back to reserve-pool banking (hit only) so nothing is silently dropped.

#### F8 bugle spawn now refuses to spawn into `tempFullSlot`
PEAK's temp slot (4th slot, created when all 3 regular slots are full) has a game-side bug: items there can lose their `holderCharacter` after death / scene switch / knockback. When that happens `StrayBugleCleaner` misreads the bugle as abandoned and destroys it, leaving an orphan reference in the temp slot — you visually still see the bugle but can't pull it out.
- New pre-check: if the host has no empty regular slot (0/1/2), F8 simply logs `[F8] Bugle spawn skipped: no free regular slot ...` and does nothing.
- Empty a regular slot first, then press F8.
- Lantern and backpack spawning are unaffected.

#### New: `[FOG_DEBUG]` read-only observers
Added a set of Harmony postfix/prefix patches that only write to the log (no behavior change):
- `Fog.Start / OnDisable / Update / MakePlayerCold` — tracks the rising-fog system, edge-triggered on enter/leave plus 1s snapshots
- `OrbFogHandler.RPC_InitFog / RPCA_SyncFog` — tracks the orb-fog system
- Throttled so even a long session doesn't spam the log. Grep `[FOG_DEBUG]` to see the timeline.
- Useful when fog-related mods (e.g. `Thanks.Fog&ColdControl`) suppress cold effects unexpectedly.

---

## [0.2.0] - 2026-05-01

### Final tuning before release

#### Default config is now "Casual" — friendly out of the box
First-time install no longer drops you into the old hardcore tuning. The default `.cfg` is now identical to the **Casual** preset:
- Lit lantern blocks **75%** cold (`EnableWarmthReduction=true`, `LanternWarmthMultiplier=0.75`)
- AutoRefill runs **1.0 fuel-second per real second**, **anytime** (no daytime/hold gating)
- Upgrade system **on by default**, cheaper curve `30,60,90,120,150` and faster passive accrual (`PassiveTickInterval=30s`)
- 3D fuel indicator on, HUD defaults to **Bottom + ExtraLarge**
- Flashlight drain dialed back to `1.2x`, proximity grace shortened to `15s`

If you want the old tuning back, switch `ActivePreset` to `Balanced` (mid-range) or `Hardcore`.

The three-way alignment (cfg defaults / `Config.Bind` defaults / `PresetManager.Casual`) means manual edits and preset switches stay consistent — no more silent drift between "what gets written on first launch" and "what Casual gives you".

#### ShootZombies 1.3.3 compatibility — leaner patch surface
Upstream `Thanks-ShootZombies` shipped 1.3.3 with its own fixes for the cook-color tinting issue. Our defensive overlays were no longer needed and have been removed:
- `InventoryItemUiCookColorRestorePatch` — deleted
- `BackpackWheelCookColorRestorePatch` — deleted
- `ShootZombiesPerformanceFix` slimmed down from 369 lines → 163 lines, keeping only the `ZombieDeathPatch` reflection-elimination Postfix (the part that still gives a measurable perf win)

No functional regression: cook-color now displays correctly in inventory and backpack wheel via SZ's own gating.

### Late additions (post-rework polish)

#### Warmth now survives when the lantern is off (revised)
- Zombie-kill and campfire refuel now work regardless of whether your lantern is currently lit — the warmth flows straight into the lantern body (held, tempSlot, or backpack), RPC-synced to other clients
- Overflow behavior: if the lantern is already full, zombie-hit overflow spills into the reserve pool (campfire is a "refill to max" action, so no overflow by design)
- Why this matters: the reserve pool caps at a fraction of lantern max (`Half` / `Quarter`), so dumping everything straight into it used to waste warmth. Filling the lantern first preserves the full value
- Only truly lantern-less players fall back to reserve-pool banking (hit only) so nothing is silently dropped

#### Host: bugle recall hotkey (`BugleRecallKey`, default F7)
- Host-only shortcut that destroys **every** bugle in the scene
- Handles PUN ownership correctly: two-phase coroutine — phase 1 destroys bugles you own + requests ownership on the rest, waits ~0.8s for the RPC to propagate, phase 2 destroys the newly-owned ones
- Pairs with F8 (host bugle spawn): recall all → spawn a fresh one

#### Bugfix: `StrayBugleCleaner` couldn't actually destroy stray bugles
- PUN requires `Destroy` to come from the current owner; stray bugles are usually owned by the last player who picked them up, not the host
- Previous version just logged a warning and gave up
- Now uses two-scan flow: first scan requests ownership (skips destroy), next 5s-scan owns it and destroys cleanly

#### Bugfix: `BugleRecaller` had the same async-ownership issue
- Rewrote as a coroutine that waits for the TransferOwnership RPC to land before destroying (see above)

#### Bugfix: bugle ultimate could force-light lanterns inside backpacks
- Backpack lanterns are supposed to stay snuffed by game design
- `BugleUltimatePatch` now refills fuel as before but skips the `LightLanternRPC` call for backpack slots — logs `Skipped re-lit (in backpack)` for verification

#### Cleanup
- Removed the dead `UpgradeMenuKey` (F9) config — redundant since upgrades became automatic
- `EnableUpgradeSystem` moved from `Display` section to `Upgrade` section (it never belonged in Display)
- Added CN/EN localization for the `Upgrade` section (9 entries) and `BugleRecallKey`

### 0.2.0 original rework below

---

**Major rework of the warmth-restore system.**
 Old restore sources that felt out of place (rope / rescue / eat / cook / luggage / huddle) are gone. The new loop is: kill zombies for quick top-ups, snuffed lanterns auto-refuel slowly, and the bugle becomes a long-cooldown ultimate.

> ⚠️ **Breaking change**: many config keys were renamed or removed. Old `.cfg` files are not migrated — delete `BepInEx/config/com.wuyachiyu.LanternShootZombiesNight.cfg` before the first launch.

### Removed restore sources
- `InteractionRestorePatch` now only handles the bugle — the 5 sub-patches (Luggage / Action_ReduceUses / Item.Consume / CharacterItems.DropItemRpc / Rope.Detach_Rpc) are deleted
- `CookingRestorePatch` / `RescueRestorePatch` / `HuddleWarmth` deleted outright
- Their config keys (`EatRestoreWarmth`, `InteractRestoreWarmth`, `DropRestoreWarmth`, `RopeRestoreWarmth`, `HuddleRestoreWarmth`, `DiversityCooldown`, etc.) are gone

### New: AutoRefill (snuffed-lantern slow regeneration)
- When fuel hits 0 and the lantern goes out, it slowly refills back up to a configurable cap
- Configurable in the new `AutoRefill` section:
  - `AutoRefillEnabled` (default true)
  - `AutoRefillCapPercent` (default 0.5 — stops at 50% of max fuel)
  - `AutoRefillRate` (default 0.5 fuel-seconds per real second)
  - `AutoRefillDaytimeOnly` (default true — only recharges in daytime)
  - `AutoRefillRequireHold` (default true — lantern must be held by a player)
- HUD shows "Refill…" while actively ticking
- The lantern stays snuffed — you have to relight it yourself once fuel is back

### New: Bugle Ultimate (`BugleUltimate` section)
- Blowing the bugle is now a group-wide AoE ability with a long shared cooldown
- All non-faerie lanterns on everyone within radius are refilled to max; overflow goes into the reserve pool
- Config:
  - `BugleUltimateEnabled` (default true)
  - `BugleUltimateCooldown` (default 180s — global, not per-bugle)
  - `BugleUltimateRadius` (default 20m)
  - `BugleUltimateRestore` (default -1 = refill to max; >0 = fixed seconds)
- Old per-item `Bugle_Enabled/Radius/Cooldown/Restore` config is gone

### F8 hotkey: host-only bugle spawn
- F8 spawns missing items with stricter gating:
  - **Lantern**: you get one only if you have no non-faerie lantern in hands, backpack, or temp slot; spawned empty (fuel=0) so you can't cheese it by burning one and respawning a full one
  - **Bugle**: only the host can spawn, and only when no bugle exists anywhere in the session (checked against every player's inventory + scene objects)
  - **Backpack**: unchanged
- Faerie Lantern is still exempt — mysterious items never block a regular lantern spawn

### New: auto-purge extra lanterns (`PurgeExtraLanterns`)
- Scans the local player every 2s with a 0.5s grace buffer
- If you're carrying 2+ non-faerie lanterns, the lowest-fuel one (tempSlot first) gets destroyed
- Faerie Lantern is exempt

### New: stray-bugle cleanup (host-only, `StrayBugle*`)
- The host periodically sweeps for bugles no one is holding, far from every player (> 100m), and has been so for a while (> 60s)
- These are destroyed via `PhotonNetwork.Destroy` to prevent solo players from "locking" the F8 bugle spawn by tossing one into the void

### Upgrade system: passive accrual + CSV-configurable levels
- Points are now gained passively (1 per 60s by default), plus event bonuses:
  - Zombie hit: +1
  - Campfire light: +5
  - Bugle ultimate trigger: +3
- All level numbers moved to the new `Upgrade` section as CSVs:
  - `LevelCostsCsv` (default "50,100,150,200,250" — Casual default ships as "30,60,90,120,150")
  - `CapacityBonusCsv` (default "0.2,0.4,0.6,0.8,1.0" — Casual default "0.15,0.3,0.45,0.6,0.75")
  - `EfficiencyBonusCsv` (default "0.1,0.2,0.3,0.4,0.5")
  - `PassiveTickInterval` / `PassivePointsPerTick` / `HitPoints` / `CampfirePoints` / `BuglePoints`

### HUD simplified
- Restore source icons reduced to: Kill / Bugle / Fire
- AutoRefill shown as a small "Refill…" status text, not an event icon
- Reserve warmth still shown separately

### Multiplayer sync
- `RoomConfigSync` publishes the new AutoRefill / BugleUltimate / Upgrade / PurgeExtraLanterns / StrayBugle* keys
- Presets (Casual / Balanced / Hardcore) re-tuned around the new loop
