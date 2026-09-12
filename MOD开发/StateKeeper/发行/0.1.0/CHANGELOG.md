# StateKeeper 0.1.0

## First Public Test Release

- Fixed truncated Unfavorite and Rename labels in the English history list by widening the action area and preserving text padding.
- Fixed cloned buttons inheriting the game's Resume callback and the rename dialog rendering behind the pause menu. The input receives focus when opened, and controls are restored when leaving the page.
- Added pause-menu access to Overview, Event Timeline, Player Report, Team Relations, Items, and Data Quality.
- Added stamina and affliction bars, item search, evidence around events, danger windows, and same-player comparisons across runs.
- Added favorites, unfavoriting, deletion, and custom run names of up to 40 characters. Removed the F8 shortcut.
- Death observations are deduplicated and checked against player states instead of being counted directly. Invalid distances caused by drift around death holding positions are excluded.
- Preserved inventory observations across clock regressions. Stages follow actual progress points, with the final stage extending to the end of the recording.
- New recordings use a local monotonic clock. Background analysis cache updates do not modify original recordings.
- Removed legacy favorite entry points, unused chart APIs, and unused parameters before release. Fixed events potentially being recorded while collection was disabled. Performance diagnostics are off by default.
- Passed 64 automated tests, including read-only replay of seven existing recordings. In-game UI, input method/controller, and multiplayer performance validation remain incomplete; see the README.

## Development Foundations

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
