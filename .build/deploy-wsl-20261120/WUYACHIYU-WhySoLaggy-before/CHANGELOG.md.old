# Changelog

## [1.0.5] - 2026-09-21

- Updated ModConfig support for the current settings menu, section names, and dropdown labels.
- Fixed settings text not following game language changes.
- Restricted translations to WhySoLaggy's own settings to avoid changing other mods' labels.
- Fixed inherited-method patch warnings during ModConfig integration and improved menu refresh cleanup.
- Kept ModConfig optional and preserved existing configuration values and diagnostic features.

## [1.0.4] - 2026-08-30

WhySoLaggy 1.0.4 is the PEAK 2.3.a-compatible maintenance release.

**Detection and attribution**
- Corrected PhotonView Ownership event classification: Request `209`, Transfer `210`, and batch Update `212`; vacant ViewId event `211` is ignored.
- Rate thresholds now attribute inbound Instantiate, Destroy, RPC, Ownership Request, and Ownership Transfer activity to remote actors. Batch Ownership updates remain audit-only.
- Added alert cooldown and per-key suppression summaries so repeated warnings do not flood text logs or the on-screen notification.
- Kept local and remote totals separate in periodic reports; malformed network payloads are warning-only.

**RPC and structured logging**
- Added a bounded RPC queue with `QueueCapacity=2048`; when full, the oldest records are discarded and recent evidence is retained.
- Added queue sequence, enqueue timestamp, enqueue frame, queue delay, runtime queue metrics, and flush timing to structured output.
- Removed duplicate watched-RPC trace output. Each watched RPC produces one complete `RpcCall` record; `RemoteRpcTrace` remains only as a compatibility enum value.
- Structured logging now buffers in memory and flushes once per second, at reports, and during shutdown.
- CSV and JSONL rotation files are independently retained by count and total size (`30` files / `350MB` by default), without deleting text logs.

**Profiler and compatibility fixes**
- Grouped FieldProbe rules by target method so each method receives at most one Prefix, Postfix, and Finalizer patch.
- PatchProfiler records actual calls and uses sampled averages only for periodic total estimates; frame spikes remain precisely timed.
- Method keys include the full type, method, and parameter signature while accepting legacy Ignore entries.
- Zombie counts use `ZombieManager.Instance.zombies.Count` with one-time failure warnings per plugin lifetime.
- Added safe bilingual ModConfig display for the WhySoLaggy title, all sections, option names, descriptions, and enum values. Persisted section/key names and values remain unchanged.
- All monitor modules support idempotent shutdown and reload; the plugin unpatches its own Harmony patches before closing logs.

## [1.0.3] - 2026-04-26

> **⚠ Testing status — client-side only.** Every 1.0.3 feature in this changelog was exercised from a **non-host (client) install**. A subset of the new diagnostics can only do real work when the install is on the **Master Client** — specifically:
> - `OnRemoteRpcEvent` Hashtable unpack (capturing real client-side RPC senders after Master relay)
> - Master-side Instantiate requester correlation (`SuspectedRequesterActor/Name/Rpc`)
> - PhotonView Ownership audit (EventCode 210/211/215)
> - Actor×Method hotspot detector
>
> These Master-only paths compile cleanly and self-check, **but have not yet been validated in an actual hosted session**. Expect potential edge-case bugs there until a host-side capture is recorded. Client-side features (FPS / RPC monitor / Instantiate rate / FieldProbe etc.) are unaffected.

**Quiet-by-default**
- `EnablePluginProfiling` / `EnablePatchProfiling` default changed to `false` — profilers are opt-in now
- `LogVerbosity` default changed to `Minimal` — text logs only carry abuse alerts; CSV/JSONL still capture everything
- Fix: abuse alerts now bypass the verbosity filter, so `Minimal` = "alerts only, but definitely alerts"
- Fix: removed orphan continuation lines when a report header was filtered but its follow-up lines weren't

**New features**
- Structured logging: `whysolaggy_data.csv` (58 cols) + `whysolaggy_events.jsonl`, auto-rotated at `MaxLogFileSizeMB`
- Startup Harmony conflict scan: `harmony_patches.csv` + alerts when the same method is patched by multiple mods
- Opt-in method stack tracer (`[MethodTracer]`): name methods, get filtered call stacks per hit
- FieldProbe: JSON-driven reflective snapshots of any field / parameter / return value at any method
- On-screen dashboard (opt-in): live FPS, frame time, GC KB/s, watched-RPC summary, last abuse alert
- GC allocation rate + Photon ping are attached to FPS / periodic reports
- **InstantiateTrace**: every `PhotonNetwork.Instantiate` captures a filtered caller stack (3 samples per prefab + 1 per 5s window, auto-forced on flood) — finally answers "which script / mod is spawning this?"
- **Watched-method library expanded** from 27 to 65+ RPC names across feeding / healing / status / death / revive / grab-kick-carry / inventory / prefab spawn / end-game / ownership / tornado / campfire
- **Master-side Relay Diagnostics** *(beta, Master-only)*: `RemoteRpcTrace` unpacks `EventCode=200` Hashtable to recover the real client sender; Instantiate rows gain `SuspectedRequesterActor/Name/Rpc/AgeMs`; `OwnershipChange` decodes EventCode 210/211/215; `OwnershipGrab` and `ActorMethodHotspot` alarms layer on top

**Performance & robustness**
- PatchProfiler throttling via `MinReportMs` — ~70–90% lower self-overhead on busy scenes
- `IgnorePluginGuids` / `IgnorePatchMethods` / `ExtraWatchMethods` config keys
- RPC hot path moved fully to main-thread pump (`ConcurrentQueue` + `PumpBatchSize`)
- Lower GC pressure across all periodic report paths; better exception reporting; null-safe patch hooks

**Docs**
- README: new **Real-world case** section with redacted dual-client lantern lit-sync + warmth diagnostics, pulled straight from a live session (`[LitSync]` / `[HuddleWarmth]` / `[FuelMath]` / `[WARMTH_LOG]` / `BugleRestore`)
- README: reworked **How to analyse the logs** into a **tag-first** workflow — `grep`/`jq` by tag now recommended, CSV pivot second, AI third (with "curate the slice first" recipe), plain-text reading fourth
- README: added six copy-paste commands (3 PowerShell `Select-String`, 3 `jq`) covering abuse alerts, lit-sync, failed warmth ticks, top RPC senders, FieldProbe hits, frame spikes

> Upgrade note: BepInEx only applies new defaults to missing keys. Delete `com.wuyachiyu.WhySoLaggy.cfg` or flip the three keys manually to pick up the quieter defaults.

## [1.0.2] - 2026-04-23

- Full RPC monitor hooks `PhotonNetwork.ExecuteRpc`; counts calls, senders and payload bytes
- Feeding-chain tracing: `SendFeedDataRPC → RemoveFeedDataRPC → GetFedItemRPC → Consume`
- 27 watched high-risk methods record sender / target / parsed args / payload size
- Per-method 32-entry ring buffer so floods can't drown out rare events
- Abuse alerts now include the current top RPC methods
- Hot path <500ns per RPC, no allocs, no locks

## [1.0.1] - 2026-04-20

- Room-bombing detection now correctly identifies the remote suspect
- Real-time on-screen alert banner (auto-fades after 12s), auto EN/中 based on system locale
- Fixed wrong attribution to the local player; fixed a thread-safety crash

## [1.0.0] - 2026-04-07

- Initial release: FPS tracker, plugin / patch profilers, room-bombing detection, dual text logs
