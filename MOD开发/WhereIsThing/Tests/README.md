# WhereIsThing regression checks

Run `dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release` from the PEAK workspace. Requires the installed .NET 10 SDK; no extra test packages are needed.

The executable compiles the actual catalog, pickup identity, plant discovery, chest labeling, segment filter, preset codec, multi-preset selection, item traits, label range and text helpers with small game API doubles. Its 184 checks cover selection migration, pickup/deselection lifecycle, duplicate chest entries, active traps, effective language, previous/next segments, overlapping terrain and carried items, every client restriction combination, preset unions, all six mushroom varieties, clone names, unusable ground items and distance boundaries.

Napberry coverage includes the single Food entry, official localized names, old item-only and scene-only selections, unchanged shared payloads, deselection, fruit-position labels, hidden/picked/missing/replaced fruit, denied-pickup recovery and Ground scope. These checks compile the production Napberry discovery code; they do not simulate Photon timing or Unity object destruction.

These are logic tests, not Unity tests. Native raycasts, TMP glyphs/layout, game registries, late joins, room/host transitions and live configuration changes must be checked in PEAK. The plugin build uses the installed PEAK assemblies (3.1.0 baseline checked on 2026-11-16). Override OutputPath when building for validation to avoid deploying to the profile automatically.

## State-flow regression checks

Run the following from the workspace (requires .NET 10 and Windows PowerShell):

    dotnet run --project MOD开发/WhereIsThing/Tests/StateFlow/WhereIsThing.StateFlow.csproj -c Release

The 104 checks compile production WhereIsThingPlugin.State.cs directly. Before each compilation, ExtractProduction.ps1 regenerates exact current method/type bodies from the plugin, discovery code, picker and label code into the project's intermediate directory. Tests do not keep a hand-edited implementation copy. The extractor handles the current selected methods; introducing braces in quoted text/comments there requires updating its parser or linking a separately compiled production unit instead.

Coverage includes room identity and same-name re-entry, host/mode changes, rejected and deferred submissions, automatic catalog readiness and migration, irrelevant/duplicate property callbacks, local versus shared preset switching, host grace expiry, external and own-UI scan settings, actual Update menu branches, Escape routing, selection cache invalidation, classification retry reuse, binding replacement/reconciliation, additional filters, camera refresh and incremental distance/settings updates.

Photon transport, Unity objects, rendering and configuration persistence are test doubles. Label text uses a counted fake layout sink; main-loop tests run the real Update body with fake windows and discovery. These checks do not prove native UI layout, actual network delivery, disk-save behavior, performance gains or all initialization races. The production Release build still targets .NET Framework 4.7.2 and must also pass.

## Bounded phase timing (2026-11-18)

Seven additional state-flow checks compile the production BoundedPhaseProfiler: default-off behavior, P95 upper bounds, deadline stop, peak context, no self-restart, explicit rearm and stale scope rejection. Total: 184 helper + 104 state-flow checks. Production scan policy is unchanged. Config persistence and Unity/Photon runtime behavior still require in-game checks.
