# PeakWorkshopCNScanner 0.1.0

A small, independent diagnostic plugin for PeakWorkshop 0.2.0 / PEAK 2.6.a. It marks suspected untranslated text in the Workshop UI and exports local HTML/JSON reports. It observes the final IMGUI output, including prefab and material labels, without replacing text or changing map data.

This is a local trial build, not a release package. [中文使用说明](使用说明.md) · [Validation](VALIDATION.md)

## Controls

Scanning and markers are enabled by default. Open each page and leave it visible for a few seconds; scroll through lists and open dropdowns/tooltips.

| Shortcut | Action |
| --- | --- |
| Ctrl + F12 | Pause/resume scanning and markers |
| Ctrl + Shift + F12 | Export the current report |
| Ctrl + Alt + F12 | Toggle markers while continuing to record |

Orange: matches an original built-in localization literal. Blue: looks like a resource key. Yellow: needs review, including possible author content. Every category is a **candidate**, not an automatically confirmed defect. Marker IDs correspond to report rows. Original labels stay intact; small controls may only show a border.

Reports are saved every 15 seconds when changed, on manual export, and on normal exit:

`BepInEx/PeakWorkshopCNScanner/<session>/report.html` and `report.json`. `latest-session.txt` points to the latest export. A crash may lose the last unexported interval. Nothing is uploaded.

## Scope and limits

Only actual IMGUI draws inside 17 verified PeakWorkshop `OnGUI` entry points are observed. The loading overlay renders its TMP strings through IMGUI and is included. Input/password fields, unopened pages, native TMP/world labels, and configuration screens drawn by another plugin are excluded. Window occlusion is not resolved. English alone does not prove a translation is missing; author names and descriptions may need to remain unchanged. Numbers/IDs, obvious URLs/Windows paths, shortcuts and common technical abbreviations are filtered heuristically.

New strings are analyzed every 0.5 seconds with bounded work: at most 64 classifications and 4 evidence stacks per sample frame, a soft 2 ms analysis budget, and 250 markers per frame. The analysis budget excludes marker rendering. Cache: 8,192 entries / 2,000,000 characters. Reports: 2,000 records by default. Text longer than 16,384 characters is not scanned and sets an explicit report flag. These limits can leave candidates uncaptured; a report is never proof that a page has no omissions.

No repeated type/field discovery, scene-object search, or unbounded string cache occurs in steady drawing. Reflection binds exact non-generic methods/delegates once at startup; first-record stack metadata is read only under the evidence budget. File serialization/writes run on a worker; snapshots are copied on the main thread. No generic `Strip<T>`/grid patches are installed.

Config: `BepInEx/config/com.wuyachiyu.peakworkshopcnscanner.cfg`. `Enabled`, `ShowMarkers`, `ShowStatus`, `SampleIntervalSeconds`, `MaximumRecords`, and exact-text `IgnoreExactText` are available. Capacity and ignore-list changes take effect after restart. The plugin records whether the existing Chinese patch reports itself enabled; it does not require that patch to scan an English/German baseline.

## Build and checks

Run from the workspace root. Build independently before deploying (the project default OutputPath follows the workspace profile convention).

```powershell
dotnet build 'MOD开发/PeakWorkshopCNScanner/PeakWorkshopCNScanner/PeakWorkshopCNScanner.csproj' -c Release -p:OutputPath='C:/Users/Administrator/Desktop/MOD/PEAK/.build/peakworkshopcn-scanner/candidate/'
dotnet build 'MOD开发/PeakWorkshopCNScanner/tests/Tests.csproj' -c Release
& 'MOD开发/PeakWorkshopCNScanner/tests/bin/Release/net472/Tests.exe' 'C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/cache/PeakCode-PeakWorkshop/0.2.0/plugins/PeakWorkshop/PeakWorkshop.dll' 'C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/Managed/UnityEngine.IMGUIModule.dll' 'C:/Users/Administrator/Desktop/MOD/PEAK/.build/peakworkshopcn-scanner/test-output'
```

`tools/Catalog` generates the compressed evidence catalog and verified OnGUI token list from the unmodified original DLL. Runtime startup requires the matching original SHA256; an unknown version fails closed with a log message.

Only copy the scanner DLL after PEAK exits. To remove it, exit the game and remove/disable `PeakWorkshopCNScanner.dll`. Existing maps, resource keys, the original Workshop DLL and the Chinese patch are independent of the scanner.
