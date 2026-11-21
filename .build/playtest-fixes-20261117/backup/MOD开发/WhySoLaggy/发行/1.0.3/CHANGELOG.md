# Changelog

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
