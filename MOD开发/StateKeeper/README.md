# StateKeeper 0.1.0

**STATE KEEPER** records your PEAK expeditions so you can review player states, item observations, and supporting evidence from the pause menu.

This is the first public test release. The interface and statistics are still being improved. Analysis reflects facts and inferences available to the recording client, not an all-seeing view or a teammate rating system.

## Features

- Records regular and extra stamina, afflictions, petrification, jumps, positions, teammate distances, and inventory changes.
- Six review pages: Overview, Event Timeline, Player Report, Team Relations, Items, and Data Quality.
- Select an event to inspect nearby stamina, status, distance, and item evidence. Missing or uncertain data is explicitly marked.
- Search items by Chinese or English name, category, or effect keywords to review consumption evidence, possible assistance, and alternative explanations.
- Keeps the 10 most recent runs by default, with favorites, unfavoriting, deletion, and custom run names. Favorites do not count toward the recent-run limit.
- Compare the same player across 2-4 runs. Only reliable Steam identities are matched; different players sharing a nickname are not merged.
- English and Chinese text follows the game language. Recordings and analysis stay local, with no automatic uploads or additional network RPCs.

## Installation and Usage

1. Install through r2modman or Thunderstore Mod Manager. Requires BepInExPack PEAK 5.4.2403.
2. For manual installation, place `StateKeeper.dll` in the game's or profile's `BepInEx/plugins` folder, then start the game. Do not keep multiple StateKeeper DLLs installed. Remove any legacy development build named `PeakRunAnalytics.dll` from the plugins folder first.
3. Recording starts automatically in-game. Press ESC and find **STATE KEEPER** below the leave-game button, separated by one empty button space.
4. Open a recorded run to view its report. The first analysis reads data in the background and shows progress, with cancellation and retry available.
5. An active run cannot be favorited, renamed, or deleted. Manage it after it ends or is aborted. Custom names support up to 40 characters; leave the name blank to restore the default.

There is no F8 favorite shortcut. Use the panel to favorite, rename, or delete runs.

## Configuration

The first launch creates `BepInEx/config/com.local.statekeeper.cfg`.

| Setting | Default | Description |
| --- | --- | --- |
| `General.Enabled` | `true` | Enables collection. When disabled, sampling and item/jump event recording stop, but history remains available. An already-started run is still saved and finalized normally. Re-enabling marks an observation interruption. |
| `Advanced.DebugLogging` | `false` | Logs diagnostic timings for collection, inventory scans, and slower panel rebuilds. Enable for troubleshooting; ordinary error logging is unaffected. |
| `Advanced.StaminaEventThreshold` | `0.01` | Minimum change recorded as an immediate stamina event, from 0 to 1. Does not change the stamina sampling rate of 5 samples per second. |

No ModConfig or other menu plugin is required. Restart the game after editing the configuration to apply the new settings.

## Data and Privacy

Default Windows data directory: `%USERPROFILE%/AppData/LocalLow/LandCrab/PEAK/StateKeeper`.

- `Runs` contains recent recordings, `Favorites` contains favorited recordings, and `Analysis` contains regenerable analysis caches.
- Raw data is compressed into chunks covering approximately 30 seconds. The active run is saved approximately every 5 seconds. A complete run includes all its chunks; do not move just one JSON file.
- Deleting a run removes its recordings and analysis cache and cannot be undone in the panel. Favorite or back up runs you want to keep.
- Recordings contain player nicknames, Steam identifiers, detailed timestamps, positions, and item information. Do not publish raw files without participants' consent.
- Only current schema 3 recordings are shown. Legacy schema 2 recordings are not migrated, displayed, or automatically deleted. Outdated analysis caches are rebuilt as needed; analysis does not modify the original recordings.

## Known Limitations

- Built against local PEAK 2.4.b assemblies. Other game versions and mod combinations may affect observations.
- Confirmed deaths, raw observations, duplicates, and unconfirmed observations are shown separately. Instant death need not include a separate unconscious episode; raw event counts are not death counts.
- Healing, feeding teammates, pull-back actions, and revivals have different observation coverage. An unknown rescuer is not assigned to the host or last item holder. There is no combined rescue-count ranking.
- Teammate desynchronization, teleportation, death holding positions, and historical clock regressions can interrupt evidence chains. Attribution is not forced across these gaps. Progress follows the recorded runtime directory; the number of progress points is not the number of mountains.
- Charts use aggregated data, and range statistics may be approximate. This is not a lossless frame-by-frame replay. Item-instance text summaries are limited to 100 lines; the observation log is paginated separately.
- In-game validation is not yet complete for all resolutions, Chinese input methods, controllers, or multiplayer performance. Passing offline tests does not prove that every layout is correct or that there are no frame stalls.
- There is no automatic upload, web synchronization, or anonymized export. Uninstalling the plugin does not automatically delete local recordings.

## Original Idea

The inspiration for this mod came from an earlier project, PlayersInfo.
A few days ago, I fixed a bug that caused item‑grid information to be printed in the logs unexpectedly. The latest version should have resolved it (now it only prints when logging is explicitly enabled in the config).
I've been thinking: should I take this bug and turn it into a mod that stores data for each round—such as health, inventories, and distances between teammates and myself—and then analyse that behaviour through algorithms? I think that could be really interesting.
Since each round generates a large amount of data, the mod saves the most recent 10 rounds by default, stored at:
C:\Users\your_computer_name\AppData\LocalLow\LandCrab\PEAK\StateKeeper
