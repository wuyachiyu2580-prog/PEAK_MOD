# WhySoLaggy

A performance profiling and network abuse detection mod for PEAK. Identifies which mods and patched methods cause lag, tracks FPS spikes in real time, and monitors suspicious network activity that may indicate room bombing.

All output goes to **dedicated log files** in the BepInEx folder — never pollutes the game's own log.

## Features

### Performance Profiling (Tested & Verified)

**FPS Tracker**
- Continuous frame-time monitoring with a 600-frame rolling window
- Instant spike detection: any frame exceeding the configurable threshold (default 50 ms) is flagged
- Periodic summary: average / min / max FPS and spike count

**Plugin Profiler**
- Measures every loaded BepInEx plugin's `Update` / `LateUpdate` / `FixedUpdate` callback time
- Dynamically injects timing via Harmony — no manual setup required
- Per-plugin per-frame average cost, with `!` / `!!!` warnings for heavy hitters
- Spike-frame detail: when a frame lags, instantly shows which plugin(s) consumed the most time

**Patch Profiler**
- Scans all Harmony-patched game methods and wraps them with timing hooks
- Maps each method back to its owning mod via `Patches.Owners`
- Top-N slowest methods report (configurable, default 10)
- Spike-frame detail: top 5 slowest methods with their owner mod IDs

### Network Abuse Detection (Experimental — Not Yet Tested)

> **Note:** This feature hooks into Photon networking calls to detect potential room bombing. It has **not been tested in a live environment** and its effectiveness is uncertain. Use it as an early-warning tool and please report any false positives or issues.

**Instantiate / Destroy Flood**
- Monitors `PhotonNetwork.Instantiate` and `PhotonNetwork.Destroy` call rates
- Alerts when rate exceeds threshold (default: 15 instantiates/s, 20 destroys/s)
- Logs top spawned prefab names and source players

**RPC Flood**
- Monitors `PhotonView.RPC` call rates
- Alerts when rate exceeds threshold (default: 50 RPCs/s)
- Logs top RPC source players

**Object Spike**
- Periodically counts PhotonView and MushroomZombie objects in the scene
- Alerts when count increases sharply (default: +30 in one check interval)
- Investigates PhotonView owner distribution on spike

**Periodic Report (every 30 s)**
- Room info, object counts, cumulative rates, alert count, top prefabs, player list

## Log Files

| File | Content |
|---|---|
| `BepInEx/WhySoLaggy.log` | FPS reports, plugin timing, patch timing, spike details |
| `BepInEx/WhySoLaggy_Abuse.log` | Network abuse alerts, object spike warnings, periodic abuse reports |

Both files are overwritten on each game launch. Timestamps are in `HH:mm:ss.fff` format.

## Config (ModConfig / BepInEx cfg)

### General — Performance Profiling

| Setting | Default | Description |
|---|---|---|
| SpikeThresholdMs | 50 | Frame time (ms) above which a spike is logged (16–200) |
| ReportIntervalSeconds | 10 | Seconds between periodic performance reports (5–60) |
| EnablePluginProfiling | true | Profile each BepInEx plugin's Update callbacks |
| EnablePatchProfiling | true | Profile all Harmony-patched game methods |
| TopMethodCount | 10 | Number of top slow methods in reports (3–30) |

### AbuseDetection — Network Monitoring

| Setting | Default | Description |
|---|---|---|
| EnableAbuseDetection | true | Master switch for abuse detection |
| CheckIntervalSeconds | 1.0 | Seconds between rate checks (0.5–5) |
| ReportIntervalSeconds | 30.0 | Seconds between periodic abuse reports (10–120) |
| InstantiateRateThreshold | 15 | Instantiate calls/s to trigger alert (5–100) |
| DestroyRateThreshold | 20 | Destroy calls/s to trigger alert (5–100) |
| RpcRateThreshold | 50 | RPC calls/s to trigger alert (10–200) |
| ObjectSpikeThreshold | 30 | Object count increase per check to trigger alert (5–100) |

## Installation

1. Install **BepInExPack PEAK**
2. Place `WhySoLaggy.dll` into `PEAK/BepInEx/plugins/`

No other mod dependencies required.

## Package Contents

- `WhySoLaggy.dll`

## Contact

- Author: 乌鸦吃鱼
- QQ Group: 1093172647

## Notes

- WhySoLaggy waits **5 seconds** after game launch before activating profiling, to let all plugins finish loading
- The mod automatically skips timing its own Harmony patches to avoid self-measurement noise
- **Performance profiling** (plugin timing, patch timing, FPS tracking) has been tested and works reliably for diagnosing which mod causes lag — especially useful when multiple mods are installed simultaneously
- **Abuse detection is experimental**: it hooks Photon network calls but has not been validated against actual room-bombing scenarios; thresholds may need tuning
- All profiling overhead is minimal by design (Stopwatch-based, no allocations in hot paths)
