# Changelog

## [1.0.2] - 2026-04-23

- **New: Full RPC monitor** — hooks `PhotonNetwork.ExecuteRpc` to track every network command, counts per-method frequency, per-player attribution and payload bytes, flags whoever sends the most traffic
- **New: Feeding-chain tracing** — captures the full `SendFeedDataRPC → RemoveFeedDataRPC → GetFedItemRPC → Consume` chain so you can tell exactly who fed whom with which item (great for catching "sneaky feeder" incidents)
- **New: High-risk RPC detail log** — 27 watched methods (feeding, healing, status/affliction, death, teleport, physics impulse, item interactions) record full detail: sender, target GameObject name + path, parsed arguments, payload bytes
- **New: Per-method independent ring buffer** — each watched method has its own 32-entry buffer so a flood from one method (e.g. `SyncAfflictionsRPC`) can no longer drown out rare but critical methods (e.g. `SendFeedDataRPC`)
- **New: Payload byte estimation** — zero-serialization estimate of each RPC's size; reports show avg/max/total bytes per method for bandwidth-abuse detection
- **Abuse log enrichment**: when an RPC-flood alert fires, the current 1-second top methods are printed alongside the suspect player
- Hot path stays under 500 ns per RPC; zero allocations, no locks, no string concat — safe to leave on in production

## [1.0.1] - 2026-04-20

- Major improvement to room-bombing detection: now accurately identifies which remote player is causing trouble
- Added real-time on-screen alert notifications (top-left corner, red text, auto-fades after 12s)
- Notifications automatically display in Chinese or English based on system language
- Fixed incorrect attribution that blamed all activity on the local player
- Fixed potential crash caused by thread-safety issues

## [1.0.0] - 2026-04-07

- Initial release
- Performance profiling: FPS tracker, plugin timing, Harmony patch method timing (tested)
- Room-bombing detection: Instantiate/Destroy/RPC flood monitoring, object spike detection (experimental)
- Dual log files: `WhySoLaggy.log` (performance) and `WhySoLaggy_Abuse.log` (abuse detection)
