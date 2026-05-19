# DreamyAscent Generation Logic Checkpoint - 2026-05-18

## Scope

User reports generation issues remain, especially floating child spawned objects such as coconuts/chains under official template generation.

This checkpoint preserves the investigation before changing code:

1. DreamyAscent current generation logic.
2. Official PEAK 1.62.a generation logic.
3. Same-type MOD logic: TerrainRandomiser and TerrainCustomiser.
4. Differences, risks, and likely repair direction.

## Current State

No new code fix has been made for this investigation yet.

Known dirty files before this checkpoint:

- `DreamyAscent/Services/DaRuntimeEditService.cs`
- `memory/CHANGELOG.md`
- `memory/mods/DreamyAscent/MAP_GENERATION_RESEARCH_NOTES.md`
- `memory/mods/DreamyAscent/RECENT.md`

## DreamyAscent - Initial Findings

Main file:

- `MOD开发/DreamyAscent/DreamyAscent/Services/DaRuntimeEditService.cs`

Important methods to verify:

- `RunSegment`
- `RunOfficialSegment`
- `RunGrouper`
- `ClearSpawnedRuntimeItemsBeforeGeneration`
- `RunGrouperGeneration`
- `RunLateGrouperGeneration`

Initial concern:

- `RunGrouper()` currently performs a custom cleanup before running a grouper.
- `RunGrouperGeneration()` has custom late-grouper behavior and may bypass the exact official/other-MOD `PropGrouper.RunAll(true)` pipeline.
- DreamyAscent currently has no Harmony generation patch for `PropGrouper.RunAll`, `PropSpawner.SpawnNew`, or `LevelGenStep.Spawn`. The runtime editor calls official generation methods directly plus local cleanup.

Current local generation chain:

1. UI calls `DaRuntimeEditService.RunSegment(segment)` or `RunGrouper(grouper)`.
2. `RunSegment` in official mode calls `RunOfficialSegment`.
3. `RunOfficialSegment` filters by current variant default baseline if present, then optionally skips selected high-risk Root/Jungle rock groupers, then calls `RunGrouper`.
4. `RunGrouper` builds a before snapshot, calls `ClearSpawnedRuntimeItemsBeforeGeneration`, then calls `RunGrouperGeneration`.
5. `RunGrouperGeneration` calls `grouper.RunAll(true)` for Early groupers, but calls DreamyAscent custom `RunLateGrouperGeneration` for Late groupers.
6. `RunLateGrouperGeneration` calls `grouper.ClearAll()`, executes `LevelGenStep`s whose nearest parent `PropGrouper.timing == Late`, then executes only `AfterCurrentGroupTiming` deferred steps.

Important local cleanup behavior:

- `ClearSpawnedRuntimeItemsBeforeGeneration` scans all scene `Item` and `PhotonView` candidates.
- A runtime item candidate must be active, have `PhotonView`, and have a kinematic `Rigidbody`.
- It collects candidates through `SpawnedItemTracker`, then resets tracker state by reflection.
- It also does spatial matching around `Spawner`/`SingleItemSpawner` origins.
- It destroys matches through `PhotonNetwork.Destroy` if possible, otherwise `DestroyImmediate`.

Risk in this cleanup:

- It is not part of official `PropGrouper.RunAll` and not seen in TerrainCustomiser/TerrainRandomiser manual generation paths.
- The kinematic `Rigidbody` filter fits coconut-style item spawns, but misses non-kinematic room items and non-item Photon objects.
- Resetting `SpawnedItemTracker` state is local reflection state surgery. It may make a spawner re-spawn, but it is not equivalent to official map segment lifecycle cleanup.
- If the wrong items are not matched spatially, old items remain floating after their parent terrain/tree is regenerated.
- If the right items are matched but clients disagree, multiplayer state can diverge.

Current CustomBlank chain:

- `RunSegment` in `CustomBlank` mode calls `ClearSegmentRuntimeChildren`.
- This clears grouper children, loose levelgen children, known loose decoration containers, capybaras, and beach/luggage objects.
- `ShouldPreserveGrouperForCustomBlank` currently returns false.
- This is separate from official template generation issues, but it confirms DreamyAscent relies on manual scene cleanup in multiple places.

Parent-child registry usage:

- `parent-child-registry.json` is loaded by `DaParentChildRegistryService`.
- Current usage is UI/diagnostic display through `DaCustomiserWindow` and copied DLL data.
- It does not currently drive runtime official generation or cleanup.
- Therefore the parent-child sample file by itself cannot fix floating coconuts/chains unless code starts using it for generation/lifecycle handling.

UI trigger to verify:

- `MOD开发/DreamyAscent/DreamyAscent/UI/DaCustomiserWindow.cs`

## Official PEAK 1.62.a - Initial Findings

Reference directory:

- `引用参考代码/反编译/1.62.a/Assembly-CSharp`

Important files:

- `PropGrouper.cs`
- `PropSpawner.cs`
- `Spawner.cs`
- `BerryBush.cs`
- `SingleItemSpawner.cs`
- `MapHandler.cs`

Initial concern:

- Coconut-like children are not simple hierarchy children. `BerryBush.SpawnItems()` uses Photon room item instantiation, so floating coconuts are runtime spawned items, not just transform children.
- Official `MapHandler` handles `ISpawner.TrySpawnItems()` during segment/global/campfire progression, and tracks spawned views for segment cleanup.

Official `PropGrouper.RunAll` flow from decompile:

1. Verify `PropSpawner` prefab references.
2. `ClearAll()` calls `Clear()` on all child `LevelGenStep`s.
3. Collect all child `LevelGenStep`s.
4. Classify each step by nearest parent `PropGrouper.timing` into early or late lists.
5. Execute early steps directly.
6. Collect and run `AfterCurrentGroupTiming` deferred steps.
7. A compiler-generated `Done` local function executes late steps and deferred steps again, then validates. The decompiled visible body does not show the call site clearly.

Official `PropSpawner` behavior:

- `Execute()` calls `Clear()` then `SpawnNew(false)`.
- `Clear()` destroys direct children under the spawner transform and clears `_deferredSteps`.
- `SpawnNew(false)` spawns props as transform children, calls `SpawnDecor()`, and for post-spawn behaviors with `AfterCurrentGroupTiming` it stores deferred runners.
- `PSM_ChildSpawners` and `PSB_ChildSpawners` execute nested `LevelGenStep`s on spawned prefab children.
- `PSM_SingleItemSpawner` configures a nested `SingleItemSpawner` prefab field on a spawned shell object.

Official runtime item generation:

- `Spawner.TrySpawnItems()` is independent of `PropGrouper.ClearAll`. It spawns `PhotonNetwork.InstantiateItemRoom(...)` room items.
- `BerryBush.SpawnItems()` also uses `PhotonNetwork.InstantiateItemRoom(...)`, then buffered `SetKinematicRPC(true, position, rotation)`.
- `SingleItemSpawner.TrySpawnItems()` uses `PhotonNetwork.InstantiateItemRoom(prefab.name, transform.position + Vector3.up * 0.1f, transform.rotation)`.
- `Luggage` is a `Spawner`; opening luggage starts a coroutine that spawns items via `SpawnItems(GetSpawnSpots())` and tracks them if a `SpawnedItemTracker` exists.

Official spawn tracking:

- `MapHandler.EnsureSpawnTrackersAttached()` attaches `SpawnedItemTracker` only to campfire `ISpawner`s and `SingleItemSpawner`s named with `Backpack`.
- `SpawnedItemTracker.Init()` computes a stable ID from scene hierarchy path and transform matrix.
- `TrackSpawnedItems` stores spawned `PhotonView`s and marks `HasSpawnHistory = true`.
- `SpawnAndTrackFromItemHistory` respawns from saved item IDs and positions.
- Ordinary coconut `BerryBush` spawners on spawned palm trees are not obviously covered by `MapHandler` tracker attachment unless a tracker component exists on that exact spawner.

Official map lifecycle:

- `MapHandler.Update()` initially calls `TrySpawnItems()` for segment 0 and global spawners.
- `MapHandler.JumpToSegmentLogic()` activates target segment, then calls `TrySpawnItems()` on `ISpawner`s under the target segment and campfire roots.
- This means official item spawn timing is segment activation/progression, not `PropGrouper.RunAll` itself.
- Destroying/rebuilding a `PropGrouper` after these item spawns can leave room items detached from their original terrain/tree if those room items are not explicitly destroyed.

## TerrainCustomiser - Initial Findings

Reference directory:

- `引用参考代码/反编译/BepInEx/plugins/TerrainCustomiser`

Important files:

- `TerrainGeneration/MapHandlerHelpers.cs`
- `TerrainGeneration/GenerationPatches.cs`
- `PropGrouperHelpers.cs`
- `SegmentPanelUI.cs`

Initial observation:

- TerrainCustomiser appears to call `PropGrouper.RunAll(true)` as the primary generation entry.
- It uses a postfix around `PropGrouper.RunAll` to run late steps, rather than replacing the main generation path with a separate custom late-only pipeline.

More detailed behavior:

- UI segment generate button calls `segment._sourceObject.RunAll(true)`.
- Startup patch `MapHandler.Start` applies properties, seeds `Random`, then calls `MapHandlerHelpers.RunAllSegments(__instance.segments)` and `RunGobal(__instance.globalParent)`.
- `RunAllSegments` calls each segment parent `PropGrouper.RunAll(true)`.
- Harmony postfix on `PropGrouper.RunAll`:
  - Collects all child `LevelGenStep`s whose nearest parent `PropGrouper.timing == Late`.
  - Calls `levelGenStep.Go()` for each late step.
- TerrainCustomiser also patches `PropSpawner.Add`, `PropSpawner.SpawnDecor`, `DecorSpawner.Add`, and `LevelGenStep.Spawn` for batching/performance and PhotonView ID handling.
- After startup terrain generation, host assigns unassigned PhotonView IDs and sends them to clients.

Interpretation:

- TerrainCustomiser treats the official `RunAll(true)` as the base pipeline and compensates for late steps with a postfix.
- It does not use a pre-generation scan/destroy of Photon room items for manual segment generation.
- Its main custom generation is intended at map start, before ordinary map progression spawns many room items.

## TerrainRandomiser - Pending

Reference directory:

- `引用参考代码/反编译/BepInEx/plugins/TerrainRandomiser`

Need to verify:

- `TerrainGeneration/PropGrouperHelpers.cs`
- `TerrainGeneration/MapHandlerHelpers.cs`
- `TerrainGeneration/GenerationPatches.cs`
- `Plugin.cs`

Verified behavior:

- `MapHandler.InitializeMap` prefix runs if randomiser is enabled.
- It applies biome/variant swaps before map initialization continues.
- It ensures all segment and variant segment parents have root `PropGrouper`s.
- It calls `PropGrouperHelpers.RunRootPropGrouper(Singleton<MapHandler>.Instance.GetComponent<PropGrouper>())`.
- After generation, host assigns unassigned PhotonView IDs and sends them to clients.

TerrainRandomiser custom root generation:

1. Clear global deferred step dictionary.
2. `rootGrouper.ClearAll()`.
3. Collect all child `LevelGenStep`s.
4. Split into early and late by nearest parent `PropGrouper.timing`.
5. Execute early steps, collecting deferred steps.
6. Send outgoing Photon commands.
7. Optionally bake lightmap.
8. Send outgoing Photon commands again.
9. Execute `AfterCurrentGroupTiming` deferred steps.
10. Execute late steps.

TerrainRandomiser generation patches:

- Patches `PropSpawner.SpawnNew` and `PropSpawner_Sphere.SpawnNew` for batched raycasts.
- Keeps post-spawn behavior/deferred handling equivalent enough to official.
- Patches `LevelGenStep.Spawn` so spawned props with PhotonViews get assigned/collected for network sync.
- Handles Looker/bridge-related debug/log behavior.

Interpretation:

- TerrainRandomiser is not a local per-grouper editor path. It rebuilds the full map generation hierarchy at initialization with global timing and network ID handling.
- It does not validate DreamyAscent's current custom late-only `RunLateGrouperGeneration` for per-grouper regeneration after official item spawns already happened.

## Working Hypotheses

These are not conclusions yet:

- DreamyAscent may be diverging from official and same-type MOD generation order.
- The custom cleanup of runtime spawned items before generation may be risky for networked spawned objects.
- The custom late-grouper path may skip official deferred/post-spawn behavior or run it in the wrong order.
- Floating coconuts/chains likely require tracing `ISpawner`, `SpawnedItemTracker`, Photon-instantiated room items, and post-spawn behaviors, not only PropSpawner transform cleanup.

## Current Conclusions

Update after the 2026-05-18 23:05 test log:

- DreamyAscent itself loaded and generated without a new crash.
- The latest host log showed Beach official template generation only.
- Pre-generation runtime item cleanup worked for old Photon room items:
  - 125 old runtime `Item` objects destroyed through Photon.
  - Breakdown: 36 coconuts, 67 berries, 21 mushrooms, 1 medicinal root.
  - No `nearby runtime item not matched` lines were emitted.
- The log still could not prove that the new post-generation scene was clean, because diagnostics only described old cleanup candidates before `RunAll`.
- WhySoLaggy's `Destroy flood` warning matched the 125 runtime item cleanup and is expected from the current bulk cleanup path.
- Rope was suspicious:
  - `WallProps/Ropes` changed from `children 0->12`.
  - A `RopeDynamic(Clone)` RPC existed early in the log.
  - Current item cleanup did not cover `RopeDynamic`, because it only scanned `Item`/kinematic item-style Photon objects.

Important source-level conclusion:

- `PropGrouper.RunAll(true)` rebuilds `LevelGenStep`/`PropSpawner` hierarchy, but does not automatically refresh all runtime child spawns.
- Coconut/berry/mushroom objects are spawned later through `ISpawner.TrySpawnItems()` as Photon room items.
- `RopeAnchorWithRope` spawns `RopeDynamic` in `OnJoinedRoom()` or via `SpawnRope()`, not as a normal `PropSpawner` child.
- Therefore a manual runtime editor generation must handle three phases:
  1. Destroy old runtime children that are no longer parented under the generated hierarchy.
  2. Run the official grouper pipeline.
  3. Refresh runtime spawns such as `ISpawner` items and `RopeAnchorWithRope` ropes after the new hierarchy exists.

Implemented after this checkpoint:

- `DaRuntimeEditService.RunGrouper` now clears old runtime ropes before regeneration.
- It uses `RopeAnchorWithRope.ropeInstance` and `Rope.attachedToAnchor` to avoid distance-based rope deletion.
- It assigns local PhotonView IDs to newly generated grouper children where possible, following the TerrainCustomiser/TerrainRandomiser pattern for host-side generated PhotonViews.
- It refreshes runtime item spawners after generation via reflective `TrySpawnItems()`.
- It excludes `Luggage` from automatic item refresh, because luggage should spawn contents only when opened.
- It refreshes `RopeAnchorWithRope` by invoking `SpawnRope()` after new anchors exist.
- It adds post-generation diagnostics:
  - `post-generation runtime item`
  - `post-generation runtime rope`
  - `post-generation runtime summary`
  - `refreshed runtime spawns after generation`

Known remaining risk:

- Host-side ViewID assignment is now present for local generated objects, but full client synchronization is not yet a complete DreamyAscent system.
- TerrainCustomiser/TerrainRandomiser publish assigned ViewIDs to clients through their own room-property protocol. DreamyAscent does not yet have that protocol.
- For the current host/local test, the next log should be enough to determine whether floating coconuts/chains are old leftovers, missing refresh, or newly spawned with bad source positions.

1. Floating coconuts are explainable with current architecture.

Palm trees are transform children generated by `PropSpawner`, but coconuts are room items spawned later by `BerryBush`/`Spawner` using Photon. Regenerating or clearing the tree parent does not inherently remove those Photon room items. DreamyAscent tries to remove them by spatial/runtime scanning, but that is heuristic and can miss items.

2. Floating chains may be the same class of problem, but the exact source still needs object-name evidence from logs/diagnostics.

Likely sources include luggage/SingleItemSpawner/runtime Photon item spawn or a prefab child with PhotonView/physics. It should be traced by item name, source spawner path, and whether it has `Item`, `PhotonView`, `Rigidbody`, `Luggage`, or `SingleItemSpawner`.

3. DreamyAscent currently mixes three incompatible models:

- Official `PropGrouper.RunAll(true)` for early groupers.
- Custom late-only runner for late groupers.
- Heuristic pre-destruction of runtime Photon items.

Official and same-type MODs either rely on official map progression or rebuild at map start with global patches/network ID handling. They do not provide evidence that per-grouper post-spawn room-item cleanup by spatial scan is safe or complete.

4. The parent-child registry is currently mostly evidence/UI, not runtime behavior.

It can identify risky templates and relationship candidates, but it is not currently used to preserve/remove/recreate child spawns during generation.

5. The most suspicious implementation divergence is `RunLateGrouperGeneration`.

TerrainCustomiser uses `RunAll(true)` plus late-step postfix. TerrainRandomiser uses a root-wide early/deferred/late pipeline. DreamyAscent uses a per-late-grouper replacement that runs only steps whose nearest parent is Late and only `AfterCurrentGroupTiming` deferred steps. This may skip behavior, change ordering, and fail nested parent-child spawners.

6. The second suspicious implementation divergence is pre-generation runtime item cleanup.

It can only clean items it can identify. It currently depends on active PhotonView + kinematic Rigidbody and proximity/tracker state. Official room items not matching this filter remain. Client/host room item state can also differ.

## Recommended Next Repair Direction

Do not immediately add another wider spatial cleanup. First make generation trace deterministic enough to name the broken source.

Priority 1:

- Add/expand diagnostics for runtime spawned items around a generated segment/grouper:
  - item name
  - `PhotonView.ViewID`
  - has `Item`
  - has `Rigidbody` and kinematic state
  - position
  - nearest `ISpawner` path and type
  - nearest `LevelGenStep`/`PropGrouper` path
  - whether it matched tracker cleanup
  - whether it matched spatial cleanup
  - whether `PhotonNetwork.Destroy` succeeded

Priority 2:

- Align `RunGrouperGeneration` with a known model:
  - Prefer `grouper.RunAll(true)` as the primary path.
  - If late steps are missing, add a controlled equivalent of TerrainCustomiser's late-step postfix for the manual generation call, not a separate custom late-grouper replacement.
  - Avoid duplicating late steps if another mod already patches `PropGrouper.RunAll`.

Priority 3:

- Separate transform child regeneration from Photon room item lifecycle:
  - For official template manual regeneration, either do not try to re-run item-spawning room objects, or implement a source-spawner-indexed cleanup that knows which items came from which `ISpawner`.
  - For `Spawner/BerryBush/SingleItemSpawner`, use trackers when present; when absent, log a precise unresolved warning instead of silently relying only on proximity.

Priority 4:

- Use the parent-child registry as a risk/behavior input:
  - Mark templates with `HasKnownSpawner`, `HasSingleItemSpawner`, `HasPhotonView`, child `LevelGenStep`s.
  - Block or require explicit handling for templates that spawn Photon room items.
  - Later, use it to recreate supported parent-child chains deliberately.

## Open Questions For Next Step

- Which exact object name is the floating chain? Need recent host/client log lines or diagnostic output listing nearby runtime Photon items.
- Are floating coconuts produced after clicking `GenerateSegment` in official template, after map progression, or after opening luggage/interacting with spawners?
- Is TerrainCustomiser/TerrainRandomiser active at the same time as DreamyAscent during tests? If yes, their Harmony patches can change `RunAll` behavior and cause double late-generation if DreamyAscent also manually runs late steps.
- Does the current test run include client logs where host destroyed room items but client retained them?

## Next Checks

1. Re-read DreamyAscent generation code with line references.
2. Re-read official `PropGrouper`, `PropSpawner`, `Spawner`, `BerryBush`, `SingleItemSpawner`, `MapHandler`, plus child/post-spawn helper classes.
3. Re-read TerrainRandomiser and TerrainCustomiser generation implementation.
4. Update this file with concrete differences and proposed fix order.

These checks are complete for the first pass. The next actionable step is code-level repair, starting with deterministic diagnostics and then replacing DreamyAscent's custom late-grouper path with a known-compatible path.

## Implementation Pass 1 - 2026-05-18

Changed file:

- `MOD开发/DreamyAscent/DreamyAscent/Services/DaRuntimeEditService.cs`

Build result:

- `dotnet build ... DreamyAscent.csproj -c Release` passed with 0 warnings and 0 errors.
- Output DLL was written to `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\DreamyAscent.dll`.

Implemented changes:

1. Runtime item cleanup diagnostics are now per-object instead of count-only.

New logs include:

- cleanup candidate object name
- hierarchy path
- position
- `PhotonView.ViewID` if available
- whether it has `Item`
- `Rigidbody` and `isKinematic`
- nearest known runtime item spawner path
- nearest distance
- match reason, such as tracker or spatial spawner path
- whether destroy used Photon or local `DestroyImmediate`

2. If no runtime cleanup candidate matches, DreamyAscent now logs nearby runtime items around known `Spawner`/`SingleItemSpawner`s under the generated grouper.

Purpose:

- If coconuts/chains still float, the log should identify whether the object was visible to the cleanup scan, why it failed to match, and which source spawner it was nearest to.

3. `RunGrouperGeneration` no longer replaces Late groupers with DreamyAscent's custom late-only pipeline.

New behavior:

- Verify grouper prefab refs.
- Call official `grouper.RunAll(true)` for all groupers.
- If no external TerrainCustomiser/TerrainRandomiser `PropGrouper.RunAll` postfix is detected, collect nearest-parent Late `LevelGenStep`s and call the same reflective `Go` path used elsewhere in DreamyAscent.

Reason:

- This is closer to TerrainCustomiser's model: official `RunAll(true)` plus late-step supplement, instead of a separate late-grouper replacement that skipped parts of the official path.

Known risk:

- If the base game build actually executes hidden late steps inside `RunAll(true)`, the manual supplement can still double-run late steps when no external postfix is present. The current decompile and same-type MOD behavior suggest the supplement is needed, but test logs should be checked for doubled late output.
