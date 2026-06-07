# LanternShootZombiesNight

A PEAK mod that turns the lantern into your core survival anchor.

**Kill zombies** to top up fuel, let a **snuffed lantern slowly refill itself**, or save the **bugle** for a once-every-3-minutes group-wide refill ultimate. Adds configurable cold resistance, dynamic drain speeds, an on-screen HUD, a passive upgrade tree, and full multiplayer config sync.

> **The shipped defaults match the Casual preset.** First-launch `.cfg` gives you a forgiving setup out of the box (with `ActivePreset = Custom` so your manual tweaks stick). Flip `ActivePreset` to `Casual` / `Balanced` / `Hardcore` any time for a one-click rebalance.

Requires **BlackPeakRemix** and **ShootZombies 1.3.3** (or newer).

---

## What's new in 0.2.0

### The warmth-restore loop was completely reworked
Out: rope / rescue / eat / cook / luggage / huddle warmth.
In: **zombie hits**, **campfire**, **bugle ultimate**, and **snuffed-lantern auto-refill**. Each source has a clear identity — combat, shelter, teamwork, downtime.

### Default settings now match the Casual preset
First-launch `.cfg` is identical to the Casual preset values:
- Lit lantern blocks **75% of cold**
- Snuffed lantern auto-refills at **1 fuel-second per real second, anytime** (no daytime/hold gating)
- Upgrade system **on by default** with a cheaper curve (`30,60,90,120,150`) and faster passive accrual (every **30s**)
- 3D fuel indicator on, HUD anchored at **Bottom** in **ExtraLarge** size
- Flashlight mode drain softened to **1.2×**, proximity grace **15s**

Want the old harder values? Switch `ActivePreset` from `Custom` to `Balanced` or `Hardcore`.

### Bugle became an ultimate
- Global 3-minute shared cooldown, not per-bugle
- Blowing it refills every non-faerie lantern on every player within `BugleUltimateRadius` (default 20m)
- Overflow goes into the reserve pool
- Backpack-stored lanterns refill without being force-lit (they stay snuffed by design)

### Host-only item management
- **F8** — spawn missing items: empty lantern if you have none, **one** bugle session-wide (host), backpack if you're empty-handed
- **F7** (host) — destroy every bugle in the scene. Handles PUN ownership via a two-phase coroutine (request ownership → wait 0.8s → destroy)
- **Stray bugle cleanup** — host periodically sweeps bugles left > 100m from everyone for > 60s and destroys them
- **Auto-purge** — if you're carrying two non-faerie lanterns, the lowest-fuel one is destroyed after a 0.5s grace

### Warmth survives a snuffed lantern
- Zombie-kill and campfire refuel no longer care whether your lantern is lit — warmth flows straight into the lantern body (held, tempSlot, or backpack)
- Only overflow (hit restore past full) drops into the **reserve pool**, which then gets spent first on the next burn
- If you have zero lanterns, zombie-hit warmth still banks into the reserve as a last-resort fallback

### ShootZombies 1.3.3 compatibility
- Upstream fixed cook-color tinting themselves, so our defensive overlays were removed
- `ShootZombiesPerformanceFix` slimmed down to only the `ZombieDeathPatch` reflection-elimination Postfix (the part that still wins measurable perf)

> ⚠️ **Breaking change**: many config keys were renamed or removed. Delete your old `.cfg` before the first launch.

See `CHANGELOG.md` for the full list.

---

## Features at a glance

| System | What it does |
|---|---|
| **Fuel capacity** | 30 / 60 / 90 / 120 / 240 seconds, or infinite |
| **Campfire** | Light = full refill; standing near = drain paused |
| **Reserve pool** | Overflow from Hit / Bugle is banked and spent first on next burn |
| **AutoRefill** | Snuffed lantern slowly refills up to a cap (default 50%) |
| **Cold resistance** | 0.0 – 1.0 multiplier when holding a lit lantern (default 0.75 = 75% block) |
| **Dynamic drain** | Flashlight × 1.2, solo penalty × 1.2 (after 15s grace), teammate discount × 0.8 |
| **Bugle ultimate** | Group-wide refill, 3-min global CD |
| **Upgrades** | Passive +1 / 30s, zombie hit +1, campfire +5, bugle +3 — auto-spend on capacity then efficiency |
| **HUD** | Fuel bar, drain multipliers, last restore source, reserve warmth, day/night, upgrade level |
| **Multiplayer sync** | Host gameplay settings push to clients in ~1s; HUD / hotkeys / mute stay local |

---

## Config layout

The `.cfg` file has 5 sections. **You rarely need to touch it** — use `ActivePreset` for a one-click rebalance.

| Section | Contents |
|---|---|
| `[Lantern]` | Fuel capacity, cold resistance toggle + multiplier, campfire refuel, reserve pool, AutoRefill (cap/rate/daytime/hold), drain multipliers (flashlight/companion/solo), proximity grace, extra-lantern purge |
| `[Restore]` | Master switch, hit warmth seconds, radius, hit cooldown |
| `[Bugle]` | Ultimate enabled/cooldown/radius/restore amount, stray-bugle cleanup distance/grace |
| `[Display]` | `ActivePreset`, HUD on/off, position (8 anchors), size (4 presets), day-night display, 3D fuel indicator, F8 spawn key, F7 host-recall key, mute zombie/tornado |
| `[Upgrade]` | Enable, level-cost CSV, capacity bonus CSV, efficiency bonus CSV, passive tick interval/amount, per-event point values |

### Preset quick-reference

| | Casual *(= shipped defaults)* | Balanced | Hardcore |
|---|---|---|---|
| Fuel capacity | 120s | 120s | 90s |
| Cold block | 75% | 60% | 40% |
| Reserve pool | Half | Half | Quarter |
| Hit restore | 8s / r50 / cd0.3 | 6s / r45 / cd0.4 | 4s / r35 / cd0.5 |
| AutoRefill cap / rate | 0.5 / 1.0 | 0.45 / 0.7 | 0.35 / 0.4 |
| AutoRefill gating | none | require-hold | daytime + require-hold |
| Bugle CD / radius | 180s / 20m | 220s / 18m | 280s / 15m |
| Drain (flash / solo / companion) | 1.2× / 1.2× / 0.8× | 1.4× / 1.3× / 0.85× | 1.7× / 1.4× / 0.9× |
| Proximity grace | 15s | 25s | 40s |
| Upgrade costs | 30,60,90,120,150 | 45,90,140,200,260 | 60,120,190,270,360 |
| Upgrade capacity max | +75% | +90% | +100% |
| Upgrade efficiency max | −50% | −50% | −40% |
| Passive tick | 30s | 45s | 60s |
| Campfire / Bugle points | 5 / 3 | 5 / 3 | 4 / 2 |
| Mute Z/T sounds | off | off | off |

Switching preset overwrites all gameplay keys at once; HUD / hotkey / mute are considered personal preferences and are **not** overridden. `Custom` does nothing automatic — it preserves whatever you've manually edited.

---

## Installation

1. Install `BepInExPack PEAK`
2. Install **BlackPeakRemix**
3. Install **ShootZombies 1.3.3** (or newer)
4. Install `ModConfig` (optional but recommended — gives the in-game config UI proper bilingual labels)
5. **Delete** any old `BepInEx/config/com.wuyachiyu.LanternShootZombiesNight.cfg` — 0.2.0 is a breaking change
6. Drop `Lantern_ShootZombies_Night.dll` into `PEAK/BepInEx/plugins/`

Launch the game once to generate the fresh `.cfg`.

---

## FAQ

**Why is the default so generous?**
Because "survive by managing a lantern" should feel fair on first try. Casual keeps you alive long enough to learn the mechanics; Balanced/Hardcore are there when you want real pressure.

**Where did my old cfg go? Why did I lose all my settings?**
0.2.0 renamed/removed many keys during the rework. Delete the old file and let 0.2.0 regenerate it.

**Why did you remove eat / cook / rescue / rope / huddle warmth?**
They felt incidental — licking a cooked mushroom shouldn't really keep your lantern lit. The new loop is narrower but every source earns its keep: combat (hit), shelter (campfire), teamwork (bugle ult), downtime (AutoRefill).

**Why does the snuffed lantern stop refilling at 50%?**
Anti-AFK. The cap gives you enough to relight for the next night but not a free ride. Bump `AutoRefillCapPercent` if you disagree.

**Bugle ultimate didn't seem to trigger for me?**
Every client checks its own distance to the bugle independently. If you were outside `BugleUltimateRadius` (default 20m) you got skipped. Check the log for `BugleUltimate ... Skipped (too far)`.

**I'm the host, but F8 doesn't give me a bugle?**
The check is strict: any bugle **anywhere** in the session (any player's hand/backpack/tempSlot, or a loose one in the world) blocks the spawn. Wait for the stray-cleanup window (default 60s grace after 100m distance) or have someone discard theirs. Or press **F7** to nuke every bugle and respawn one fresh.

**Cold resistance not working?**
1. Game version must be **1.61.b or later**
2. ShootZombies "Night Cold" is **on**
3. Current ascent is **5 or above**
4. `EnableWarmthReduction` is `true`

---

## Contact

- Author: 乌鸦吃鱼
- QQ Group: 1093172647
