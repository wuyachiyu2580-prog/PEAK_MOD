# WhySoLaggy

Find out which mod is making your game lag, and who is bombing — or sneakily feeding — your room.

1.0.3 focuses on **quiet-by-default**, **structured data** and **targeted diagnostics**: profilers are off until you need them, but a one-line config flip turns the mod into a full performance lab with Harmony-conflict scan, method stack tracer, and a JSON-driven field probe.

---

## Quick start

1. Install **BepInExPack PEAK**.
2. Drop `WhySoLaggy.dll` into `PEAK/BepInEx/plugins/`.
3. Launch the game, play normally, then exit.
4. Open `PEAK/BepInEx/` and look at:
   - `WhySoLaggy.log` — performance data (quiet by default, see Log Verbosity)
   - `WhySoLaggy_Abuse.log` — abuse alerts + RPC reports + feeding-chain traces
   - `whysolaggy_data.csv` — every event in 58 fixed columns
   - `whysolaggy_events.jsonl` — every event as JSON (one per line)
   - `harmony_patches.csv` — every Harmony patch in the process (written once at startup)

Zero extra setup needed. All advanced features are opt-in.

---

## Features

Each feature has a **default state**. Features marked *opt-in* cost zero when disabled.

### 1. FPS Monitor — always on

- Tracks frame time every frame; any frame exceeding `SpikeThresholdMs` (default 50ms) is logged as a spike and written as a `SpikeFrame` event.
- Every `ReportIntervalSeconds` (default 10s) emits an `FpsReport` event with average/p95/p99 frame ms, window average, and (if memory monitor is on) `AllocRateKBps`.
- Overhead: one `Time.unscaledDeltaTime` read per frame. Negligible.

### 2. Plugin Profiler — *opt-in* (default **off** in 1.0.3)

- When `EnablePluginProfiling=true`, Harmony-patches `Update`/`LateUpdate`/`FixedUpdate` on every BepInEx plugin's `MonoBehaviour`s and records their per-frame cost.
- Periodic report lists the slowest plugins by average ms, plus total calls.
- Use `IgnorePluginGuids` (comma-separated) to permanently mute noisy plugins. Example:
  ```
  IgnorePluginGuids = com.example.ui-mod,com.example.minimap
  ```
- Why off by default: instrumenting every mod's `Update` adds measurable self-overhead. Turn on only when diagnosing.

### 3. Patch Profiler — *opt-in* (default **off** in 1.0.3)

- When `EnablePatchProfiling=true`, wraps every Harmony-patched target method with prefix/postfix timing.
- **Throttling**: after a method has been sampled >20 times and its average sits below `MinReportMs` (default 0.1ms), only every 10th call is recorded afterwards. Cuts self-overhead by ~70–90% in busy scenes.
- `IgnorePatchMethods` lets you skip known noisy methods:
  ```
  IgnorePatchMethods = Player.Update,Camera.LateUpdate
  ```
- Null-safe: a null `__originalMethod` is logged as `"(unknown)"` instead of throwing inside Harmony.

### 4. Room-Bombing Detection — always on

- Monitors three per-second rates: Instantiate, Destroy, RPC. Also tracks absolute scene object count for spike detection.
- When any rate exceeds its threshold, an `⚠ ABUSE ALERT` is written to `WhySoLaggy_Abuse.log` together with the offending player's nickname + ActorNumber and the current top-5 RPC method names for context.
- A red on-screen banner shows the alert for 12s (auto-fades). Language follows system locale (中/EN).
- **Observation only** — never kicks, blocks or interferes.
- **New in 1.0.3** — two extra alarms layered on top (see also the Master-side diagnostics section):
  - `ActorMethodHotspot` — the same actor sent the same RPC ≥ `ActorMethodRateThreshold` times in one check window (default 20/s). Pinpoints *which* client is flooding *which* method, not just the total.
  - `OwnershipGrab` — one actor pulled ≥ `OwnershipGrabRateThreshold` PhotonView ownerships in one window (default 10/s). Surfaces silent "I own everything now" griefing patterns.
- **InstantiateTrace** — every `PhotonNetwork.Instantiate` is prefix-hooked; first 3 calls per prefab + one sample per 5s window capture a filtered caller stack (`TraceCaller`, `TraceStack`) so you can see *which* mod or script is producing the spawn. An Instantiate flood automatically forces a stack capture on the next call.

Default thresholds (per second): Instantiate 15, Destroy 20, RPC 50, ObjectSpike +30, ActorMethodHotspot 20, OwnershipGrab 10.

### 5. Full RPC Monitor — always on

Hooks `PhotonNetwork.ExecuteRpc` and tracks **every** RPC.

- **Two buffers per method**:
  - `_windowByMethod` — 1-second rolling count, powers the abuse alert and Top-N list
  - `_watchedByMethod[m].Records` — 32-entry ring buffer per **watched** method, survives across windows, holds full detail records
- **65+ built-in watched methods** covering feeding / healing / status / death / revive / teleport / physics impulse / grab-kick-carry / item pickup-drop-throw / inventory / prefab spawn / end-game / ownership / tornado capture / campfire. They record sender, target GameObject + scene path, parsed arguments, payload bytes.
- **Feeding-chain tracing**: `SendFeedDataRPC → RemoveFeedDataRPC → GetFedItemRPC → Consume` preserved in order, so you can see "who fed whom with which item".
- **Thread-safe**: Photon callback thread only enqueues into a lock-free `ConcurrentQueue`. All dictionary/PhotonView work happens on the main thread, batched at `PumpBatchSize` (default 32/frame).
- **Add custom watched methods** without recompiling:
  ```
  [RpcMonitor]
  ExtraWatchMethods = MyModRPC_Foo,AnotherModRPC_Bar
  ```
- Hot path <500ns per RPC, no allocations.

### 6. Structured Logging (CSV + JSONL) — always on

Every event — `FpsReport`, `PluginTiming`, `PatchTiming`, `SpikeFrame`, `AbuseAlert`, `RpcCall`, `PeriodicReport`, `HarmonyPatchMap`, `MethodTrace`, `FieldProbe`, `InstantiateTrace`, `RemoteRpcTrace`, `OwnershipChange` — is also written to:

- `BepInEx/whysolaggy_data.csv` — 58 fixed columns, Excel/Power-BI friendly
- `BepInEx/whysolaggy_events.jsonl` — one JSON object per line, perfect for `jq` / any AI

Example `whysolaggy_events.jsonl` line:
```json
{"ts":"2026-04-24T15:30:12.451","type":"RpcCall","method":"SendFeedDataRPC","sender":"PlayerB#2","targetPath":"Mushroom Lace(Clone)","payloadBytes":20,"detail":"喂食者=PlayerB#20013, 被喂者=PlayerC#30013"}
```

**Automatic rotation**: when a file exceeds `MaxLogFileSizeMB` (default 10MB) it's renamed with a timestamp suffix (e.g. `whysolaggy_data_20260424_1530.csv`) and a fresh file is created. Long sessions no longer produce ever-growing logs.

Flushes every 10 events + once per periodic report, so a crash loses at most a few seconds.

### 7. Startup Harmony Conflict Scan — always on

Once at startup, the mod snapshots every Harmony-patched method in the process to:

- `BepInEx/harmony_patches.csv` — columns: `TargetMethod, PatchType, OwnerHarmonyId, Priority`
- structured log (`HarmonyPatchMap` events)

If **the same target method is patched by two or more different Harmony IDs**, a warning lands in `WhySoLaggy_Abuse.log` and on the dashboard. This instantly surfaces silent mod conflicts.

Sample `harmony_patches.csv` row:
```
CharacterAfflictions.AddStatus,Prefix,com.wuyachiyu.Lantern,400
CharacterAfflictions.AddStatus,Prefix,com.otherauthor.SomeMod,400   ← CONFLICT
```

Zero runtime cost after the initial scan.

### 8. Method Stack Tracer — *opt-in* (default disabled)

Attach a stack-capture prefix to arbitrary methods without writing code.

```ini
[MethodTracer]
TraceMethodNames = Player.Update,ColdComponent.Apply
TraceMaxDepth = 5
TraceRateLimit = 100
```

For each matched invocation the tracer records timestamp, the top N frames (Unity / HarmonyLib / MonoMod frames filtered out), the immediate caller, and a short argument summary — written as a `MethodTrace` event.

Per-method rate limiter prevents high-frequency methods from flooding the log; the first overflow logs a single warning and the rest are silently dropped. Empty `TraceMethodNames` = hook never installed, zero cost.

### 9. FieldProbe — *opt-in* (default disabled), JSON-driven

The most powerful 1.0.3 addition: **reflectively snapshot any field, parameter or return value at any method**, controlled entirely by a JSON rules file. Useful when you need to prove or disprove a specific hypothesis (e.g. "is this Cold-status change going through AddStatus or SetStatus?") without recompiling.

**Enable:**
```ini
[FieldProbe]
EnableFieldProbe = true
RulesFile = WhySoLaggy.fieldprobe.json   # resolved under BepInEx/config/
DefaultRateLimit = 60
DefaultMaxValueLen = 128
DefaultIncludeStack = false
DefaultStackMaxDepth = 5
```

**Rules file schema** (place the file as `BepInEx/config/WhySoLaggy.fieldprobe.json`):

```json
{
  "_schema": "WhySoLaggy.FieldProbe v1",
  "_doc": "target=Type.Method; fields roots: __instance / __argN / __args / __result / __exception / TypeName; operators .Member ?.Member [N] .Count .Length",

  "enabled": true,
  "rateLimitPerRule": 60,
  "maxValueLen": 128,
  "includeStack": false,
  "stackMaxDepth": 5,

  "rules": [
    {
      "note": "Check which lantern prefab passes the slot filter",
      "target": "LanternHelper.FindLitLanternSlot",
      "fields": [
        "__arg0?.name",
        "__result",
        "__result?.prefab?.name"
      ],
      "includeStack": true,
      "rateLimit": 30
    },
    {
      "note": "Milk-invincibility: is the short-circuit firing?",
      "target": "CharacterAfflictions.AddStatus",
      "fields": [
        "__arg0",
        "__arg1",
        "__instance.character.data.isInvincibleMilk",
        "__result"
      ],
      "rateLimit": 120
    },
    {
      "note": "Which StatusField is applying Cold?",
      "target": "StatusField.Update",
      "fields": [
        "__instance.gameObject.name",
        "__instance.statusType",
        "__instance.statusAmountPerSecond"
      ],
      "rateLimit": 10
    }
  ]
}
```

**Expression DSL:**

| Root | Meaning |
|---|---|
| `__instance` | Harmony `__instance` (the `this`) |
| `__arg0`, `__arg1`, … | Positional parameters |
| `__args` | Full argument array summary |
| `__result` | Return value (postfix) |
| `__exception` | Caught exception (postfix, may be null) |
| `SomeTypeName` | Fully-qualified type name to reach statics |

Operators: `.Member` (field/property), `?.Member` (null-safe), `[N]` (array/list index), `.Count`, `.Length`.

**Per-rule knobs:** `rateLimit`, `maxValueLen`, `includeStack`, `stackMaxDepth`, `enabled`, `note`. All optional — fall back to the global defaults above.

Each invocation emits one `FieldProbe` event containing the target name and every expression's evaluated value (or an error tag). Set `"enabled": false` on a rule to keep it in the file but skip it.

A full working sample (used to diagnose the Faerie-Lantern and Milk-Invincibility bugs during development) ships as `WhySoLaggy.fieldprobe.json` in the mod source; copy it into `BepInEx/config/` and tweak as needed.

Zero overhead when `EnableFieldProbe=false`.

### 10. On-Screen Dashboard — *opt-in* (default off)

Set `[UI] ShowDashboard = true` to draw a draggable `GUILayout` window showing:

- live FPS, frame time, window-average frame ms
- GC allocation rate (KB/s, if memory monitor is on)
- current watched-RPC summary
- most recent abuse alert

When off, the entire IMGUI draw path is skipped — no cost.

### 11. GC Allocation + Ping — always on

- `EnableMemoryMonitor` (default **on**): samples `Profiler.GetTotalAllocatedMemoryLong()` once per second, exposes the rate as `AllocRateKBps` on every `FpsReport`. Tells you at a glance whether a spike is CPU-bound or GC-bound.
- Periodic reports + `AbuseAlert` events also include `PhotonNetwork.GetPing()`, so you can correlate lag with network RTT.

### 12. Log Verbosity — **Minimal by default** in 1.0.3

```ini
[Logging]
LogVerbosity = Minimal   # options: Minimal | Normal | Verbose
```

| Level | `WhySoLaggy.log` | `WhySoLaggy_Abuse.log` |
|---|---|---|
| **Minimal** *(default)* | nothing | abuse alerts only (alerts always land, even in Minimal) |
| Normal | + periodic FPS/RPC reports | + periodic RPC reports, feeding-chain detail |
| Verbose | + every spike line, every patch timing | + every watched RPC detail record |

> The CSV/JSONL structured logs are **never** gated by verbosity — they always capture everything for post-hoc analysis. Verbosity only affects the human-readable text logs.

### 13. Master-side Relay Diagnostics — always on (1.0.3, **beta**)

PEAK runs a Master-relay model: every client RPC goes **client → Master → targets**, so on clients the visible sender is almost always the Master. On a Master install, WhySoLaggy unpacks the Photon event payload itself to recover the *real* origin:

- **`RemoteRpcTrace`** — parses `EventCode=200` `Hashtable` CustomData and extracts `(methodName, viewID, realSenderActor)` for every RPC on a short whitelist of high-risk methods. Emits one structured event per hit.
- **Master-side `SuspectedRequester*`** — when `PhotonNetwork.Instantiate` fires on the Master, the tracer scans the last 500 ms of `RemoteRpcTrace` buffer to find the client-side RPC that most plausibly caused the spawn (by method + viewID proximity). Writes `SuspectedRequesterActor`, `SuspectedRequesterName`, `SuspectedRequesterRpc`, `SuspectedAgeMs` into the `InstantiateTrace` row — this turns "Master spawned the item" into "Client X asked Master to spawn this via RPC Y".
- **`OwnershipChange`** — decodes EventCode 210 (request) / 211 (transfer) / 215 (update). The payload is `int[2]` = `{viewID, otherActor}`. Each event is logged as structured data; `CheckOwnershipGrab` cross-counts per-actor in a window and fires the `OwnershipGrab` alarm described above.
- **`ActorMethodHotspot`** — per-actor per-method RPC counter with its own fast window (`CheckInterval`) and slow window (`ReportInterval`). Top-N Actor×Method rows are added to every periodic report.

> These four diagnostics are the Master-only feature set flagged in the [Testing status](#-testing-status-103) section — they compile cleanly and self-check, but have not yet been exercised in an actual hosted session. Treat their output as beta; on a client install these code paths stay dormant and cost nothing.

---

## Output files

All files live under `PEAK/BepInEx/`.

| File | When written | Contents |
|---|---|---|
| `WhySoLaggy.log` | session | Human-readable perf log (gated by `LogVerbosity`) |
| `WhySoLaggy_Abuse.log` | session | Human-readable abuse + RPC log (gated by `LogVerbosity`) |
| `whysolaggy_data.csv` | session | 58-column event stream, auto-rotated at `MaxLogFileSizeMB` |
| `whysolaggy_events.jsonl` | session | JSON-per-line event stream, auto-rotated |
| `harmony_patches.csv` | startup once | Every Harmony patch in the process |

---

## Configuration (key items)

Open `BepInEx/config/com.wuyachiyu.WhySoLaggy.cfg` after first launch (or use any ModConfig UI).

### `[General]`
| Key | Default | Description |
|---|---|---|
| SpikeThresholdMs | 50 | Frame ms counting as a spike (16–200) |
| ReportIntervalSeconds | 10 | Seconds between `FpsReport` (5–60) |
| EnablePluginProfiling | **false** | Per-plugin Update timing. Opt-in. |
| EnablePatchProfiling | **false** | Per-patch method timing. Opt-in. |
| TopMethodCount | 10 | Top-N for perf reports (3–30) |
| MinReportMs | 0.1 | Patch-profiler low-cost filter |
| IgnorePluginGuids | *(empty)* | CSV of plugin GUIDs to skip |
| IgnorePatchMethods | *(empty)* | CSV of `Type.Method` names to skip |
| EnableMemoryMonitor | true | Sample GC alloc rate |

### `[AbuseDetection]`
| Key | Default |
|---|---|
| EnableAbuseDetection | true |
| CheckIntervalSeconds | 1.0 |
| ReportIntervalSeconds | 30.0 |
| InstantiateRateThreshold | 15 |
| DestroyRateThreshold | 20 |
| RpcRateThreshold | 50 |
| ObjectSpikeThreshold | 30 |
| ActorMethodRateThreshold | 20 |
| OwnershipGrabRateThreshold | 10 |

### `[RpcMonitor]`
| Key | Default | Description |
|---|---|---|
| EnableRpcMonitor | true | |
| TopMethodCount | 10 | Top-N in periodic report |
| WatchedRecordPerMethodCapacity | 32 | Ring buffer size per watched method |
| WatchedShowPerMethod | 6 | Records printed per watched method in report |
| ExtraWatchMethods | *(empty)* | CSV of extra method names to watch |
| PumpBatchSize | 32 | Queue items consumed per frame |

### `[Logging]`
| Key | Default |
|---|---|
| LogVerbosity | **Minimal** |
| MaxLogFileSizeMB | 10 |

### `[UI]`
| Key | Default |
|---|---|
| ShowDashboard | **false** |

### `[MethodTracer]`
| Key | Default |
|---|---|
| TraceMethodNames | *(empty)* |
| TraceMaxDepth | 5 |
| TraceRateLimit | 100 |

### `[FieldProbe]`
| Key | Default |
|---|---|
| EnableFieldProbe | **false** |
| RulesFile | `WhySoLaggy.fieldprobe.json` |
| DefaultRateLimit | 60 |
| DefaultMaxValueLen | 128 |
| DefaultIncludeStack | false |
| DefaultStackMaxDepth | 5 |

> **Upgrading from 1.0.2?** BepInEx only applies new default values when a key is missing. To pick up the quieter defaults, either delete `com.wuyachiyu.WhySoLaggy.cfg` or flip `EnablePluginProfiling`, `EnablePatchProfiling` to `false` and `LogVerbosity` to `Minimal` manually.

---

## How to analyse the logs

By 1.0.3 the recommended workflow is **tag-first**: every event (native WhySoLaggy or any mod cooperating with it, e.g. `LanternShootZombiesNight`) now lands with a bracketed tag like `[LitSync]`, `[WARMTH_LOG]`, `[FuelMath]`, `[RPC_MON]`, `⚠ ABUSE ALERT`. Slice by tag first, only then zoom out.

### Option A — grep / jq by tag (recommended)

Fastest path for single-question diagnostics. One pass, zero tooling beyond the shell.

**Text logs (Windows PowerShell / `rg` / `grep`):**
```powershell
# every abuse alert in this session
Select-String -Path BepInEx\WhySoLaggy_Abuse.log -Pattern '⚠ ABUSE ALERT'

# every lantern lit-state change on the host
Select-String -Path BepInEx\LogOutput.log        -Pattern '\[LitSync\]'

# every failed warmth tick on the client
Select-String -Path BepInEx\LogOutput.log        -Pattern 'WARMTH_LOG.*FAILED'
```

**Structured logs (`whysolaggy_events.jsonl` + `jq`):**
```bash
# top RPC senders
jq -r 'select(.type=="RpcCall") | .sender' whysolaggy_events.jsonl | sort | uniq -c | sort -rn

# all FieldProbe hits for one rule
jq 'select(.type=="FieldProbe" and .target=="StatusField.Update")' whysolaggy_events.jsonl

# frame spikes worse than 100ms
jq 'select(.type=="SpikeFrame" and .frameMs>100)' whysolaggy_events.jsonl
```

### Option B — open the CSV in Excel / Power BI / DuckDB

`whysolaggy_data.csv` has 58 fixed columns. Pivot by `type`, filter by `method` / `sender`, chart `frameMs` over time. Great for multi-session comparison and for sharing one screenshot with teammates.

### Option C — hand a curated slice to an AI

Still useful, but **only after** you've narrowed the file down. Dumping a full multi-MB `.log` into a chat wastes tokens and buries the signal. Typical recipe:

1. `Select-String` / `jq` to extract the 50–500 lines around the incident.
2. Paste that slice plus this prompt:
   > *"Lines below are tagged PEAK session logs (`[LitSync]` = lantern sync, `[WARMTH_LOG]` = warmth tick, `⚠ ABUSE ALERT` = suspected flood). Tell me what went wrong, for which viewId, and in what order."*
3. JSONL paste-through works the same — each line is already a self-describing object.

### Option D — read the plain-text log yourself

- `WhySoLaggy.log`: lines with `!` or `!!!` mark spikes; the `ms` number shows severity.
- `WhySoLaggy_Abuse.log`:
  - `⚠ ABUSE ALERT` — suspect nickname + ActorNumber follow
  - `[RPC_MON]` — periodic RPC stats + watched detail
  - `SendFeedDataRPC` detail: `喂食者=X#123 → 被喂者=Y#456, 物品=Z` means X fed Y with Z
- Any cooperating mod log (e.g. `LogOutput.log` with lantern tags): search by `[TagName]` — see the real-world case section below for what the tags look like in practice.

---

## Sample logs — real data (names redacted)

Real snippets from a 6-player session. Nicknames replaced with `PlayerA`–`PlayerG` (PlayerA = Master). The buffer format has since shifted to per-method rings, but the information shown is the same.

**Startup**
```
[21:47:25.473] [RPC_MON] Hooked PhotonNetwork.ExecuteRpc successfully
[21:47:25.473] [RPC_MON] RpcMonitor initialized (watched methods: 27)   # historical snippet — 1.0.3 ships 65+
```

**Abuse alert with suspect + top RPC methods**
```
[21:47:44.510] ⚠ ABUSE ALERT: RPC flood! Rate: 85.0/s (threshold: 50/s)
[21:47:44.511] [ABUSE]   Top RPC sources (by ActorNumber):
          PlayerA#1: 42x
          PlayerB#2: 10x
          PlayerC#3: 8x
[21:47:44.511] [RPC_MON]   Current-window top RPC methods:
          SyncInventoryRPC: 25x
          SetItemInstanceDataRPC: 9x
          SetCharacterIdle_RPC: 6x
```

**30s periodic report — top methods with payload + top senders**
```
[21:47:55.246] [RPC_MON]   Top 10 RPC methods this period:
          SyncStatusesRPC: 82x  avg=72B max=72B total=5904B  [PlayerB#2:28, PlayerA#1:15, PlayerE#7:15]
          SyncInventoryRPC: 76x  avg=59B max=83B total=4531B  [PlayerA#1:56, PlayerE#7:5, PlayerB#2:5]
          ReceivePluginsFromHostRPC: 9x  avg=1392B max=1392B total=12528B  [PlayerA#1:9]
```

**Watched method detail — sender, target GameObject path, parsed args**
```
[37.6s] PlayerA#1 → RPC_SetThrownData  payload=24B
   target=(PlayerA#187)  path=C_Pawn W(Clone)
   detail=投掷者=PlayerA#10002, 力度=0.00
```

**Feeding-chain trace — "who fed whom with which item"**
```
[668.3s] PlayerC#3 → Consume  payload=20B
   target=(PlayerC#30040)  path=Mushroom Lace(Clone)
   detail=消耗者=PlayerC#30013, 物品=Mushroom Lace(Clone)#30040
[668.3s] PlayerC#3 → RemoveFeedDataRPC  payload=20B
   target=(PlayerC#30040)  path=Mushroom Lace(Clone)
   detail=喂食结束: 喂食者=PlayerC#30013
```

In each trace above the **feeder viewID equals the consumer viewID** — everyone ate their own food, no sneaky cross-player feeding. If someone ever feeds someone else, the two IDs differ and the anomaly stands out immediately.

---

## Real-world case: lantern lit-sync + warmth diagnostics (dual-client)

Pulled from a co-op session between a Host (`IsMasterClient=True`) and a Client (`IsMasterClient=False`) running `LanternShootZombiesNight` on top of WhySoLaggy. The lines below come straight from `BepInEx/LogOutput.log`; `viewId` and `lanternInstID` are Photon/Unity internal IDs (not player-identifying) and are kept verbatim. No nicknames appear in these tags.

These tags are emitted by the lantern mod itself — WhySoLaggy's job here is to make them **structured** (CSV/JSONL, auto-rotated, AI-friendly) and to offer `FieldProbe` as a zero-recompile way to add more of them against any `Type.Method` target.

**Host side — `[LitSync]` proves both sync channels fire**

Channel 1 (`LightLanternRPC`) and Channel 2 (`SetItemInstanceDataRPC → OnInstanceDataSet`) each carry their own tag; the follow-up `lit CHANGED` line confirms the field actually flipped and the VFX GameObject (`lightGO.active`) matches.

```
[LitSync] OnInstanceDataSet: viewId=10007, FlareActive=False, lit=False, isMine=True
[LitSync] LightLanternRPC RECEIVED: viewId=10007, lit=True,  isMine=True, lightGO.active=False
[LitSync] lit CHANGED:           viewId=10007, False→True,   isMine=True, lightGO.active=True
[LitSync] LightLanternRPC RECEIVED: viewId=10007, lit=False, isMine=True, lightGO.active=True
[LitSync] lit CHANGED:           viewId=10007, True→False,   isMine=True, lightGO.active=False
```

Reading the trio: the RPC arrives → `lit` flips → `lightGO.active` flips the same frame. If `OnInstanceDataSet` ever fires with `FlareActive=True` but `lit` stays `False`, that's a sync bug; if `lit=True` but `lightGO.active=False`, the VFX layer is desynced.

**Client side — warmth lifecycle readable at a glance**

```
[HuddleWarmth] STARTED: nearby=1, multiplier=0.5x, warmth=2.5s, interval=7s
[WARMTH_LOG]   source=HuddleWarmth | nearby=1 | warmth=+2.5s | result=FAILED(no lit lantern)
[HuddleWarmth] STOPPED: nearby=0 < min=1
[HuddleWarmth] STARTED: nearby=1, multiplier=0.5x, warmth=2.5s, interval=7s
[FuelMath]     currentFuel=118.484, delta=2.500, rawNew=120.984, maxFuel=120.000, overflow=0.984, lanternInstID=-45672
[WARMTH_LOG]   source=HuddleWarmth | nearby=1 | multiplier=0.5x | warmth=+2.5s | interval=7s | result=SUCCESS
```

Three states in ten lines: first tick fails because the client hadn't lit the lantern yet (`FAILED(no lit lantern)`); the pair then breaks radius and the tracker correctly `STOPPED`; once they regroup and the lantern is lit, `[FuelMath]` quantifies the cap-waste (`overflow=0.984`) and the next `WARMTH_LOG` shows `result=SUCCESS`.

**Client side — alternative warmth source with distance + overflow**

```
[FuelMath]   currentFuel=116.986, delta=5.000, rawNew=121.986, maxFuel=120.000, overflow=1.986, lanternInstID=-46484
[WARMTH_LOG] source=BugleRestore | warmth=+5.0s | dist=9.3m  | result=SUCCESS
[FuelMath]   currentFuel=120.000, delta=5.000, rawNew=125.000, maxFuel=120.000, overflow=5.000, lanternInstID=-47052
[WARMTH_LOG] source=BugleRestore | warmth=+5.0s | dist=10.3m | result=SUCCESS
```

The `source=` + `dist=` + `overflow=` triad makes every warmth event post-hoc auditable: which mechanic fired it, how far the recipient was, and how much fuel (if any) was wasted at the cap. Pair this with WhySoLaggy's `whysolaggy_events.jsonl` and you can replay a full session in `jq`, Excel, or an AI chat without ever re-running the game.

> Want these exact fields without touching source? A `FieldProbe` rule against `LanternHelper.AddPlayerLanternFuel` / `HuddleWarmth.Update` / `StatusField.Update` produces one `FieldProbe` event per call with the same level of detail — shipped through the same CSV/JSONL pipeline.

---

## Installation

1. Install **BepInExPack PEAK** (v5.4.2403+).
2. Drop `WhySoLaggy.dll` into `PEAK/BepInEx/plugins/`.
3. (Optional) For FieldProbe, place your `WhySoLaggy.fieldprobe.json` under `PEAK/BepInEx/config/` and set `EnableFieldProbe=true`.

No other mods required.

---

## Deployment: Master vs Client

WhySoLaggy works on any install, but **who sees what** differs between a host (Master Client) and a regular client. Quick guide:

| Feature | Client install | Master install |
|---|---|---|
| FPS / SpikeFrame / GC alloc rate | ✅ full | ✅ full |
| Plugin / Patch Profiler | ✅ full (local plugins) | ✅ full (local plugins) |
| Local Instantiate / Destroy / RPC rate | ✅ full | ✅ full |
| InstantiateTrace (caller stack) | ✅ full (local spawns) | ✅ full (all spawns pass here) |
| Full RPC Monitor + watched records | ✅ full (sender usually = Master) | ✅ full (sender = real actor) |
| FieldProbe / MethodTracer / Harmony scan | ✅ full | ✅ full |
| Abuse alerts + red on-screen banner | ✅ full | ✅ full |
| On-Screen Dashboard | ✅ full | ✅ full |
| **RemoteRpcTrace** (real client actor) | ⚪️ dormant | 🔍 Master-only, *beta* |
| **Instantiate `SuspectedRequester*`** | ⚪️ dormant | 🔍 Master-only, *beta* |
| **OwnershipChange / OwnershipGrab alarm** | ⚪️ dormant | 🔍 Master-only, *beta* |
| **ActorMethodHotspot alarm** | 🟡 counts but sender = Master | 🔍 full Actor×Method, *beta* |

**TL;DR**
- **Client-only install** — you get the full performance lab, local-spawn forensics and RPC sample buffer; the "real client sender" stays hidden because Master has already rewritten it.
- **Master install** — you additionally get the four relay-decoding diagnostics that pinpoint *which* client sent the RPC, grabbed the ownership or triggered the Instantiate. These are flagged *beta* until a hosted-session capture confirms them.
- **Dual install (both players run the mod)** — ideal for forensics. Correlate the client's `InstantiateTrace.TraceStack` with the host's `RemoteRpcTrace.SenderActor` by viewID and you get a full end-to-end trail.

All Master-only paths are null-safe and wrapped in `try/catch`; on a client install they short-circuit at the `IsMasterClient` check and consume no CPU.

---

## Performance baseline (1.0.3)

Measured on an Intel i5-12400 @ 4.4 GHz, PEAK 6-player session. All numbers are amortised **per second** unless noted.

| Scenario | CPU (ms/s) | GC (KB/s) | Disk (KB/s) | Resident (KB) |
|---|---|---|---|---|
| Normal co-op, default config | 1.0–1.5 | 2–4 | 1–2 | ≈ 450 |
| Item-spam session (15–30 Inst/s) | 2.5–4.0 | 4–8 | 3–6 | ≈ 600 |
| Extreme RPC flood (≥ 300/s) | 6–10 | 8–15 | 10–20 | ≈ 800 |
| With `EnablePluginProfiling=true` | **+3–6** ms/s | +1–2 | — | — |
| With `EnablePatchProfiling=true` | **+2–4** ms/s (throttled) | +1 | — | — |
| With `ShowDashboard=true` | +0.3–0.6 ms/s | negligible | — | — |

**Hot path breakdown (default config, normal session):**

| Component | Share | Notes |
|---|---|---|
| RpcMonitor main-thread Pump | ≈ 40 % | 32 items/frame, lock-free queue |
| StructuredLogger CSV+JSONL writer | ≈ 25 % | flush every 10 events, 10MB rotation |
| NetworkAbuseDetector `OnEvent` | ≈ 20 % | counters + whitelist lookups |
| FPS tracker + memory sampler | ≈ 10 % | one `unscaledDeltaTime` / frame |
| Everything else (dashboard, FieldProbe idle, …) | ≈ 5 % | |

**Opt-in modules that truly cost zero when off:** `PluginProfiler`, `PatchProfiler`, `PerformanceDashboard`, `MethodTracer`, `FieldProbe` — their hooks/IMGUI paths are not installed unless the corresponding config flag is true.

**Throughput ceiling:** the CSV+JSONL writer has been benchmarked at ≈ 8,000 events/s sustained before the flush queue starts lagging. In normal play the observed rate is < 50/s; extreme floods peak around 500/s.

> Keep the default config on for 7×24 captures — the overhead is lower than a single cosmetic mod's `Update` loop. Only flip `EnablePluginProfiling` / `EnablePatchProfiling` on when actively hunting a specific offender.

---

## Notes

- The mod waits ~5s after launch before hooking, so other mods can finish loading first.
- All advanced features (Plugin/Patch Profilers, Dashboard, MethodTracer, FieldProbe) are **off by default** in 1.0.3. Flip them on only when you need them.
- Observation-only: nothing is kicked, blocked or modified in the simulation. Perfect for post-incident evidence.
- Net overhead in default config is *lower* than 1.0.2 thanks to the Minimal verbosity + quiet profilers.

### ⚠ Testing status (1.0.3)

- Every 1.0.3 feature was validated from the **client** side during development. Client-only functionality (FPS monitor, RPC monitor, local Instantiate/Destroy/RPC rates, FieldProbe, MethodTracer, Harmony conflict scan, structured logging) is fully exercised.
- A subset of new diagnostics can only do real work when the install is on the **Master Client**:
  - `OnRemoteRpcEvent` Hashtable unpack — captures the real client Actor behind a Master-relayed RPC
  - Master-side Instantiate requester correlation — ties a `PhotonNetwork.Instantiate` back to the client RPC that most likely triggered it (`SuspectedRequesterActor/Name/Rpc`)
  - PhotonView Ownership audit — EventCode 210 / 211 / 215
  - Actor×Method hotspot detector — per-actor per-method RPC rate alarm
- Those Master-only paths compile cleanly, self-check without exceptions and the code has been reviewed, **but have not yet been exercised in an actual hosted session**. Treat their output as *beta* until a host-side capture confirms them; client-side observations are unaffected.
- If you happen to host a session with this build, please send the resulting `whysolaggy_events.jsonl` (filter `type=RemoteRpcTrace / OwnershipChange / InstantiateTrace`) so the Master-side paths can be validated against real data.

---

## Contact & feature requests

- Author: **乌鸦吃鱼**
- QQ Group: **1093172647**

Spotted an RPC that looks suspicious but isn't on the watched list yet? Drop the method name in the QQ group — if it makes sense I'll add it in the next update. Or add it yourself via `ExtraWatchMethods` without waiting.
