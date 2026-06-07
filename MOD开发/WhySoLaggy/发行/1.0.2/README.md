# WhySoLaggy

Find out which mod is making your game lag, and who is bombing — or sneakily feeding — your room.

## Features

### Performance Profiling — Find the Lag Source

- **FPS Monitor**: Tracks your frame rate in real time, flags any frame that takes too long
- **Plugin Timer**: Measures how much time each installed mod takes per frame — see who's the slowest
- **Patch Analyzer**: Identifies which specific modified game methods are eating up performance

### Room-Bombing Detection — Find Who's Causing Trouble

- Automatically monitors network activity in your room and detects suspicious behavior:
  - Spawning tons of objects in a short time
  - Flooding the network with RPC commands
  - Sudden explosion in the number of scene objects
- When something suspicious is detected:
  - A red alert pops up on the top-left corner of your screen (auto-fades after 12s)
  - The log records the suspect player's name and ID number, plus the top RPC methods at that moment
- **Observation only** — does NOT kick, block, or interfere with anyone. Just records evidence.

### Full RPC Monitor — Know Every Network Command (new in 1.0.2)

- Hooks the unified network entry point and tracks **every single RPC** going through your room
- Every 30s report shows: top methods by call count, top senders per method, average / max / total payload bytes
- **27 high-risk methods** (feeding, healing, status/affliction, death, teleport, physics impulse, item pickup/drop/throw) get full detail records: sender, target GameObject + scene path, parsed arguments, payload size
- **Feeding-chain tracing**: captures `SendFeedDataRPC → RemoveFeedDataRPC → GetFedItemRPC → Consume` so you can tell exactly **who fed whom with which item** — useful for spotting sneaky feeders in co-op
- **Per-method independent buffer**: each watched method has its own 32-entry ring buffer, so a flood from one method can no longer drown out rare but critical events
- **Near-zero overhead**: hot path stays under 500 ns per RPC, no allocations, no locks

---

## Log Files

After your session, open the BepInEx folder in your game directory. You'll find two files:

| File | What it records |
|---|---|
| `BepInEx/WhySoLaggy.log` | Performance data: FPS, mod timing, lag causes |
| `BepInEx/WhySoLaggy_Abuse.log` | Bombing evidence + full RPC monitor reports (top methods, watched details, feeding chains) |

These are recreated every time you launch the game, so they won't grow forever.

---

## How to Analyze the Logs

### Option A: Give it to any AI (Recommended)

Copy-paste the log content into any AI assistant (ChatGPT, Claude, etc.) and ask:

> "This is a performance/abuse log from my game. Please tell me:
> - Which mod is causing the most lag?
> - Were there any suspicious players?
> - Did anyone feed someone else secretly?
> - What happened and when?"

The AI will give you a clear summary.

### Option B: Check it yourself

- **Performance log**: Look for lines with `!` or `!!!` — the number in `ms` shows how slow it is
- **Abuse log**:
  - Lines with `⚠ ABUSE ALERT` — the suspect's player name and ID are shown right after
  - Lines with `[RPC_MON]` — periodic RPC statistics and watched-method details
  - Look for `SendFeedDataRPC` detail records: `喂食者=X#123 → 被喂者=Y#456, 物品=Z` means X fed Y with item Z

---

## Sample Logs — Real Data (names redacted)

The following snippets are **real logs** captured from a 6-player session. Player nicknames have been replaced with `PlayerA`~`PlayerG` (PlayerA = Master). These samples were collected on an in-development DLL whose buffer format is slightly different from the final 1.0.2 layout (single ring buffer vs. per-method buffer), but the information shown is essentially the same.

### 1. Plugin initialization

```
[21:47:25.473] [RPC_MON] Hooked PhotonNetwork.ExecuteRpc successfully
[21:47:25.473] [RPC_MON] RpcMonitor initialized (watched methods: 27)
```

### 2. Abuse alert with suspect attribution + top RPC methods at that moment

```
[21:47:44.510] ⚠ ABUSE ALERT: RPC flood! Rate: 85.0/s (threshold: 50/s)
[21:47:44.511] [ABUSE]   Top RPC sources (by ActorNumber):
          PlayerA#1: 42x
          PlayerB#2: 10x
          PlayerC#3: 8x
          PlayerD#6: 8x
          PlayerE#7: 8x
[21:47:44.511] [RPC_MON]   Current-window top RPC methods:
          SyncInventoryRPC: 25x
          SetItemInstanceDataRPC: 9x
          SetCharacterIdle_RPC: 6x
          SetKinematicRPC: 6x
          SyncThornsRPC_Remote: 5x
```

### 3. 30-second periodic report — top methods with payload and top senders

```
[21:47:55.246] [RPC_MON]   Top 10 RPC methods this period:
          SyncStatusesRPC: 82x  avg=72B max=72B total=5904B  [PlayerB#2:28, PlayerA#1:15, PlayerE#7:15]
          SyncInventoryRPC: 76x  avg=59B max=83B total=4531B  [PlayerA#1:56, PlayerE#7:5, PlayerB#2:5]
          SetItemInstanceDataRPC: 25x  avg=32B max=32B total=800B  [PlayerA#1:20, PlayerB#2:4, PlayerF#8:1]
          EquipSlotRpc: 22x  avg=24B max=24B total=528B  [PlayerB#2:10, PlayerA#1:7, PlayerE#7:3]
          ReceivePluginsFromHostRPC: 9x  avg=1392B max=1392B total=12528B  [PlayerA#1:9]
          RPC_SetThrownData: 7x  avg=24B max=24B total=168B  [PlayerA#1:7]
          DropItemRpc: 7x  avg=78B max=78B total=546B  [PlayerB#2:4, PlayerA#1:3]
```

### 4. Watched method details — sender, target GameObject + scene path, parsed arguments

```
[37.6s] PlayerA#1 → RPC_SetThrownData  payload=24B
   target=(PlayerA#187)  path=C_Pawn W(Clone)
   detail=投掷者=PlayerA#10002, 力度=0.00

[38.5s] PlayerA#1 → DropItemRpc  payload=78B
   target=(PlayerA#10002)  path=Character [PlayerA : 1]
   detail=丢弃物品, target=Character [PlayerA : 1]#10002
```

### 5. Feeding-chain tracing — "who fed whom with which item"

Each self-feed in the log looks like this (`Consume` followed immediately by `RemoveFeedDataRPC` on the same item view):

```
[668.3s] PlayerC#3 → Consume  payload=20B
   target=(PlayerC#30040)  path=Mushroom Lace(Clone)
   detail=消耗者=PlayerC#30013, 物品=Mushroom Lace(Clone)#30040
[668.3s] PlayerC#3 → RemoveFeedDataRPC  payload=20B
   target=(PlayerC#30040)  path=Mushroom Lace(Clone)
   detail=喂食结束: 喂食者=PlayerC#30013

[1238.7s] PlayerD#6 → Consume  payload=20B
   target=(PlayerD#60113)  path=Glizzy(Clone)
   detail=消耗者=PlayerD#60063, 物品=Glizzy(Clone)#60113

[1688.9s] PlayerF#8 → Consume  payload=20B
   target=(PlayerF#80071)  path=Mushroom Lace Poison(Clone)
   detail=消耗者=PlayerF#80040, 物品=Mushroom Lace Poison(Clone)#80071
```

In all entries above the **feeder character viewID equals the consumer viewID** — everyone ate their own food, no sneaky cross-player feeding. If someone ever feeds someone else, the two IDs will differ and it'll stand out immediately.

---

## Configuration

Adjust via in-game ModConfig or BepInEx config file:

### Performance Profiling

| Setting | Default | Description |
|---|---|---|
| SpikeThresholdMs | 50 | How many ms before a frame counts as a lag spike (16–200) |
| ReportIntervalSeconds | 10 | Seconds between performance reports (5–60) |
| EnablePluginProfiling | true | Whether to measure each mod's timing |
| EnablePatchProfiling | true | Whether to measure patched method timing |
| TopMethodCount | 10 | How many slow methods to show in reports (3–30) |

### Room-Bombing Detection

| Setting | Default | Description |
|---|---|---|
| EnableAbuseDetection | true | Turn bombing detection on/off |
| CheckIntervalSeconds | 1.0 | Seconds between checks (0.5–5) |
| ReportIntervalSeconds | 30.0 | Seconds between full reports (10–120) |
| InstantiateRateThreshold | 15 | Objects spawned per second to trigger alert (5–100) |
| DestroyRateThreshold | 20 | Objects destroyed per second to trigger alert (5–100) |
| RpcRateThreshold | 50 | RPC calls per second to trigger alert (10–200) |
| ObjectSpikeThreshold | 30 | Object count increase per check to trigger alert (5–100) |

### RPC Monitor (new in 1.0.2)

| Setting | Default | Description |
|---|---|---|
| EnableRpcMonitor | true | Turn full RPC tracking on/off |
| TopMethodCount | 10 | How many top methods to show in each 30s report (3–30) |
| WatchedRecordPerMethodCapacity | 32 | Per-method ring buffer size — each watched method keeps its own last N records, so high-frequency methods can't drown out rare ones (8–256) |
| WatchedShowPerMethod | 6 | How many most-recent detailed records to print per watched method in each report (1–50) |

## Installation

1. Install **BepInExPack PEAK**
2. Place `WhySoLaggy.dll` into `PEAK/BepInEx/plugins/`

No other mods required.

## Contact & Feature Requests

- Author: wuyachiyu
- QQ Group: **1093172647**

**Spotted an RPC that looks suspicious but isn't on the watched list yet?** Drop a message in the QQ group with the method name and what it does — if it makes sense, I'll add it to the watched list in the next update. The full list of 27 watched methods covers feeding, healing, status/affliction, death, teleport, physics impulses, and item interactions, but the game has hundreds of RPCs and anything can be abused creatively. Your report helps.

## Notes

- The mod waits 5 seconds after launch before starting, so other mods can finish loading first
- Extremely low overhead — the RPC monitor hot path stays under 500 ns per call
- Bombing detection and RPC monitor are observation-only, perfect for collecting evidence after an incident
