# Changelog

## 开发构建 2026-11-17（2026-11-18 验收）

- 新增Diagnostics/PhaseTiming与PhaseTimingSeconds：默认关闭，一次30–60秒窗口，结束自动关闭。
- 聚合网络发现/重试/清理、20个场景步骤、标签刷新/投影和Update；附峰值对象/标签/选择代数等上下文。P95为直方图上界，阶段嵌套不可相加。
- 原扫描/晚安莓/共享行为保持；先采集证据再决定周期尖峰优化。
- 184项helper+104项生产流程检查通过；真实依赖Release零警告零错误。本次隔离构建未部署、未实机。

## Unreleased (development)

- Republish shared presets after room or host changes, retry rejected submissions, and automatically publish resolved targets when the item database becomes ready.
- Ignore unrelated or unchanged room properties without extending timed scans or interrupting local preset editing. Keep the existing host-transition grace period.
- Apply scan mode and duration changes consistently from ModConfig and the preset window. Labels keep updating and expiring while a selection window is open.
- Close the rename dialog before closing the preset window when pressing Escape. Update local settings without rebuilding preset rows.
- Cache effective selections, reuse unchanged label bindings and classification results, and update distance text without repeating title layout. Recheck scene filters and same-scene camera changes.
- Remove unused internal scan bookkeeping and helper code while preserving legacy preset migration and shared-property compatibility.
- Combine unpicked and picked Napberries into one Napberry entry under Food. Existing selections for either form continue to work.
- Place unpicked Napberry labels on the fruit, and remove them when it is hidden or picked up. Empty plants no longer remain labeled.
- Apply the Ground filter to unpicked Napberries; picked fruit keeps the existing ground, held and backpack visibility rules.

Validated with 184 helper checks, 97 production state-flow checks, and a Release build against the installed PEAK 3.1.0 assemblies. This build has not been deployed or playtested. Performance gains have not been measured in-game.

## 0.1.3

- Distinguish hidden mandrakes from medicinal roots before pickup, using their actual pickup identity. Terrain plants follow the Ground filter.
- Preserve existing custom selections while correcting the untouched Survival Medical preset.
- Group ordinary luggage behind one entry, with region and loot-pool filters available in the expandable advanced view. Revival statues appear under Landmarks.
- Use one label per trap chest. Opened chests stop appearing unless their trap is still active.
- Add Current Segment / Whole Map scope. The default includes the current segment and its entry camp; unlimited distance stays within that scope. Carried items follow their current position.
- Use scene hierarchy and supporting terrain for segment detection, with runtime progress boundaries as a fallback. Unassigned objects on maps without usable boundaries are limited to the nearby 40 metres in Current Segment mode.
- Fix mixed-language backpack/statue suffixes and variant counts. New filters and status text follow the same language rules; preset target summaries respect the name-language setting.
- Refresh open windows and existing labels after language changes, and use an available Chinese-capable game font when required.

Validated against PEAK 3.0.1 assemblies. In-game rendering, multiplayer behavior and custom-map boundaries still require playtesting.

## 0.1.2

- Clarified that player-name labels are for fun only: late joins and missing placement information can leave names unavailable, while some objects only show their item name and distance.
- Added a built-in `Player Placed` preset and a global `Player names` switch for supported placed targets.
- Added labels for placed checkpoint flags, shelf shrooms, bounce shrooms, cloud fungus, scout cannons, chain-shooter vines, pitons, and rope variants.
- Merged duplicate placed-target entries into their item entries and migrated existing selections. Magic bean vines show names and distances without guessing the planter.
- Improved hammered-piton and rope detection, including delayed object initialization and separate rope/anchor ownership.
- Updated placed-target detection for PEAK 2.4.b, excluded system ropes, and fixed beach breakable bridges being mistaken for chain-shooter vines.
- Thrown mushrooms only show a player name when a unique local throw record identifies the player; other targets retain their name and distance labels.
- Removed custom inventory cleanup for rope shooters and constructables, leaving item consumption and placement to the game.
- Corrected categories for jetpacks, rocketpacks, healing items, deployables, mystical variants, creatures, honeycomb, and chess pieces.
- Kept the statue fragment name mapping and geyser positioning fixes.
- Added a maximum-distance slider with 20-metre steps (`0` means unlimited) and tidied the scan settings and player-name controls.
- Split item names, player names, and distances into stable rows with lighter outlines and shared shadows.
- Spread target discovery across frames and cached item definitions, placed-target data, player names, and unchanged label text to reduce repeated work.
- Reduced each label from fifteen text objects to three text rows and one arrow, without modifying the game's font materials.
- Lowered the UI canvas sorting order to improve compatibility with TrueFinalAscent dropdown lists while keeping the selection window interactive.
- Updated ModConfig menu and language-change support. Translations only affect this mod's labels and dropdown text; saved values, presets, and multiplayer sharing remain compatible.

## 0.1.1

- Added a `Statue` scan range for selected amulets.
- Added labels for visible, uncollected amulet fragments held by the chapter 1-4 amulet statues.
- Statue fragments use the selected amulet names and do not become separate selectable targets.
- Added safer PEAKLib.ModConfig localization that only changes WhereIsThing's own visible names and descriptions.
- Preserved the original configuration sections, keys, enum values, preset data, and multiplayer sharing behavior.

## 0.1.0

- Initial release of WhereIsThing.
- Added location labels for items, luggage, hazards, and the Gloom Bell Tower.
- Added local presets, host-shared presets, and read-only preset selection for clients.
- Added three built-in fallback presets: Survival Medical, Achievement, and Ascent 8.
- Added support for creating, editing, renaming, publishing, hiding, and deleting custom presets.
- Each player can independently choose a preset and configure scan locations, display mode, and duration.
- Added master-client switch handling and built-in fallback presets when the host does not have the mod.
- Added English and Chinese UI text, target search, and ModConfig localization.
- Added selectable game fonts for location labels with automatic Chinese glyph fallback.
- Made location labels thinner by disabling bold text by default and reducing the outline weight.
- Font, bold, size, and language changes now apply to existing labels without rescanning.
