# StateKeeper 0.1.0

- Reworked run storage into bounded 30-second GZip JSON chunks.
- Moved checkpoint serialization and compression off the Unity main thread.
- Replaced repeated player IDs, status names, and distance objects with compact indexed arrays.
- Removed unchanged-inventory allocations by comparing value-type fingerprints first.
- Added active-chunk recovery and chunk-aware favorite/retention handling.
- Added storage tests for 8-player chunks, recovery, favorites, and recent-run cleanup.

- Added first-phase PEAK run telemetry collection.
- Added RunId-based active-run recovery and formal end detection.
- Added 5Hz stamina, state, position, and pairwise distance samples.
- Added compact inventory snapshots and item lifecycle/resource events.
- Added atomic JSON persistence, corruption quarantine, favorites, and recent-run retention.
