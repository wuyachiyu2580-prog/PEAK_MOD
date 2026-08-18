# PEAK MOD Memory

更新时间：2026-08-17

这是项目记忆的唯一入口。目标是让新的 AI 智能体在 1 到 3 分钟内知道：当前有哪些 MOD、近期做了什么、还有什么没做、哪些规则不能违反。

## 先读什么

1. `CHANGELOG.md`：**先看**。按时间倒序列出最近的结构/内容变更，判断要不要重读。
2. `MEMORY_INDEX.md`：总览当前结构、全部 MOD 四件套、通用规则清单。
3. `TODO.md`：永久待办和风险。
4. `common/00_用户偏好.md` + `common/01_协作与记忆规则.md`：四同步铁律、Get-Date 日期规则、说人话风格。
5. `mods/<ModName>/README.md`：当前 MOD 的专属上下文。
6. `mods/<ModName>/temp/`：**压缩恢复后先看**。读取最新 `YYYY-MM-DD.md`，找回最近阶段性判断。
7. `mods/<ModName>/RECENT.md`：近期已经做过什么。
8. `mods/<ModName>/DECISIONS.md`：已经确认过的技术决策和禁止回退项。
9. `common/02-05`：只在需要通用规则时读取（工程/日志/联机/发布）。

## 接手前检查（必做）

- 先 `list_dir memory/`，按文件大小和修改时间判断是否有新增/变更。
- 翻一遍 `CHANGELOG.md` 顶部最近的两条时间戳条目。
- 有变更再按需 `read_file` 对应文件；没变更直接按内存记忆里的结论走即可。

## 目录职责

- `mods/`：每个 MOD 一个目录，四件套 `README.md` / `RECENT.md` / `DECISIONS.md` / `FILES.md`。不要把某个 MOD 的细节写进 `common/`。
- `mods/<ModName>/temp/`：每个 MOD 的临时思考记忆区。当天文件命名为 `YYYY-MM-DD.md`，每形成 3 次阶段性判断就追加摘要；上下文压缩或换 AI 后先读最新临时文件。
- `common/`：跨 MOD 共享的规则和规范，只写可复用结论，不写某个 MOD 的流水账。
- `TODO.md`：永久待办。任何未完成、待验证、已知风险都必须同步到这里。
- `CHANGELOG.md`：只记录 memory 结构或重要内容变更，按时间倒序追加。

## 2026-05-24 DreamyAscent 暂停状态

- 用户明确要求 `DreamyAscent永久暂停`。
- `DreamyAscent` 现在是永久暂停/归档项目；除非用户明确恢复，不再继续 DA 的功能、日志、构建、诊断或 TODO。

## 2026-08-17 PlayersInfo 状态

- `PlayersInfo` 当前源码和 profile DLL 版本为 `0.2.1`。
- 0.2.1 已整合稳定玩家条绑定、统一观战目标、独立异常组件、饥饿倒计时、队友耐久条、熟食图标颜色，以及按背包实际容量显示内容/喷气背包燃料的三级配置。
- 本地额外体力条不再由 PlayersInfo 重挂层级或强制改宽度，恢复交给 PEAK 原版 `StaminaBar.Update()` 控制缩放、动画、黑边、闪电图标和石化布局；PlayersInfo 只在绿色填充内部显示当前额外体力整数，例如 `40`。
- 观战/灵魂状态继续使用 `observedCharacter -> localCharacter` 的统一目标；队友额外图形条继续隐藏。队友物品栏中的紧凑喷气背包燃料条是独立成熟改动，本轮明确保留。
- PlayersInfo 直接编译输出 DLL 路径：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\PlayersInfo.dll`；后续构建直接写入该 profile，不再输出到测试环境。
- 当前 Release 构建为 `0` warnings / `0` errors；profile DLL 为 `0.2.1.0`、`83968` 字节，时间 `2026/8/17 18:25:47`。本地额外条和 0.2.1 新增功能仍需 PEAK 2.1.a 实机验证。

## 2026-06-04 PlayersInfo 历史发布状态

- 当时 `PlayersInfo` 当前版本线为 `0.1.1`。
- `发行/0.1.1/PlayersInfo.dll` 已包含 2026-05-24 队友临时体力数字裁切修复；2026-05-30 已同步 README / CHANGELOG。
- `发行/0.1.1/wuyachiyu-PlayersInfo-0.1.1.zip` 已存在，大小 `158802`，zip 内 README/CHANGELOG 含临时体力裁切修复说明，发布 DLL 与测试环境 DLL 哈希一致。
- 下次接手 PlayersInfo 先读 `mods/PlayersInfo/temp/2026-06-04.md`，再读 `RECENT.md`。

## 2026-07-30 PeakMapBrowser 状态

- `PeakMapBrowser` 已补齐独立 memory 四件套和当天临时恢复入口。
- 当前版本为 `0.1.1`，账号、个人地图管理、上传归属、点赞同步和图片缓存已打包；session 使用 DPAPI 加密 refresh token，access token 只在内存中存在。
- 点赞仍保持账号/guest cookie 身份，IP 只限频；网页端服务端退出接口 `/api/auth/sign-out` 已在线部署并通过无 token 拒绝测试。
- 用户明确暂缓 UI 框架重构；当前继续使用 IMGUI，后续若重启 UI 任务再评估 uGUI + TextMeshPro。
- 下次接手 PeakMapBrowser 先读 `mods/PeakMapBrowser/temp/2026-07-30.md`，再读该项目 `RECENT.md` / `DECISIONS.md`。

## 2026-05-21 收口状态

- `DreamyAscent` 今天收尾在官方 `Generate Segment` 的零输出保底恢复：selected `PropSpawner` / `PropSpawner_Line` / `PropSpawner_Sphere` 执行前备份直接子物体，执行后若生成结果为 0 则恢复旧子物体，防止 Jungle/Roots/Snow 点击生成后空段。
- 下次压缩恢复或换 AI 后，优先读取 `mods/DreamyAscent/temp/2026-05-21.md`，再看 `RECENT.md` / `TODO.md`。下一步是实机复测 Jungle/Roots/Snow，并用 `backedUpSteps`、`restoredZeroedSteps`、`zero-output PropSpawner diagnostics` 判断底层 raycast/material/constraint 失败点。

## 2026-05-30 TerrainCustomiserCN 发布状态

- `TerrainCustomiserCN` 当前发布 `0.1.2`，对应 snozz 原版 `TerrainCustomiser 0.3.2`；已建立独立 memory 四件套，并新增 `temp/2026-05-30.md` 作为最新压缩恢复入口。
- 注意不要和 2026-05-12 的旧 `TerrainCustomiserCN -> DreamyAscent` 改名历史混淆；当前 `TerrainCustomiserCN` 是 2026-05-23 重新建立的新项目。
- 核心约束：以中文 UI 为主，同步对应原版必要修复；不改地图数据、联机同步键、序列化字段和 CN 专属玩法。继续使用原版网络管理器 ID `com.snosz.terraincustomiser` 与属性键 `mapData` / `propViews` / `playerInfo` / `TC_inCustomMap`，并用序列化 binder 保存原版类型名。
- `0.1.2` 跟随原版 `0.3.2` 保存路径：`%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`，并同步 Caldera/Volcano 自定义变体修复。
- 发布包在 `MOD开发\TerrainCustomiserCN\发布\0.1.2\wuyachiyu-TerrainCustomiserCN-0.1.2.zip`，README / CHANGELOG / manifest description 已中文化，包内包含 `TerrainCustomiserCNCollector.dll` 收集漏翻。

## MOD 开发项目

- `ItemInfoCN`：物品信息中文化（1.0.0 已发布）。入口：`mods/ItemInfoCN/README.md`。
- `Lantern_ShootZombies_Night`：灯笼、打僵尸、日夜和寒冷/回暖相关功能整合（0.2.1）。入口：`mods/Lantern_ShootZombies_Night/README.md`。
- `PlayersInfo`：队友状态、物品栏和观战相关 HUD 信息（0.2.1）。项目直接编译输出到 PEAK 2.0.a profile。入口：`mods/PlayersInfo/README.md`。
- `DreamyAscent`：地形定制中文化与功能修复（永久暂停/归档）。入口：`mods/DreamyAscent/README.md`。
- `WhySoLaggy`：性能、RPC、Harmony 和异常行为观测（1.0.3）。入口：`mods/WhySoLaggy/README.md`。
- `TerrainCustomiserCN`：TerrainCustomiser 中文 UI 版（0.1.2 已发布，对应原版 0.3.2）。入口：`mods/TerrainCustomiserCN/README.md`。
- `WhereIsThing`：PEAK 2.1.a 多物品位置显示 MOD（0.1.0 基础版，待实机验证）。入口：`mods/WhereIsThing/README.md`。

## 写入规则（四同步铁律）

任何记忆相关变更，必须同时完成：

1. `update_memory`（内存记忆）。
2. 改对应 MD 文件。
3. 同步 `MEMORY_INDEX.md`（新增/重命名/删除条目时）。
4. 追加 `CHANGELOG.md`（格式：`- [新增/修改/删除/规则/索引] 文件名：一句话说清楚`）。

四者缺一不可，否则新接手的 AI 会读到不一致的结论。

## 日期铁律

- 首选 PowerShell 命令：`Get-Date -Format "yyyy-MM-dd"`，用返回值写入文档。
- 命令异常才问用户。
- 禁止凭系统时间戳或对话历史猜日期。

## 2026-05-10 收口状态

- `DreamyAscent` 今天主要推进后期物品编辑基础：对象引用诊断、`ObjectCatalog.json`、只读“区段模板库”UI、区段模板库翻译和生成器范围高亮。
- 当前区段模板库是 Segment 级模板/材质清单，不等于未来山顶、山腰、洞口这类 XYZ 放置子区。
- `InspectTcCn` 是一次性诊断工具，已确认不参与发布；`tmp/InspectTcCn` 可删除。
- 最新 DreamyAscent Release 构建成功，输出到 r2modman terrain profile，0 警告 0 错误。

## 2026-05-11 续作状态

- `DreamyAscent` 的 `CustomBlank` 第一版已在实机跑通并通过构建。当前语义是跳过官方生成器、清理该段已生成的官方子物，但保留基础机制对象，例如 Caldera 的 `River`、Volcano 的 `RisingLava/Lava`。
- 最新复测把 `Caldera_Segment` 的剩余候选进一步收敛到 `ash`、`Bubbles`、`River`、`Coll`，把 `Volcano_Segment` 收敛到 `Coll`、`Plane`；这轮残留已确认主要是机制或粒子，不是未清理的普通装饰。
- 本轮结论已同步到 `mods/DreamyAscent/RECENT.md`、`DECISIONS.md` 和根 `TODO.md`，后续重点转到地图里剩余漂浮物与后期区域物品编辑架构。

## 2026-05-12 地图生成研究补强

- `DreamyAscent` 已新增 `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`，专门记录跨区段物品放置。核心模型是“来源模板 + 目标子区”：来源模板提供 prefab/默认参数/组件/风险，目标 `SubArea` 提供 XYZ、范围、ray、layer 和落地约束。
- 后续不要再只说“模板库可混合”。第一条建议验证例子是把 Jungle `Jungle_PalmTree_*` 低风险模板放到 Desert 平台/山腰子区；Roots `Redwood`、行李、藤蔓、虫类、岩浆机制等因父子/Photon/机制风险先只读标记。
- `DreamyAscent` 已新增 `mods/DreamyAscent/IMPLEMENTATION_MATRIX.md`，把官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照和多人同步逐项落到实现路径、依据、例子和资料缺口。
- `MAP_GENERATION.md` 和 `MAP_GENERATION_RESEARCH_NOTES.md` 已补强为多轮、多角度研究记录；下一步开发先做稳定 path、`DaObjectRegistry`、`SubArea` 和低风险 `PlacementRule`。

## 2026-05-13 改名状态收口

- 已收口 DreamyAscent 改名后的 Git 索引脏状态：旧 `TerrainCustomiserCN` 删除和新 `DreamyAscent` 未跟踪已整理为 staged rename/add，避免后续误恢复旧目录或误删新目录。
- 当前仍有两个无关未处理脏项：`.gitignore` 修改、`MOD开发/PlayersInfo/合并输出.txt` 删除。它们不属于 DreamyAscent/memory 改名冲突，后续处理前需确认来源。
- 已检查 PEAK 1.62.a 反编译更新：核心反编译源码与 1.61.b 哈希一致，五个 MOD 均 Release 构建通过，暂不需要代码更新；后续若实机出现新日志异常，再按具体栈定位。

## 2026-05-13 DreamyAscent 样本审计收口

- `MOD开发\DreamyAscent\data\map-data` 已完成集中审计：官方自然样本 22 个 JSON、19 个完整诊断目录；TerrainRandomiser 验证样本 29 个 JSON、7 个完整诊断目录。
- 批量复核结果：26 个诊断目录均包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`，`0 grouper` segment 为 0，未知 variant 为 0。
- 当前 `Beach / Jungle / Roots / Snow / Desert` 的全部已知变体均已覆盖；`Caldera / Volcano` 按 `DirectSegmentRoot` 处理。下一步不是继续刷图，而是基于 `sample-index.json`、`RuntimeExport`、`ObjectCatalog` 和 `ObjectReferenceMap` 提取模板快照、对象注册表和回归检查。
- 已新增 `data/tools/build_map_data_artifacts.py` 和 `data/map-data/generated/`：当前生成 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`，回归为 `pass`。下一步是把离线产物接入源码侧 `DaObjectRegistry` 和稳定 path 匹配。

## 2026-05-14 DreamyAscent UI 重构停点

- DA UI 当前处于重构测试阶段。结构面板已从旧树形缩进改为三层联动：关卡横排、区域/生成组横排、当前区域生成物/步骤列表。
- 生成物行显示 step 类型和运行时 catalog 匹配到的 item/material 数量，用于先判断每个区域扫到了哪些生成物；这仍是 UI/诊断联动，不是实际自定义生成或重叠检测。
- 明天优先实机测试三层联动是否符合截图预期，并复测 `SubArea` / `PlacementRule` 导入回显 `placementConfigs=1`。两项通过前不要直接进入 Instantiate 生成。

## 2026-05-17 DreamyAscent Snapshot V2

- `DreamyAscent` 已新增 `GeneratedChildrenSnapshot.json` 诊断，用于记录官方已生成结果、loose/special objects、脏样本原因和 TerrainRandomiser 来源标记。
- 旧 `data/map-data/1.62.a/` 与 `TerrainRandomiser/` 样本目录已删除；新采集只放 `1.62.a-snapshot-v2/` 和 `TerrainRandomiser-snapshot-v2/`。
- 下一步必须先跑一份示范样本验字段，不能直接全量重跑；示范通过后再采官方自然样本和 TR 补变体样本。

## 2026-05-20 DreamyAscent 官方生成收尾

- Beach 椰子/物品生成已正常；失败的材质 modifier replay 和 custom placement child-scale sync 已按用户要求退回。Beach 地形材质仍是未解决项，后续先补 renderer/material 诊断。
- Jungle `Generate Segment` 空段问题二次定位：最新日志已加载新 DLL，但 `Pops_Plat` / `Props_Wall` 仍 `lateSupplementSteps=0`，说明不是外部 postfix guard，而是 Late step 收集没找到 inactive 父层级下的 `PropGrouper`。
- `DaRuntimeEditService` 已改为手动沿 `Transform.parent` 查找最近 `PropGrouper` 来收集 Late steps；Release 构建 0/0，DLL 已覆盖 terrain profile。下次只需重启/重新加载游戏后复测 Jungle，预期 `lateSupplementSteps>0` 且灌木/藤蔓/蘑菇 runtime spawner 恢复。
- 同日晚些时候继续修复 Late 执行语义：日志证明 Late 收集已生效但 `Go()` 补跑仍让 Roots/Snow 许多 PropSpawner 归零。当前改为 `LevelGenStep.Execute()` + `DeferredStepTiming.AfterCurrentGroupTiming` deferred 执行，并记录 `deferredAfterGroup`。最新 DLL 时间 `2026/5/20 20:57:50`。


