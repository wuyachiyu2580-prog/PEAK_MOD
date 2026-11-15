# WhereIsThing regression checks

Run `dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release` from the PEAK workspace. Requires the installed .NET 10 SDK; no extra test packages are needed.

The executable compiles the actual catalog, pickup identity, plant discovery, chest labeling, segment filter, preset codec and text helpers with small game API doubles. It checks selection migration, pickup/deselection lifecycle, duplicate chest entries, active traps, effective language, previous/next segments, overlapping terrain and carried items.

These are logic tests, not Unity tests. Native raycasts, TMP glyphs/layout, game registries, late joins and host/client behavior must be checked in PEAK. The normal plugin build still uses the installed PEAK 3.0.1 assemblies.
