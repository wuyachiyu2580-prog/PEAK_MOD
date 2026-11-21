# WhySoLaggy

Find out which mod is making your game lag, and who is bombing your room.

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
  - The log records the suspect player's name and ID number
- **Observation only** — does NOT kick, block, or interfere with anyone. Just records evidence.

---

## Log Files

After your session, open the BepInEx folder in your game directory. You'll find two files:

| File | What it records |
|---|---|
| `BepInEx/WhySoLaggy.log` | Performance data: FPS, mod timing, lag causes |
| `BepInEx/WhySoLaggy_Abuse.log` | Bombing evidence: suspicious events, suspect players, timeline |

These are recreated every time you launch the game, so they won't grow forever.

---

## How to Analyze the Logs

### Option A: Give it to any AI (Recommended)

Copy-paste the log content into any AI assistant (ChatGPT, Claude, etc.) and ask:

> "This is a performance/abuse log from my game. Please tell me:
> - Which mod is causing the most lag?
> - Were there any suspicious players?
> - What happened and when?"

The AI will give you a clear summary.

### Option B: Check it yourself

- **Performance log**: Look for lines with `!` or `!!!` — the number in `ms` shows how slow it is
- **Abuse log**: Look for lines with `⚠ ABUSE ALERT` — the player name and ID are shown right after

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

## Installation

1. Install **BepInExPack PEAK**
2. Place `WhySoLaggy.dll` into `PEAK/BepInEx/plugins/`

No other mods required.

## Contact

- Author: wuyachiyu
- QQ Group: 1093172647

## Notes

- The mod waits 5 seconds after launch before starting, so other mods can finish loading first
- Extremely low overhead — won't affect your game performance
- Bombing detection is observation-only, perfect for collecting evidence after an incident
