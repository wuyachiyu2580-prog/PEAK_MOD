# WhereIsThing regression checks

Run `dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release` from the PEAK workspace. Requires the installed .NET 10 SDK; no extra test packages are needed.

The executable compiles the actual catalog, pickup identity, plant discovery, chest labeling, segment filter, preset codec, multi-preset selection, item traits, label range and text helpers with small game API doubles. Its 184 checks cover selection migration, pickup/deselection lifecycle, duplicate chest entries, active traps, effective language, previous/next segments, overlapping terrain and carried items, every client restriction combination, preset unions, all six mushroom varieties, clone names, unusable ground items and distance boundaries.

Napberry coverage includes the single Food entry, official localized names, old item-only and scene-only selections, unchanged shared payloads, deselection, fruit-position labels, hidden/picked/missing/replaced fruit, denied-pickup recovery and Ground scope. These checks compile the production Napberry discovery code; they do not simulate Photon timing or Unity object destruction.

These are logic tests, not Unity tests. Native raycasts, TMP glyphs/layout, game registries, late joins, room/host transitions and live configuration changes must be checked in PEAK. The plugin build uses the installed PEAK assemblies (3.1.0 baseline checked on 2026-11-16). Override OutputPath when building for validation to avoid deploying to the profile automatically.
