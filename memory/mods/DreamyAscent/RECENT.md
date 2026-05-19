# DreamyAscent Recent

Last updated: 2026-05-20

## 2026-05-20 官方生成本段：Beach 物品正常，Jungle 空段二次修复

- 用户确认 Beach 椰子/物品生成已正常；这部分来自官方整段生成改为“按 Early/Late 顺序跑 grouper + 整段统一清理 runtime spawn + 整段统一 postrefresh”。
- Beach 地形材质仍像默认 Beach；已按用户要求退回失败的“生成后材质 modifier replay”和“放置父子缩放同步”改动。当前只保留较保守的随机材质候选冻结逻辑，材质问题不作为今天继续硬改对象。
- Jungle `Generate Segment` 仍空的最新日志说明：新 DLL 已加载（不再出现 `replayedMaterialModifiers`），但 `Pops_Plat` / `Props_Wall` 仍是 `lateSupplementSteps=0`，随后 `Bushes/Trees/Vines/Mushrooms` 等 PropSpawner 从非 0 变 0，postrefresh 的 `itemSpawners=0`。
- 根因从“外部 Harmony postfix 判断跳过 Late 补跑”进一步收窄为“Late step 收集失败”：旧代码用 `GetComponentInParent<PropGrouper>()` 判断 step 最近父 grouper，在 inactive 父级/层级场景下会拿不到，导致明明有 9/18 个 step 却补跑数量为 0。
- 已修复：`CollectLateStepsForGrouper()` 改为手动沿 `Transform.parent` 向上查找最近 `PropGrouper`，包含 inactive 父层级；官方生成仍先走原版 `RunAll(true)`，再对 Late step 调用 `Go()` 补跑。`Go()` 会清理该 step 自己的子物再生成，避免简单重复叠加。
- 已删除不用的外部 Harmony postfix 检测，不再把 Jungle Late 生成寄托在其它 MOD 的 `RunAll` postfix 上。
- Release 构建通过：`dotnet build .\MOD开发\DreamyAscent\DreamyAscent\DreamyAscent.csproj -c Release`，0 warning / 0 error，DLL 已覆盖到 terrain profile，时间 `2026/5/20 0:09:54`。
- 下一次只需重启/重新加载游戏后复测 Jungle `Generate Segment`：预期日志出现 `manual late-step supplement used`，`Pops_Plat` 的 `lateSupplementSteps` 约 9、`Props_Wall` 约 18，且 postrefresh 不再是 `itemSpawners=0`。

## 2026-05-18 官方生成链追踪加固

- 为排查“点击官方 `Generate Segment` 后 Beach 的 `PlateauProps/WallProps` 直接归零、只剩地形”的问题，已在 `DaRuntimeEditService` 加临时生成追踪。
- 现在 `RunSegment` 入口会记录调用堆栈，`RunGrouper` 会记录生成前后 step 直接子物/后代数量，`RunWhitelistedStepsForSkippedOfficialGrouper` 也会记录单步前后差异。
- 这次改动不改变生成逻辑，只是让后续诊断优先看“哪个 grouper/step 从非 0 变 0”，再看入口堆栈，不再靠猜是样本缺失还是运行时清空。
- `dotnet build ... DreamyAscent.csproj -c Release` 已通过，并同步到 terrain profile 的 `DreamyAscent.dll`。
- 后续日志复核确认真实问题：Beach 官方模板 `PlateauProps` 和 `WallProps` 从非 0 直接变 0，baseline 当前变体匹配、runtime refs 完整，说明不是样本缺失或绑定丢失；反编译 `PropGrouper.RunAll()` 在当前 1.62.a 路径里只执行 Early list，Late list 被收集但未在可见路径调用。
- 已修复：`DaRuntimeEditService.RunGrouperGeneration()` 对 `PropGrouper.timing == Late` 不再调用原版 `RunAll(true)`，改用安全 Late pipeline：先 `ClearAll()`，再执行最近父 grouper timing 为 Late 的 `LevelGenStep.Execute()`，最后执行 `AfterCurrentGroupTiming` deferred steps，并按 `ValidateAfterwards` 反射触发验证。Early grouper 仍走原版 `RunAll(true)`。
- 这个修复不是恢复旧的“RunAll 后额外补跑 late steps”。旧做法会重复执行 late/child generation 并造成悬空椰子、锁链等子生成物；新做法是对 Late grouper 替代 `RunAll`，避免 double-run。
- UI 生成动作也已在点击“生成本组/生成本段/切换模板模式”前调用 `ReleasePreviewSceneIsolationForExport()`，避免在只显示单段的预览隔离状态下执行生成。
- 最新 Release 构建 0 warnings / 0 errors，DLL 已覆盖 terrain profile；下次复测必须用新开/新图，因为已经被旧 DLL 清成 0 的 `PlateauProps/WallProps` 不能自动作为干净验证基准。

## 2026-05-17 生成本段只剩地形的阶段性判断

- 用户反馈点击“生成本段”后只剩地形，像椰子树这类官方子物没有一起出来。
- 复核当前调用链后，`GenerateSegment` 走的是 `DaRuntimeEditService.RunSegment(segment)`；只有 `OfficialTemplate` 才会进入 `RunOfficialSegment()`，`CustomBlank` 只是清理/保留少量结构，不会跑官方 `PropGrouper`。
- `RunOfficialSegment()` 还会先过 `HasCurrentVariantDefaultTemplate()` 和 `ContainsRuntimeGrouper()` 过滤，并对 Jungle/Roots 做高风险 grouper 跳过，所以“只有地形”不一定是样本缺失，更可能是当前模式/过滤条件没有走到官方生成链。
- `template-snapshots.json` 里 Jungle 默认有 4 个 grouper、Beach 有 4 个、Roots 有 6 个，说明树类和装饰类步骤在样本里是存在的，不是完全没采到。
- 复核原版反编译后确认 `PropGrouper.RunAll(bool)`、`PropSpawner.Go()`、`PropSpawner_Sphere.Go()` 本身都走原版生成链；之前那段额外 late-step 手动补跑逻辑已移除，不要再加回去。
- 当前优先检查 UI 里的 `SegmentEditMode` 是否仍是 `CustomBlank`；如果是，这次“只剩地形”属于预期。如果已经是 `OfficialTemplate` 但仍只剩地形，再给 `RunOfficialSegment()` 和 `TryBindSegmentRuntimeReferences()` 加更细日志，区分是 baseline 过滤、重绑失败还是 grouper 根本没扫到。

## 2026-05-17 官方模板重复晚期步修复

- 用户确认官方模板里仍能看到悬空的子生成物，问题重新聚焦到生成时机而不是采样缺失。
- 复核原版 `PropGrouper.RunAll()` 后确认，它本身已经执行 early steps、late steps 和 deferred steps；DreamyAscent 之前在 `RunGrouper()` 里又额外调用了一次 `RunLateLevelGenSteps()`，等于把 late steps 再强制 `Go()` 一遍。
- 已删除这段二次晚期补跑逻辑，只保留原版 `RunAll(true)` 作为官方模板唯一生成入口，避免父子/延迟生成链重复触发。
- 该修复已通过 `dotnet build ... DreamyAscent.csproj -c Release`，并同步到 r2modman terrain profile 的 `DreamyAscent.dll`。

## 2026-05-17 PreviewPoses 同步 + 当前变体默认模板 baseline 初版

- 用户更新的 `DreamyAscent PreviewPoses.json` 已固化到源码目录 `MOD开发\DreamyAscent\DreamyAscent PreviewPoses.json`，并纳入 `DreamyAscent.csproj` 构建复制；Release 构建会同步到插件目录根部。`DaMapPreview.DefaultPreviewPoses` 也同步更新了 Caldera/Volcano 的代码内置 fallback，外部 JSON 仍优先。
- 补齐本轮日志中缺失的 fallback 翻译：`Default`、`BlackSand`、`CactusForest`、`Dolo`。当前没有强改外置 `localization.zh-CN.json`，避免破坏其编码；DLL fallback 已足够消除缺失映射日志，外置 JSON 后续仍可覆盖。
- 当前变体内置默认模板 baseline 已接入运行时：`DaTemplateBaselineService` 新增 `HasCurrentVariantDefaultTemplate()` / `GetCurrentVariantDefaultTemplate()`，会用当前 Segment + `NormalizedVariantName` 匹配 `template-snapshots.json` 中的官方快照，并要求缺失 grouper/step 为 0。
- 官方模板生成现在使用“当前变体默认模板”过滤官方 grouper；额外自定义运行时对象不再因为 extra runtime warning 让 baseline 失效。日志会出现 `Using current variant default template for segment generation`。
- UI 的样本资产面板新增当前变体默认模板状态、模板变体、命中 grouper/step 和 warning 计数，方便实机确认 baseline 是否接上。
- Release 构建通过并覆盖 r2modman terrain profile：`dotnet build ... DreamyAscent.csproj -c Release` 为 0 warnings / 0 errors；`DreamyAscent.dll` 与 `DreamyAscent PreviewPoses.json` 已部署到插件目录。

## 2026-05-17 Snapshot V2 全量样本审计完成 + 诊断内存优化

- 用户完成 Snapshot V2 数据采集后，已重新从完整性、纯净性、变体覆盖、模板匹配、官方/TR 来源标记、体积和回归输入角度审计样本。
- 当前有效样本集：`data/map-data/1.62.a-snapshot-v2/` 官方自然样本 21 份，`data/map-data/TerrainRandomiser-snapshot-v2/` 强制补变体样本 6 份，总计 27 份诊断。
- 审计结论：`issues=0`；所有样本包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`、`GeneratedChildrenSnapshot.json`、`TemplateSnapshotMatchReport.json`、`TemplateBaselineReport.json`；`GeneratedChildrenSnapshot.schemaVersion=3`；`potentiallyDirty=false`；官方样本 `externalMapModifierCount=0`；TR 样本均有 `TerrainRandomiser` 标记。
- 覆盖结论：Beach、Jungle、Roots、Snow、Desert 的全部已知变体均已覆盖；Caldera/Volcano 仍按 DirectSegmentRoot 处理，不需要变体补样。
- 统计结果：27 份样本共 135 segments、536 groupers、4194 steps；`TemplateSnapshotMatchReport` 全部 matched，`weakMatch=0`；`TemplateBaselineReport` 全部 matched，warning 0。
- 原始 `GeneratedChildrenSnapshot.json` 合计约 8.87GB。该体积是有意保留的原始重建资料，不做瘦身；若需要轻量数据，应从原始诊断离线派生产物，不能删字段后再要求重建官方地形。
- 已更新 `data/map-data/sample-index.json` 到 Snapshot V2 真实计数，旧零计数索引不再有效。
- 已优化运行时诊断内存峰值：`DaGeneratedChildrenSnapshotDiagnosticService` 写 `GeneratedChildrenSnapshot.json` 改为 `JsonTextWriter` 流式写出，不再先构造完整大字符串。
- 已优化运行时离线资产加载：`DaObjectRegistryService` 与 `DaTemplateSnapshotService` 改为流式 JSON 读取；左下“样本资产/模板快照/注册表”改为手动加载，避免打开 UI 或启动后自动加载开发期离线资产。
- 已优化离线产物脚本：`build_map_data_artifacts.py` 校验 `GeneratedChildrenSnapshot.json` 时只读头部字段和 `relationshipCandidates` 存在性，不再整份读取 9GB 原始快照。
- 已基于 Snapshot V2 正式重跑 `build_map_data_artifacts.py`，输出 `template-snapshots.json` / `object-registry-input.json` / `sample-regression-report.json`：`snapshots=135`、`templates=193`、`materials=25`、`regression=pass`、`issues=0`、`warnings=0`。
- 第二次 Release 构建通过并覆盖到 r2modman terrain profile，已把新版 `DreamyAscent Data\template-snapshots.json` 约 6.49MB、`object-registry-input.json` 约 2.20MB 同步到插件目录；`dotnet build ... DreamyAscent.csproj -c Release` 为 0 warnings / 0 errors。
- 现在 memory 中此前“先跑一份 Snapshot V2 示范样本”“重采官方/TR 样本”“基于 V2 重跑离线产物”的 P0 已完成；下一步是进游戏实机验证新版 Data 手动加载与 UI 不回退，然后接当前变体内置默认模板基线。

## 2026-05-17 Snapshot V2 采集重置

- 新增 `GeneratedChildrenSnapshot.json` 诊断：写出诊断时会额外记录每个 segment/grouper/step 的已生成直接子物、loose segment objects、特殊场景物件、transform/bounds/components/materials/PhotonView/SingleItemSpawner 计数、path hash/signature，并输出脏样本原因。
- 新快照 schemaVersion 已升到 2，并记录 `externalMapModifiers`。如果检测到 enabled 的 `TerrainRandomiser`，会在快照里标为 `SourceClassification=terrain-randomiser-forced-map`，并写出 TR 的 seed、section、biome、variant 选择。
- 2026-05-17 晚上又把 `GeneratedChildrenSnapshot.json` 升到 `schemaVersion=3`：每个 segment 新增 `relationshipCandidates`，每个 step/生成物/关系候选可带 `interestingComponentFields` 和子物摘要。目标是一次示范样本就能检查椰子/椰子树、`BeachSpawner.treeParent/palmTrees/spawned`、`PSM_ChildSpawners`、`PSM_SingleItemSpawner.objToSpawn`、`SingleItemSpawner.prefab`、桥、营火附属物、RisingLava、独立机关等父子/业务关系候选。
- `build_map_data_artifacts.py` 已改为拒收 `GeneratedChildrenSnapshot.schemaVersion < 3` 或缺 `relationshipCandidates` 的样本，避免旧字段样本混进新重建产物。
- 用户更新的 `DreamyAscent PreviewPoses.json` 已同步进 `DaMapPreview.cs` 默认值：`Jungle_Segment`、`Snow_Segment`、`Caldera_Segment` 的源码 fallback 位姿已更新；外部 JSON 仍优先覆盖。
- Release 构建已通过并覆盖到 r2modman terrain profile：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\DreamyAscent.dll`。
- 旧样本目录 `data/map-data/1.62.a/` 和 `data/map-data/TerrainRandomiser/` 已按用户要求删除，避免旧四件套样本和新生成结果快照混用。当前只保留 `1.62.a-snapshot-v2/` 与 `TerrainRandomiser-snapshot-v2/` 作为新采集入口。
- `data/map-data/generated/template-snapshots.json` 和 `object-registry-input.json` 暂时保留，因为当前 DLL 运行时仍会加载它们；新 v2 样本回归通过前，离线脚本不会覆盖这些运行时依赖产物。
- `build_map_data_artifacts.py` 已强制要求 `GeneratedChildrenSnapshot.json`，并会拒绝 `potentiallyDirty=true` 的样本；官方来源目录中若检测到 enabled TerrainRandomiser 会拒绝，TerrainRandomiser 来源目录中若没有 TR 标记也会拒绝。
- 该采集阶段已在 2026-05-17 后续完成并通过审计；后续不要再把“示范样本/重采样本”当作当前 P0，除非游戏版本或采集 schema 再变化。

## 2026-05-16 运行时放置初版与优先级重排

- 客机端日志已临时归档：`Player.log` 中曾出现 DreamyAscent IMGUI `ArgumentException`（左下摘要面板 Layout/Repaint 控件数不一致）以及多人环境下的 `DropItemRpc` / PhotonView / RPC 噪声。当前结论是客机端兼容和多人同步问题后置，不作为当前功能开发阻塞；先把官方模板、空白模板、混合模板、自定义放置、模板基线和诊断闭环做完。
- 当前 DLL 已对左下摘要面板做固定控件数防护，但客机端仍需未来单独多人复测确认；本阶段不继续追客机端日志。
- 已实现第一版运行时自定义放置：`CustomBlank` / `Hybrid` 会自动应用 `PlacementRule`，`OfficialTemplate` 只在手动点击“生成自定义”时应用，避免污染官方模板模式。
- 当前运行时放置只允许低风险静态模板；带 `PhotonView`、`SingleItemSpawner`、子 `LevelGenStep`、子 `PropGrouper`、`Spawner` 继承链或已知生成器组件（如 `BerryBush`、`BerryVine`、`GroundPlaceSpawner`、`Luggage`）的模板会被拒绝。
- 已把每条规则数量上限临时夹到 25，并补运行时清理统计，避免一次生成过多导致卡顿或残留判断失真。
- 已接入当前变体过滤：推荐候选和旧规则执行都会按 `NormalizedVariantName` 过滤，避免当前 Jungle 是 `Ivy` 却尝试生成其它变体的 `Jungle_SharpPlant`。
- 用户发现椰子和果子浮空后，结论是父子/生成器型模板不能当普通 prefab 直接实例化；这类绑定物和 spawner 模板后置，先只做静态低风险模板。
- 用户重新定义 `CustomBlank`：只保留起始点过渡，桥、绳子、终点、边缘中段等全部应清掉。因此保留白名单已收窄为仅 `LevelGenStep` 名称 `Start`；不再保留 `End`、`End_L`、`End_R`、`Small_End`、`Bridges`、`Ropes` 或 `Edges/Middle`。
- 用户实测后补充：空白模板不能删掉岩浆涨落机制；第五关起始墙壁石头可以删掉。已修正为 `Volcano_Segment` 不保留 `Start` 步骤，避免 `Edges/Start/RockFinal` 残留；同时保护 `Mechanics/RisingLava`。
- 二次复核反编译代码确认：第五关涨落机制不仅是 `RisingLava/Lava/Plane`，`MovingLava` 会调用 `rockAnim.Play("RockDoor")`，所以 `Mechanics/Rock_Round.010` 很可能是该机关动画引用。已停止删除 `Mechanics/Rock_Round.010`，避免可见岩浆还在但涨落机制失效。
- 用户反馈第三关独立生成物“卡皮巴拉”在空白模板中没删掉，随后明确 Snow 关卡也有。当前样本里已确认 Desert 第三关 `Map/Biome_3/Mesa/Desert_Segment/Platteau/Rocks/Oasis` 的温泉/卡皮巴拉对象材质包含 `M_Capybara`，层级样例为 `Oasis/Onsen/Capy/Capybara (...)`；Snow 的实机对象可能不是同一条离线样本路径。已把清理从 Desert-only 改成 `CustomBlank` 全段兜底：删除名称包含 `Capy/Capybara` 或当前 Renderer 材质为 `M_Capybara` 的对象。
- 最新 Release 构建通过并覆盖 r2modman terrain profile：`dotnet build ... DreamyAscent.csproj -c Release` 为 `0 warnings / 0 errors`。
- 新优先级：先实机复测 `CustomBlank` 是否只保留起始点过渡、并清掉桥/绳子/终点等结构；再做当前变体内置默认模板基线；随后接 `template-snapshots.json` 稳定匹配和回归；之后再扩展子区编辑、低风险放置和跨区段放置；父子/绑定物、spawner、Photon、外部物品和多人同步继续后置。

## 2026-05-16 配置回环验证通过

- `SubArea` / `PlacementRule` 导入回显已经实测通过：同一份导出 JSON 可重新导入，`placementConfigs=1` 正常回显。
- 新增、删除规则的回环也通过了，`subAreas` / `placementRules` 的空数组清空语义正常。
- 下一步转到 `template-snapshots.json` 的稳定模板匹配，不再停留在导入导出闭环。

## 2026-05-15 UI 收口与中文外置化

- 左下参数说明和焦点联动已稳定，`TrackFocusedProperty()` 与 `TrackFocusedPlacementField()` 现在会互相清空，旧的提示卡住问题已处理。
- 当前 UI 暂时收口，不再继续扩展布局或交互，后续只在出现回归时再改。
- 中文本地化改为外置优先：`Plugin.cs` 启动时调用 `DaLocalization.Initialize()`，先加载 `DreamyAscent Data\localization.zh-CN.json`，DLL 内字典只保留 fallback。
- 本地化和对象注册表路径都改成按插件目录拼接的相对路径，并增加了上一级目录兜底，适配 DLL 可能位于 `plugins\DreamyAscent\` 子目录的情况。
- 用户已实测确认：DLL 放在 `plugins\` 根目录和 `plugins\DreamyAscent\` 子目录两种部署方式都能正常找到外置 JSON。
- 最新 Release 构建通过，`dotnet build ... DreamyAscent.csproj -c Release` 为 `0 warnings / 0 errors`。

## 2026-05-14 今日收尾

- 今天任务结束，明天继续实机测试。当前停点是 DA UI 重构 + 配置层修复，不进入实际自定义 Instantiate 生成。
- 最新 DLL 已构建并输出到 r2modman terrain profile；最后一次 `dotnet build ... DreamyAscent.csproj -c Release` 为 0 warning / 0 error。离线样本脚本上一次已知回归仍为 `pass`，本轮 UI 改动没有重跑样本脚本。
- 明天先测试两件事：一是三层联动结构面板是否符合用户截图预期，切换关卡/区域/生成物时下方列表、右侧参数和场景高亮是否同步；二是用 `DreamyAscent Files\Beach-Segment__Jungle-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment_20260513_235007.json` 重新导入，确认日志 `placementConfigs=1`，Beach 页回显 2 个子区和 1 条规则。
- 若两项复测通过，再继续补 `SubArea` 编辑能力或接 `template-snapshots.json` 做稳定模板匹配；不要直接进入 Instantiate 生成。

## 2026-05-14 UI 参数说明与样本资产接入定位

- 用户反馈参数编辑过于抽象，尤其 `true/false` 需要手写，中文玩家看不懂。已开始把参数编辑器改成“中文名 + 原字段名”的显示方式；布尔值第一版改为“启用/关闭”按钮，不再要求手写 `true/false`。
- 左下辅助区域从空白占位转为信息面板：显示当前选中步骤的常见参数中文说明、离线样本资产状态和当前放置配置摘要。说明文字放左下角，右侧参数面板继续负责编辑，避免右侧再次变宽。
- 样本资产确认有用，但用途要分层：`object-registry-input.json` 已经是运行时可加载的只读模板注册表输入；`template-snapshots.json` 和 `sample-regression-report.json` 暂时是开发期资产，用于后续稳定 path/ID 匹配、子区推荐、变体污染回归和生成逻辑回归，不代表当前已经参与实际自定义生成。
- 后续做真实放置前，优先把 `template-snapshots.json` 的 segment/grouper/step path 与运行时扫描结果做稳定匹配；再用 `sample-regression-report.json` 作为每次改导出/模板匹配后的回归检查，保持 `status=pass`。
- 根据用户截图建议，结构面板已从旧树形缩进改为三层联动：最上面横排显示要修改的关卡，下一行横排显示当前关卡的区域/生成组，下面只显示当前区域的生成物/步骤；切换关卡会重置区域和生成物，切换区域会实时刷新下方生成物，点击生成物会同步右侧参数和场景高亮。
- 生成物行会显示当前 step 类型，以及运行时 catalog 中匹配到的 item/material 数量，用于先判断某个区域到底扫到了哪些生成物，避免继续在不联动的树里猜重叠关系。当前仍是 UI/诊断联动，不执行自定义 Instantiate。
- Release 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，0 warnings，0 errors，DLL 已输出到 r2modman terrain profile。

## 2026-05-13 配置导入回显修复与 UI 调整

- 用户实机验证发现：从推荐候选添加规则、改数量/缩放并应用可用，但导出后重新导入 UI 不显示上次添加的规则。
- 诊断结论：最新导出文件 `DreamyAscent Files\Beach-Segment__Jungle-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment_20260513_235007.json` 中 `Beach_Segment` 已有 `subAreas=2`、`placementRules=1`，说明导出保存正常；`RuntimeExport.json` 没这些字段是正常的运行时诊断边界；日志 `placementConfigs=0` 说明问题在导入识别。
- 根因：Json.NET 反序列化会填充已有 `List`，未必触发 `SubAreas` / `PlacementRules` setter，导致 `HasPlacementConfigSpecified` 没置位。现已给两个字段加 `ObjectCreationHandling.Replace`，并在导入端用非空 `subAreas` / `placementRules` 兜底识别。
- 兼容修复：用户这次把规则目标子区填成了显示名 `111`，不是内部 ID。新导入逻辑会先按 ID 匹配，失败后按 `displayName` 匹配，再兜底到第一个子区，避免旧导出作废。
- UI 调整：Catalog 页改为折叠区块；放置配置和推荐候选默认展开，运行时 `CatalogItems` / `CatalogMaterials` 默认折叠；规则目标子区不再手填 ID，改为 `<` / `>` 按钮在子区间切换。
- Release 构建通过并输出到 r2modman terrain profile，`DreamyAscent.dll` 时间为 2026-05-14 00:06:55；离线样本脚本复跑 `regression=pass`。

## 2026-05-13 SubArea / PlacementRule 配置层初版

- `DaSegmentData` 已新增 `subAreas` 与 `placementRules` JSON 数据层，当前只保存配置，不实例化、不运行自定义生成，也不污染 live scene。
- `DaPlacementRuleData` 第一版保存：规则 ID、显示名、启用状态、`registryId`、目标 `targetSubAreaId`、数量、最小/最大缩放、`placementMode`、`rotationMode`、`ownershipMode` 和本地偏移。
- `DaRuntimeEditService.ApplyImportedData` 已支持按 `SegmentName` 导入放置配置。旧 JSON 没有 `subAreas` / `placementRules` 字段时不会清空当前配置；新 JSON 带空数组时可显式清空。
- “区段模板库”页新增放置配置区：可添加默认子区，可从 `DaObjectRegistryService.GetRecommendedTemplatesForSegment()` 的首批推荐候选创建规则，可编辑规则名、目标子区、数量和缩放，可删除子区/规则。
- 当前 UI 明确是配置入口，不是生成入口。下一步先进游戏验证：添加规则 -> 导出 JSON -> 重新导入应用，确认 JSON 持久化和 UI 回显正确；仍不要直接做 `Instantiate` 生成。
- Release 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，0 warnings，0 errors；离线样本产物脚本复跑 `regression=pass`。

## 2026-05-13 样本产物工具链初版

- 新增 `MOD开发/DreamyAscent/data/tools/build_map_data_artifacts.py`，从 `data/map-data/sample-index.json` 和诊断四件套可重复生成后续开发输入。
- 新增 `data/map-data/generated/` 三个核心产物：`template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`，并补 `generated/README.md` 说明用途和重跑命令。
- 当前生成统计：130 个 Segment snapshot、520 个 grouper、4001 个 step；`object-registry-input.json` 合并 193 个模板候选、25 个材质候选；`sample-regression-report.json` 为 `pass`，issue 0、warning 0。
- 对象注册表候选已区分 `technicalLowRiskPlacementCandidate` 和 `recommendedFirstPassCandidate`。第一版推荐候选 20 个，已用名称/组件启发式排除桥、岩浆、行李、龙卷风、炸药、蝎子等结构/机制对象。
- 下一步接源码时不要再手工扫 JSON；优先把 `object-registry-input.json` 的 registry ID、风险标签、sourceExamples 接到 `DaObjectRegistry` / UI，再用 `template-snapshots.json` 做稳定 path 匹配和回归对比。

## 2026-05-13 DaObjectRegistry 只读骨架接入

- 新增 `Data/DaObjectRegistryData.cs` 和 `Services/DaObjectRegistryService.cs`，启动时从插件目录 `DreamyAscent Data/object-registry-input.json` 可选加载离线对象注册表；缺文件只警告，不影响现有运行时 catalog。
- `DreamyAscent.csproj` 已把 `data/map-data/generated/object-registry-input.json` 作为 `None` 复制到输出目录 `DreamyAscent Data/object-registry-input.json`，Release 构建后已确认文件在 r2modman terrain profile 中可解析。
- UI 的“区段模板库”页新增只读全局注册表摘要：显示模板数、材质数、技术低风险候选数、首批推荐候选数，并按当前 Segment 显示 `recommendedFirstPassCandidate` 候选和来源 path。当前没有生成按钮，不会改变地图。
- Release 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，0 warnings，0 errors。
- 该骨架之后已经进入 `SubArea` / `PlacementRule` 配置层；仍不要直接给 registry 候选加“生成”按钮，下一步先验证配置保存/导出/导入。

## 2026-05-13 样本审计与 memory 补全

- 按用户要求重新从多个角度审计 `MOD开发/DreamyAscent/data/map-data`。当前 `1.62.a/` 官方自然样本包含 22 个 `DreamyAscent Files` JSON、19 个完整诊断目录；`TerrainRandomiser/` 包含 29 个 JSON、7 个完整诊断目录。所有诊断目录均包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`。
- 新增项目内审计报告：`MOD开发/DreamyAscent/data/map-data/SAMPLE_AUDIT_2026-05-13.md`。该文件集中记录样本来源、诊断完整性、全量变体覆盖、关键 TerrainRandomiser 样本、纯净性判断、代表性 catalog/object-reference 统计和结论。
- 新增机器可读样本索引：`MOD开发/DreamyAscent/data/map-data/sample-index.json`。记录来源计数、诊断完整性、`0 grouper`/未知 variant 检查结果、各 segment 变体覆盖计数和纯净性判定规则，后续工具优先读这个文件。
- 覆盖结论已收口：按当前源码 `s_variantRules`，`Beach / Jungle / Roots / Snow / Desert` 的全部已知变体均已有样本覆盖。`Caldera` 与 `Volcano` 继续按 `DirectSegmentRoot` 处理。
- 官方自然样本已覆盖大部分变体；TerrainRandomiser 只用于补齐官方自然跑图难遇到的缺口，包括 `Beach/JellyHell`、`Jungle/Thorny`、`Jungle/SkyJungle`、`Roots/Cave Mania`、`Roots/Deep Water`、`Roots/Bomb Beetle`、`Roots/Clearcut`、`Desert/ScorpionsHell`、`Desert/CacusHell`、`Desert/TumblerHell`。
- 纯净性审计结论：`Roots` 的 TR 补齐样本中，`Cave Mania / Deep Water / Bomb Beetle / Clearcut` 均只命中对应 active variant path；`Jungle/SkyJungle` 和 `Desert/*Hell` 样本中出现少量共享 step 名属于官方当前激活层级内的共用步骤/资源命名，不单独作为污染证据。
- 代表性统计已记录：官方 Roots/Desert 组合 `20 groupers / 165 steps / 161 catalog items / 1160 object references`；官方 Jungle/Snow 组合 `20 groupers / 144 steps / 126 catalog items / 871 object references`；TR Roots/Snow 组合 `23 groupers / 158 steps / 140 catalog items / 937 object references`；TR Jungle/Desert 组合 `18 groupers / 162 steps / 148 catalog items / 1125 object references`。
- 已补长期 memory：`DECISIONS.md` 增加变体污染判定规则和 TerrainRandomiser 样本定位；`FILES.md` 增加 `data/map-data`、样本审计和覆盖表路径；`README.md` 增加当前样本基线状态；`MAP_GENERATION.md` 更新资源可信度、当前能力、诊断事实和“必须补的数据”状态。
- 下一步方向从“继续刷图”切换为“利用样本”：优先从 `RuntimeExport/ObjectCatalog/ObjectReferenceMap` 提取稳定模板快照、稳定 path、对象注册表候选和后续变体回归对照。

## 2026-05-13 诊断导出纯净性复测通过

- 用户在 `2026-05-13 20:22` 和 `20:24` 用新 DLL 重新导出两组诊断：`Beach+Roots+Desert+Caldera+Volcano` 与 `Beach+Jungle+Snow+Caldera+Volcano`。本轮 DLL 时间戳为 `2026-05-13 20:19:59`。
- 本轮确认两类关键回归已修复：一是 `ObjectReferenceMap.json` 成功落盘，不再报路径过长；二是此前因 `activeInHierarchy` 误伤导致的非当前关卡 `0 grouper` 问题已消失。
- 最新日志统计：Roots 6 groupers / 47 steps，Desert 5 / 54，Jungle 4 / 37，Snow 7 / 42，Caldera 3 / 18，Volcano 2 / 10；说明导出现在能覆盖未走到但已加载的后续区段。
- 纯净性判断：`Jungle_Segment` 导出路径全部落在 `.../Pillars/...`，`Snow_Segment` 全部落在 `.../Default/...`；`Desert_Segment` 识别到 active `VariantObject` 为 `CactusForest`，`Roots Segment` 识别到 active 路径为 `- Redwoods Default Variant`。当前样本没有发现未激活变体整支泄漏进导出的证据。
- 注意：Desert 的当前样本里仍能看到名为 `ScorpionsHell`、`TumblerHell` 的 step，但它们出现在当前激活层级内，且不是额外激活的 `VariantObject` 节点；现阶段应视为原版资源命名/共用 step，而不是导出污染。
- 剩余验证点只在覆盖面，不在主逻辑：后续若要把 `VariantObject` 变体命名完全坐实，优先继续补 `Roots` 非默认变体（如 `Cave Mania` / `Deep Water` / `Bomb Beetle`）和更多 `Desert` 非 `CactusForest` 样本。

## 2026-05-13 变体覆盖面继续扩展

- 用户随后继续批量跑样本，到 `20:49` 为止的导出统计显示：Beach 已覆盖 `Default / SnakeBeach / RedBeach / BlueBeach / BlackSand`；Jungle 已覆盖 `Default / Pillars / Bombs / Ivy / Lava`；Snow 已覆盖 `Default / Spiky / GeyserHell / Lava`；Desert 已覆盖 `NoVariant / CactusForest / DynamiteHell / TornadoHell`；Roots 已覆盖 `Default / Deep Woods`。
- 最新 Jungle 识别已确认不止 `Pillars`：`20260513_204925` 识别到 `Ivy`，`20260513_204801` 与 `20260513_204250` 识别到 `Bombs`，旧样本里也已有 `Lava`。Snow 也已识别到 `Spiky`、`GeyserHell`、`Lava`，说明 `BiomeVariant` 路径的归一化逻辑已稳定。
- Desert 的 `VariantObject` 识别已确认不止 `CactusForest`：`20260513_203544` / `203746` 识别到 `TornadoHell`，active path 为 `Map/Biome_3/Mesa/Desert_Segment/Misc/TornadoHell`；`20260513_203827` / `204132` 识别到 `DynamiteHell`，active path 为 `Map/Biome_3/Mesa/Desert_Segment/Wall/Props/DynamiteHell`；`NoVariant` 也能稳定识别为 `Map/Biome_3/Mesa/Desert_Segment/Platteau/Props/NoVariant`。
- Desert 各样本中始终都能看到 step `ScorpionsHell` 与 `TumblerHell`，包括 `NoVariant`、`CactusForest`、`DynamiteHell`、`TornadoHell`。它们是当前激活层级里的共用 step，不应再作为“是否混入错误变体”的判断依据。
- Roots 本轮新增确认 `Deep Woods`：`20260513_204438` 识别到 active path `Map/Biome_2/Roots/Roots Segment/PlateauProps/- redwoods deep woods Variant`。但目前已跑样本里还没有抓到 `Cave Mania`、`Deep Water`、`Bomb Beetle`、`Clearcut`。
- 现阶段结论：导出主逻辑已稳定，剩余任务主要是补齐 Roots 尚未出现的 4 个 `VariantObject` 变体样本；Desert 已经足够证明识别链路可用。

## 2026-05-13 TerrainRandomiser 样本补齐全量覆盖

- 用户把 `TerrainRandomiser` 验证样本单独归档到 `MOD开发/DreamyAscent/data/map-data/TerrainRandomiser/`，并补齐此前官方自然跑图里很难稳定遇到的缺失变体。
- `Roots` 现已通过 TerrainRandomiser 补齐全部 6 个变体，关键样本为：
  - `20260513_210422` -> `Cave Mania`，active path `Map/Biome_2/Roots/Roots Segment/PlateauProps/- Cave Mania Variant`
  - `20260513_210542` -> `Deep Water`，active path `Map/Biome_2/Roots/Roots Segment/PlateauProps/- Deep Water variant`
  - `20260513_210643` -> `Bomb Beetle`，active path `Map/Biome_2/Roots/Roots Segment/PlateauProps/- Bomb Beetle Variant`
  - `20260513_210824` -> `Clearcut`，active path `Map/Biome_2/Roots/Roots Segment/PlateauProps/- Redwood Clearcut Variant`
- `Jungle` 也已通过 TerrainRandomiser 补齐剩余 2 个变体：
  - `20260513_211911` -> `Thorny`，active path `Map/Biome_2/Tropics/Jungle_Segment/Thorny`
  - `20260513_212020` / `20260513_212118` -> `SkyJungle`，active path `Map/Biome_2/Tropics/Jungle_Segment/SkyJungle`
- `Desert` 也已通过 TerrainRandomiser 补齐剩余 3 个变体：
  - `20260513_211911` -> `ScorpionsHell`，active path `Map/Biome_3/Mesa/Desert_Segment/Wall/Props/ScorpionsHell`
  - `20260513_212020` -> `CacusHell`，active path `Map/Biome_3/Mesa/Desert_Segment/Wall/Props/CacusHell`
  - `20260513_212118` -> `TumblerHell`，active path `Map/Biome_3/Mesa/Desert_Segment/Platteau/Props/TumblerHell`
- 至此，按 DreamyAscent 当前源码里的变体规则，`Beach / Jungle / Roots / Snow / Desert` 五类区段的全部已知变体都已有样本覆盖。`Caldera` 与 `Volcano` 仍按 `DirectSegmentRoot` 处理，不在这套变体验证范围内。
- 结论可以收口：DreamyAscent 当前版本已经具备按当前激活分支导出各类已知变体的能力；`TerrainRandomiser` 样本主要用于补覆盖面，不改变这一结论。

## 2026-05-13 PEAK 1.62.a compatibility check

- 用户更新 PEAK 1.62.a 反编译后复核 DreamyAscent。`Assembly-CSharp` 与 1.61.b 反编译源码哈希一致，地图生成关键类未变化：`LevelGenStep`、`PropGrouper`、`PropSpawner`、`BiomeVariant`、`DesertRockSpawner`、`DecorSpawner`、`SingleItemSpawner`、`PSM_ChildSpawners`、`PSM_SingleItemSpawner`。
- Release 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，0 warnings，0 errors。暂不需要因为 1.62.a 改业务代码；地图生成长期 TODO 仍按既有计划推进。

## 2026-05-13 改名脏状态收口

- 用户要求修复此前提到的 DreamyAscent / memory 脏数据或脏代码，避免后续冲突。本轮确认问题不是业务代码新增 bug，而是 Git 索引仍把旧 `TerrainCustomiserCN` 视为删除、把新 `DreamyAscent` 视为未跟踪，memory 目录也有同类旧目录删除 + 新目录未跟踪状态。
- 已先执行 `git restore --staged -- .` 清掉错误 staged 删除，只修索引不改文件内容；随后仅对 `MOD开发\TerrainCustomiserCN`、`MOD开发\DreamyAscent` 和 `memory` 执行 `git add -A`，让 Git 把改名和 memory 迁移识别为同一批变更。
- 当前 DreamyAscent 相关状态已从“旧目录 D + 新目录 ??”收敛为 staged rename / add：`Tc*` 文件到 `Da*` 文件的改名、多数源码改名、`memory/mods/TerrainCustomiserCN` 到 `memory/mods/DreamyAscent` 的迁移和新增地图生成研究文件均已纳入索引。
- 仍有两个无关脏项没有处理：`.gitignore` 修改，以及 `MOD开发/PlayersInfo/合并输出.txt` 删除。它们不属于 DreamyAscent / memory 改名冲突，本轮没有回滚，避免覆盖用户或历史变更。
- 本轮没有修改 DreamyAscent 业务代码，没有重新构建 DLL；只整理 Git 索引和 memory 收口记录。

## 2026-05-12 地图生成研究再次补强

- 根据用户反馈，上一版记录仍偏概念化，尤其缺少“别的区段的物品如何放到当前区段”的完整流程、正反例、实际数据 ID 和依据链。已新增 `IMPLEMENTATION_MATRIX.md`，把官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照、多人同步拆成十个工作面。
- 已继续补强 `CROSS_SEGMENT_PLACEMENT.md`：新增错误实现/正确实现对照、可执行 `PlacementRule` JSON 例子、生成后诊断要求和兼容矩阵。第一条明确规则是 `item:jungle-segment:step-prop-prefab:dcb9dd57` 放到 `Desert_Segment / PlateauTop`，数量 3-10，使用目标子区 ray/layer，不继承来源 transform 或数量。
- 已把 `MAP_GENERATION.md` 改为“需求拆分 + 分层实现路线”，并增加父子/绑定物、外部 Unity 物品、材质替换的独立章节；`MAP_GENERATION_RESEARCH_NOTES.md` 新增需求逐项验算和测试用例倒推两个角度。
- 新增跨区段放置核心结论：必须使用“来源模板 + 目标子区”模型。来源模板提供 prefab/默认参数/组件/风险，目标 `SubArea` 提供 XYZ、范围、ray、layer 和落地约束；不能直接把来源区段的 `PropGrouper/LevelGenStep` 拿到目标区段运行。
- 新增可执行例子：Jungle `Jungle_PalmTree_Thick/Crook/Thin` 是第一批低风险候选，可放到 Desert 平台/山腰子区验证；Desert `Cactus Ball Big` 可作为普通仙人掌候选；`Cactus Ball Base`、Roots `Redwood`、行李、藤蔓、虫类、岩浆机制因父子/Photon/机制风险先只读标记。
- 依据已写清：原版 `PropSpawner.GetRandomPoint()` 决定目标落点，`Spawn()` 使用 `props` 实例化；HazardSpam 拆分 `PropPrefabs` 和 `(Zone, SubZoneArea)` 的 `PropSpawners`，并由主机广播 positions/rotations；TerrainRandomiser 提供 room property 与 PhotonView ID 分发参考。
- TODO 已补齐到实现矩阵对应工作面：`SubArea`、`DaObjectRegistry`、低风险 `PlacementRule`、跨区段兼容矩阵、外部资源格式、材质覆盖、多人同步和中途加入。

## 2026-05-12 地图生成资源通读初版

- 按用户要求通读并记录 DreamyAscent 源码、当前 memory、诊断 JSON、PEAK 1.61.b 地图生成反编译、TerrainCustomiser、TerrainRandomiser、HazardSpam 源码和 NetGameState 参考代码；过程记录在 `MAP_GENERATION_RESEARCH_NOTES.md`，稳定结论整理到 `MAP_GENERATION.md`。
- 初版结论：DreamyAscent 当前主模型仍是 `Segment -> Grouper -> Step -> 标量属性/约束`；`ObjectCatalog` 是只读/诊断索引，不是可执行资源注册表；官方模板、空白自定义、模板库/外部资源/材质/多人同步必须拆成不同数据路径。
- 诊断复核：最新有效目录含 5 个地图组合；Roots/Desert 组合 5 segments、20 groupers、165 steps；Jungle/Snow 组合 5 segments、20 groupers、142 steps；Desert 有重复 `Props/Rocks`，后续必须使用层级 path 或稳定 ID。
- `CustomBlankRemaining` 复核：Desert 最新只剩 `GroundMesh`；Caldera/Volcano 主要剩岩浆机制对象；Jungle/Beach/Snow 多轮为 0；Roots 剩余主要是地形、水体、墙体 splitmesh、粒子，不能默认作为普通装饰清理。
- 故障反推已写入 memory：Desert 空心来自官方生成前预清理破坏 `DesertRockSpawner` 依赖；Roots/Jungle 乱石来自变体/岩石组重复生成；`CustomBlank -> OfficialTemplate` 不能承诺自动恢复。后续应做干净模板快照、稳定 path、SubArea/PlacementRule/ObjectRegistry，而不是继续逐个路径补清理特判。

## 2026-05-12 高亮与空白模式修正

- 用户反馈生成器范围高亮仍看不清。已将选中 Step 的高亮增加为半透明实心矩形面，并保留青色外框/中心标记；已修改但未选中的 Step 仍使用橙色线框，避免画面过满。
- 用户反馈 Desert 和 Caldera 在 `CustomBlank` 后变成空心。判断原因是空白模式把承载地形骨架的生成组也清掉了；已新增结构组保留规则：`Desert_Segment` 保留 `Platteau` / `Rocks`，`Caldera_Segment` 保留 `LavaRivers` / `Rocks`，其他普通装饰组继续清理。
- 用户进一步明确 Caldera “除了岩浆，其他都撤”。已撤销 Caldera 的 `LavaRivers` / `Rocks` 结构保留白名单；Caldera `CustomBlank` 会清理官方生成组，只保留非生成器体系里的岩浆机制对象。Desert 仍暂时保留 `Platteau` / `Rocks` 防止空心。
- 修正 `CustomBlank` 点击“生成本段”后 UI 看起来没反应的问题：之前 `RunSegment` 清理成功也返回 0，导致状态显示为 0；现在返回实际清理数量。日志确认按钮此前实际已经触发。
- 用户反馈 Desert 官方模板生成后仍像空心。日志确认 `生成本段` 时 `Platteau` 和第一个 `Rocks` 报 `DesertRockSpawner.Clear` NRE；原因判断为 DreamyAscent 在调用原版 `PropGrouper.RunAll(true)` 前先手动清理子物，破坏了 `DesertRockSpawner.Clear()` 依赖的对象。已移除官方模板生成前的预清理，空白模式仍走自定义清理。
- 用户确认 Desert 官方模板已正常，但空白自定义仍不对。日志和诊断显示空白模式保留整组 `Platteau` / `Rocks` 太粗，导致大量官方岩石/峡谷子生成物残留。已把 Desert `CustomBlank` 保留规则收窄为只保留 `Platteau`，不再保留 `Rocks`；若仍过多，下一步继续细分 `Platteau` 内部的 Canyon/Big 等子项。
- 用户反馈“有点干净，但又不太干净”。诊断统计显示 Desert `CustomBlank` 剩余主要挂在 `Platteau/Canyon` 下：`Small` 800、`Big` 400、`Pillar` 200、`Start` 150。已新增保留 `Platteau` 后的内部二次清理：清掉嵌套 `Canyon` grouper 生成物，保留 `Platteau` 底板骨架。
- 用户继续询问是否还有需要清理。最新诊断显示 Desert `CustomBlank` 剩余降到 151 个候选：1 个 `GroundMesh`，其余 150 个都在 `Platteau/Start` 下的 `RockFinal_Desert*` 和 LOD。已继续清理 `Platteau/Start` 子物，预期只保留 `GroundMesh` 这类底板本体。
- 用户询问雨林和 Roots 生成时出现很多不该存在的石头。日志显示 Roots 没识别到 active `BiomeVariant`，fallback 到 whole segment 后导出了多个互斥变体：`- Bomb Beetle Variant`、`- Cave Mania Variant`、`- Deep Water variant`、`- Redwood Clearcut Variant`，以及 `PlateauRocks` / `WallRocks` 等，导致变体叠加和乱石。已修正 fallback 扫描逻辑：只导出 `activeInHierarchy` 的 `PropGrouper`，并在日志记录 `activeGroupers/totalGroupers`。
- 用户反馈新修法导致 Roots/Jungle 扫不到。日志显示 active grouper 计数为 0，说明 `activeInHierarchy` 硬过滤过严，也会误伤直接根段。已撤掉全局 active 过滤，改为只排除 inactive 且名称匹配 `- xxx Variant` 的变体分支；fallback 日志改为 `variant-branch filtering`。
- 用户再次反馈 Roots 仍有不该存在的石头。最新日志确认变体组已不再导出，Roots 只剩 6 个默认组；乱石来自默认 `PlateauRocks` 和 `WallRocks`。已为 Roots 官方整段生成增加窄规则：跳过 `PlateauRocks` / `WallRocks`，并写日志 `Skipped grouper for official segment run`。
- 用户反馈 Roots 中点击空白生成正常，但切回官方后只剩蘑菇。日志显示切回官方后 `Redwood` 生成失败 NRE，且此前跳过 `PlateauRocks` / `WallRocks` 让官方结构恢复不完整。已撤销 Roots 跳过岩石组的规则；从 `CustomBlank` 切回 `OfficialTemplate` 时自动执行 `Rescan()`，避免用空白清理后的旧运行时引用继续生成。
- 用户确认：不跳过 `PlateauRocks` / `WallRocks` 会让 Roots 官方生成异常乱石，跳过则空白后切回官方无法完整恢复。最终决策改为恢复 Roots 官方整段生成跳过 `PlateauRocks` / `WallRocks`；`CustomBlank -> OfficialTemplate` 视为运行时不可完整恢复路径，不再自动 Rescan，只在 UI 和日志提示“建议重开或新图复测”。
- 用户新反馈：打开雨林不显示；Roots 绕过 `PlateauRocks` / `WallRocks` 后每次生成变化可能偏少。最新日志实际地图组合为 `Beach_Segment / Roots Segment / Desert_Segment / Caldera_Segment / Volcano_Segment`，没有 `Jungle_Segment`，因此本轮无法用日志确认雨林问题，需要进到含 Jungle 的地图后再看。
- 最新日志确认 Roots 点击“生成本段”时所有非跳过 grouper 都报 `Cannot generate grouper because runtime grouper reference is missing`，最终 `groupers=0`；这说明当前问题不只是 Rocks 跳过，而是运行时数据里的 Unity 引用丢失或已被空白清理破坏。已新增生成前运行时引用重绑：按当前场景的 segment/grouper/step 名称重新绑定 `SourceSegment`、`SourceRoots`、`PropGrouper`、`LevelGenStep` 和 constraint 引用；“生成本段”“生成本组”和参数修改后的自动生成都会先尝试重绑。
- Roots 官方整段生成仍暂时跳过 `PlateauRocks` / `WallRocks`，因为用户已确认不跳过会出现异常乱石。变化少的问题需在引用重绑修复后重新判断；如果仍单调，再细分定位石头来源到具体 step，避免直接恢复整组 Rocks。
- 用户截图确认 Jungle 也有同类问题：Jungle 并非扫描不到，日志显示 `Jungle_Segment` 已导出 4 组：`Pops_Plat`、`Props_Wall`、`Rocks_Plat`、`Rocks_Wall`。点击“生成本段”后四组都执行，截图中的大量石头来自 `Rocks_Plat` / `Rocks_Wall` 在已有地图上重复生成。已为 Jungle 官方整段生成新增保护：跳过 `Rocks_Plat` / `Rocks_Wall`，保留植物/道具组 `Pops_Plat` / `Props_Wall`。
- 2026-05-12 Release 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，0 warnings，0 errors。下一轮实机重点看高亮是否明显、Desert/Caldera 是否不再空心、普通漂浮物是否仍减少。
- 2026-05-12 再次 Release 构建通过：运行时引用重绑修复后输出到 r2modman terrain profile，0 warnings，0 errors。
- 2026-05-12 再次 Release 构建通过：Jungle 官方整段生成跳过 `Rocks_Plat` / `Rocks_Wall` 后输出到 r2modman terrain profile，0 warnings，0 errors。

## 2026-05-12 项目改名

- 改名前用户已经完成一轮功能基线测试并发送截图，作为后续改名回归对照。该基线关注 F1 面板、地图预览、区段模板库、生成器范围高亮、空白模式和统一导入/导出目录等已有功能，不代表改名后已经复测完毕。
- 用户补充说明：此前截图中 Caldera/Volcano 的 `CustomBlank` 测试属于改名前功能测试。最新对应诊断显示 `CustomBlankRemaining_Caldera_Segment.json` 只剩 `River` 和 `River/Coll`，`ash` / `Bubbles` 已不在剩余列表；`CustomBlankRemaining_Volcano_Segment.json` 只剩 `Mechanics/RisingLava/Lava/Coll` 和 `Plane`，属于上升岩浆机制。
- 用户要求将 `TerrainCustomiserCN` 改为 `DreamyAscent`。已完成源码目录、项目文件、命名空间、程序集名、BepInEx 插件 ID、窗口标题、日志前缀、诊断目录、导入导出目录、预览位姿文件名和 memory 入口的改名。
- 用户补充要求旧 `Tc*` 代码前缀也要改掉。已将源码类名、文件名和项目编译项统一改为 `Da*`，例如 `DaRuntimeEditService`、`DaMapPreview`、`DaTerrainData`、`DaLog`；源码中不再保留 `Tc[A-Z]` 前缀。
- 新构建命令为 `dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\DreamyAscent\DreamyAscent.csproj" -c Release`，输出为 r2modman terrain profile 下的 `BepInEx\plugins\DreamyAscent.dll`。
- 测试 profile 中旧的 `TerrainCustomiserCN.dll` / `.pdb` 和误名 `DreamAscent.dll` / `.pdb` 已删除，避免新旧插件同时加载。旧 `TerrainCustomiserCN Files` / `Exports` / `Imports` / `PreviewPoses.json` 保留为迁移来源，首次启动会复制到新的 `DreamyAscent` 文件名。

## 2026-05-11 空白自定义复测与残留对象确认

- 最新日志确认 `CustomBlank` 第一版已经跑通：`Caldera_Segment` 与 `Volcano_Segment` 切到空白后均未再执行官方生成器，`RuntimeEditService` 成功写出 `CustomBlankRemaining_*.json`，日志里没有新的 DreamyAscent 致命异常。
- `Caldera_Segment` 的剩余候选只剩 4 个：`ash`、`Bubbles`、`River`、`Coll`。其中 `River`/`Coll` 带 `Lava`、`LavaTides`、`PhotonView` 和碰撞体，属于要保留的岩浆/水流机制；`ash`/`Bubbles` 是粒子表现。
- `Volcano_Segment` 的剩余候选只剩 2 个：`Coll`、`Plane`，都挂在 `Mechanics/RisingLava/Lava` 下，属于上升岩浆机制，不是普通装饰残留。
- 这轮复测同时确认统一目录 `DreamyAscent Files`、导入栏布局、`editMode` 继承/导入应用都已在实机跑通，后续不用再把这些项保留在根 TODO 里。

## 2026-05-11 HazardSpam 与反编译参考结论

- 已完成区段编辑模式的最小数据/UI 闭环：`DaSegmentData` 新增 `editMode`，支持 `OfficialTemplate`、`CustomBlank`、`Hybrid` 三种枚举并以字符串写入 JSON；旧数据默认 `OfficialTemplate`。
- `DaCustomiserWindow` 在区段详情头部新增三段模式选择和中文提示；当前只保存/导出模式，不会立刻清空或跳过官方生成器，避免 `CustomBlank` 方案未验证前破坏原版生成结构。
- 已补区段编辑模式持久化闭环：`DaTerrainExportService` 重新扫描同一 `mapKey` 时会按 `segmentName` 继承上一份运行时数据的 `editMode`，避免用户点重新扫描后模式全部回到 `OfficialTemplate`。
- 已补导入应用同步：`DaRuntimeEditService.ApplyImportedData` 会按 `segmentName` 把导入 JSON 的 `editMode` 应用到当前区段，并在日志中记录 `segmentModes` 数量。
- 根据用户反馈“导入/导出目录不应该拆开”，将地形配置文件目录统一为 `DreamyAscent Files`；`ExportDirectoryPath` 和 `ImportDirectoryPath` 现在指向同一目录，`选最新导入` 和 `导出当前` 使用同一个文件池。
- 为兼容旧测试文件，初始化时会把旧 `DreamyAscent Exports` / `DreamyAscent Imports` 下的 JSON 复制到 `DreamyAscent Files`，不删除旧目录。
- 根据用户截图修正导入栏布局：路径输入框原本把整行宽度吃满，导致“浏览/选最新导入/导入并应用”按钮被挤到窗口右侧外面；现在路径单独一行，按钮单独一行。
- Windows Forms 文件选择框在游戏环境中实测不可靠：点击后没有成功/失败日志，用户仍反馈“浏览没反应”。已撤掉 `OpenFileDialog` 和 `System.Windows.Forms` 依赖，改为“打开目录”按钮，直接打开统一的 `DreamyAscent Files` 文件夹；导入仍通过“选最新导入”或路径输入框完成。
- 用户截图确认统一目录/导入栏已正常，但地图中仍可见漂浮物；漂浮物作为后续修复项记录，重点排查生成器清理后残留、父子物/装饰物脱离父级、约束/落地规则失效或高处生成物未被清理。
- `CustomBlank` 第一版生效策略已接入：当 Segment 处于 `CustomBlank` 且点击“生成本段”时，只清理该区段各官方 `PropGrouper` / `LevelGenStep` 下已生成的子物体，不重新执行官方生成器；地形、生成器结构和后续自定义规则入口保留。
- “生成本组”当前仍按官方单组生成，用于测试/回退；是否在 `CustomBlank` 下禁用生成本组，后续根据实机反馈决定。
- 2026-05-11 构建通过：`dotnet build ... DreamyAscent.csproj -c Release` 输出到 r2modman terrain profile，0 warnings，0 errors。
- 按用户要求复核 `引用参考代码\github\peak-hazard-spam-main` 源码和 `引用参考代码\反编译`：HazardSpam 可作为 DreamyAscent 后期地图生成交互参考，尤其是 Zone/SubZone、模板注册、主机生成位置并广播给客机的分层方式。
- 用户补充后期有两条方向：第一条是各区段参考原版游戏官方模板，在官方生成器基础上改参数；第二条是各区段默认为空，由用户自己添加想要的物品来创造地图。跨区段选择物品只是第一类/模板复用能力的一部分，不代表完整目标。
- 设计记录：后期数据模型应支持 `OfficialTemplate`、`CustomBlank`，以及可选 `Hybrid`。`CustomBlank` 不能只靠现有 `LevelGenStep` 编辑，因为空白区段没有官方 Step 可承载新增物品，必须新增自定义生成规则/手动放置规则层。
- HazardSpam 的 `HazardTemplateManager` 将 `PropPrefabs` 按物品类型保存，将 `PropSpawners` 按 `(Zone, SubZoneArea)` 保存；`HazardManager.CreateSpawner` 复制目标区域 spawner 的 `area/ray/raycast/layer/constraints` 等参数，再放入选中 prefab。这说明“模板来源”和“目标放置区域”应分离。
- HazardSpam 的 UI 描述层用 `ZoneDescriptor` / `SubZoneArea` 区分区域和子区域，且多个 Zone 的可选 HazardType 白名单基本可共享；这支持后期 DreamyAscent 做跨区段混合模板，但需要通过目标子区兼容规则过滤，而不是直接把所有 Segment 模板合成无上下文列表。
- 反编译原版 `PropSpawner` 确认生成核心是 `area`、raycast、`nrOfSpawns`、`props`、`constraints`、`modifiers`、`postConstraints`；反编译 TerrainCustomiser/TerrainRandomiser 确认当前 `Segment -> Grouper -> LevelGenStep` 编辑路径合理，但 prefab/material 引用和新增物品仍需要独立对象库/资源注册表。
- 新增设计结论：后期“区段模板库”应演进为“全局模板注册表 + 当前区段过滤 + 目标放置子区兼容规则”。允许跨区段混合模板，模板的 Segment 只表示来源和默认参数，不能作为唯一可放置范围。
- 新增 UI 结论：当前只读阶段参数页和区段模板库同窗可接受；一旦支持添加、右键、拖拽、搜索和材质选择，应把模板库拆成独立窗口或固定侧栏。主参数面板只编辑当前 Step/子区/生成规则。
- 新增同步结论：后期手动放置物/子区生成物应参考 HazardSpam 和 TerrainRandomiser，由主机决定模板、目标子区、positions/rotations 和必要 PhotonView ID，再广播给客机复现；不能只让客机本地各自随机生成。

## 2026-05-11 TODO 清理

- 清理根 `TODO.md`：预览雾状/白雾遮挡从 P0 移出。依据是 2026-05-10 用户多张实机截图中主预览已清晰显示多个地图，没有再出现白雾遮挡；后续只作为普通回归风险观察。
- 清理根 `TODO.md`：F1 输入稳定性从 P0 移出。当前已停用 Canvas 隐藏，主相机预览会避开左侧面板输入，用户连续截图测试未再反馈 ESC/鼠标异常。
- 修正“清理废弃预览代码”待办表述：`_screenPreviewActive` 现在是有效状态位，`mainCamera.rect` 只是保存/恢复主相机原状态，不是旧 `Camera.rect` 分屏方案；后续清理不能误删这些当前路径。

## 2026-05-10 后期物品编辑需求记录与可行性初评

- 用户实机写诊断成功：最新 `ObjectReferenceMap.json` 位于 `Beach-Segment__Roots-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment`，时间 2026-05-10 20:07:41，大小约 3.9 MB。
- 新增第一版对象库诊断：`DaObjectCatalogDiagnosticService` 会在写诊断时额外输出 `ObjectCatalog.json`，按 Segment 汇总 item/material 候选。它只读运行时对象，不改变地形生成。
- 新增对象库数据结构 `Data\DaObjectCatalogData.cs`：包含 `DaObjectCatalog`、`DaCatalogSegment`、`DaCatalogItem`、`DaCatalogMaterial`、`DaCatalogSource`、`DaCatalogDefaults`，后续 UI/导入导出可以直接复用，不再依赖私有诊断类型。
- `ObjectCatalog.json` 预计包含：区域 item/material id 索引、item kind/role/name/stableKey、来源 step/owner/field、是否父子模板/单物品生成器/PhotonView、renderer 材质名、默认数量/缩放/区域/生成概率等参数快照。
- 2026-05-10 构建通过：新增 `DaObjectCatalogDiagnosticService` 和 `DaObjectCatalogData` 后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 用户实机生成 `ObjectCatalog.json` 成功：最新文件位于 `Beach-Segment__Jungle-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment`，时间 2026-05-10 20:17:53，大小约 246 KB；日志只有 `Object catalog diagnostic written`，未见写入失败。
- 最新 `ObjectCatalog.json` 统计：`SegmentCount=5`、`ItemCount=147`、`MaterialCount=10`。区域条目：Beach 34 items/3 materials，Jungle 33/1，Desert 51/2，Caldera 16/3，Volcano 13/1。
- `ObjectCatalog.json` 角色分布：items 为 `step-prop-prefab=144`、`single-item-prefab=1`、`parent-child-template=2`；materials 为 `set-material=5`、`set-child-material=1`、`random-material=1`、`replace-material-from=1`、`replace-material-to=1`、`banned-material=1`。
- 抽样确认：Desert 的 `Dynamite` 成为 `single-item-prefab`，保留 `PhotonView`、来源 `Props/Dynamite/objToSpawn` 和默认 `nrOfSpawns=20`、`area=250,280` 等参数；Beach 的 `WallRocks` / `WallProps` 成为 `parent-child-template`；Desert 的仙人掌、AntLion、Oasis、mineshaft 等条目带 `HasChildGeneration`。
- 默认参数覆盖有效：`layerType` 覆盖 147 个 item，`nrOfSpawns` / `syncTransforms` 覆盖 145 个，`area`、`chanceToUseSpawner`、`minSpawnCount`、`randomSpawns` 等覆盖 136 个，足够作为区域物品 UI 的第一版参数来源。
- 注意：本轮 catalog 是 Jungle 组合，不包含 Roots；Roots 的 Redwood / Forest Cave 等父子模板需要下次切到 Roots 组合后再生成 `ObjectCatalog.json` 覆盖验证。
- 用户完成 Roots 组合测试：`Beach-Segment__Roots-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment\ObjectCatalog.json` 于 2026-05-10 20:30:24 生成，大小约 291 KB；`ObjectReferenceMap.json` 同步更新，日志未见写入失败。
- Roots 版 `ObjectCatalog.json` 统计：`SegmentCount=5`、`ItemCount=169`、`MaterialCount=20`。其中 Roots Segment 有 55 个 item、11 个 material；items 分布为 `step-prop-prefab=160`、`parent-child-template=8`、`single-item-prefab=1`。
- Roots 父子模板已验证进入 catalog：`Redwood`（4 个子 `LevelGenStep`）、`Forest Cave Safe`（10 个子 `LevelGenStep`）、`Mushroom tree Flat tall`、`redwoods` treePlatformParent、`Redwood Massive` 等都带来源 step 和默认参数。
- Roots 材质规则已验证进入 catalog：`M_Forest_rock`、`M_Forest_rock_Bald`、`M_Wood 1`、`M_Rock`、`RedWood`、`M_Foliage Pine colony` 等按 required/set/replace/banned 分类输出。
- 修复对象库诊断日志噪声：`DaLocalization` 新增 `TranslateOrOriginal`，`DaObjectCatalogDiagnosticService` 对 catalog item/material 显示名采用静默兜底，不再因大量 prefab/material 名称缺翻译刷 `Missing localization mapping`。
- 2026-05-10 构建通过：修复 catalog 显示名日志噪声后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 新增第一版只读区段模板库 UI：`DaCustomiserWindow` 在当前 Step 编辑器下方显示当前 Segment 的 catalog item/material 数量、物品模板、材质规则、来源 grouper/step/field、默认数量/范围/概率、父子模板/Photon 标记。当前仅展示，不添加/删除/改生成。
- 新增 `DaObjectCatalogService`：按当前 mapKey 缓存 `DaObjectCatalogDiagnosticService.BuildCatalog(data)` 的结果，避免 IMGUI 每帧重建 catalog；重新扫描或换 mapKey 后会重建。
- `DaLocalization` 补充 catalog UI 文案和 role 显示名：生成器模板、单物品模板、父子模板、设置材质、随机材质、替换材质、必需/禁用材质等。
- 2026-05-10 构建通过：只读区段模板库 UI 接入后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 根据用户截图修正 F1 布局：左侧主相机模式面板宽度从 430 调到 520，实际左栏从 390 调到 490；顶部地图名从完整 `mapKey` 改为中文短组合，避免按钮和地图名挤成竖排。
- 详情区新增标签页：`参数` / `区段模板库`。模板库不再压在一长串参数下面，切到模板库页即可直接看当前 Segment 的 item/material。
- 2026-05-10 构建通过：布局修正后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 用户指出“区域物品库”概念容易误导：当前 catalog 是按游戏 Segment/区段归档模板，不是未来手动摆放用的山顶/山腰/洞口这类 XYZ 子区域。已将 UI 文案改为“区段模板库”，并将默认参数里的 `area` 显示为“范围”。
- 设计结论：后续添加物品时不能只按 Segment；需要在 Segment 下新增“放置子区/锚点/XYZ 范围”模型，例如同一关内的山顶、山腰、洞口各有自己的中心点、范围、法线/落地规则和可用模板列表。
- 补充一批 catalog prefab/material 翻译：Jungle palm、行李、海滩贝壳/海胆、Redwood/Mushroom tree、Forest Cave、Desert cactus/rock、Lava/Rock/material 等；Release 构建 0 warnings, 0 errors。
- 修正当前生成器范围高亮：`DaSceneHighlighter` 不再把 `area` 画成立体大盒子，改为贴近地面的平面脚印（矩形/圆形）和中心十字/竖向短标记；选中项用青色，已修改项用橙色。这个高亮表示当前 LevelGenStep 的生成范围，不等同于未来“放置子区”。
- 2026-05-10 构建通过：高亮修正后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 根据用户实机截图继续收敛高亮：矩形范围不再画完整外框和对角线，且只使用 yaw 水平旋转，避免随 step 的 pitch/roll 变成立体斜面；现在矩形范围只显示四角短括号和小中心标记，减少大范围 step 对地图预览的遮挡。
- 2026-05-10 构建通过：高亮二次修正后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 根据新一批截图继续修正：区段模板库补 `Jungle_Willow_*`、`Jungle_Monstera*`、`Ice_Pine *`、`Ice_DeadShrub *`、`Rock_Plat`、`RockCold`、`M_Rock_ice`、`M_RopeBridgeCold` 等翻译，并新增 `Ice_Pine` / `Ice_DeadShrub` / `BI_Rock` 编号规则。
- 高亮四角括号长度改为按实际范围收缩，取消 4 米最小长度，避免 `area=150,140` 或 fallback 小范围时四角短线互相接上，看起来又像完整方框。
- 2026-05-10 构建通过：翻译和高亮三次修正后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 根据用户截图修正区段模板库残留英文：新增编号 prefab 规则翻译，覆盖 `Rock_Round.007`、`Rock_Round12`、`Rock_Lil.002`、`Desert_Rock 13`、`LavaBridge 6` 等同类名称；并补 `Shader` / `PhotonView` UI 标签中文化。Shader 具体资源名仍保留原始技术名。
- 2026-05-10 构建通过：区段模板库翻译修正后 Release 输出到 r2modman terrain profile，0 warnings，0 errors。
- 最新 `ObjectReferenceMap.json` 统计：`ReferenceCount=1235`、`UniqueObjectCount=179`、`CatalogCandidateCount=1196`、`ParentChildCandidateCount=50`、`SingleItemSpawnerShellCount=2`。分类字段已正常写入，说明增强诊断链路可用。
- 角色分布：`step-prop-prefab=1033`、`single-item-prefab=2`、`set-material=52`、`set-child-material=3`、`random-material=12`、`replace-material-from=9`、`replace-material-to=9`、`required-material=54`、`banned-material=4`、`unity-object-reference=57`。
- 关键样本：Desert 的 `Dynamite` / `Dynamite_Outside` 通过 `PSM_SingleItemSpawner.objToSpawn` 指向 `Dynamite` prefab，目标组件含 `Item`、`PhotonView`、`TrackableNetworkObject`、`Rigidbody` 等，确认单物品生成器资料可用。
- 父子/依附样本：Roots 中 `Redwood` 含 4 个子 `LevelGenStep`，`Forest Cave Safe` 含 10 个子 `LevelGenStep`，蘑菇树模板也有子生成器；后续绑定物关系可以优先从这些候选做对象库分组。
- 材质样本：统计到 `M_Forest_rock`、`M_Rock_Volcano`、`M_SaltRock`、`M_DesertSand`、`M_Foliage_Beach Bark` 等材质引用；后续材质 UI 可以从 set/random/replace/required/banned 分类分别处理。
- 新增只读诊断服务 `DaObjectReferenceDiagnosticService`：在写 `RuntimeExport.json` / `NameMap.json` 时额外输出 `ObjectReferenceMap.json`，专门记录当前 JSON 因 `UnityEngine.Object` 被跳过的 `GameObject`、`Material`、`Material[]` 等引用。
- `ObjectReferenceMap.json` 会按 Segment/Grouper/Step/owner 字段列出引用来源、目标对象名、类型、层级路径、组件、Renderer/Material、是否含 `LevelGenStep`、`SingleItemSpawner`、`PhotonView` 等信息，用于后续对象库、绑定物和材质 UI 设计。
- `ObjectReferenceMap.json` 已进一步增强分类字段：`RoleCounts`、`CandidateReasonCounts`、`ObjectTypeCounts`、`CatalogCandidateCount`、`ParentChildCandidateCount`、`SingleItemSpawnerShellCount`。每条引用会标出 `Role`、`CatalogCandidate`、`CandidateReason`、是否含子生成器/单物品生成器，后续可直接筛选可添加物品、材质引用和父子模板候选。
- 2026-05-10 构建通过：`dotnet build ... DreamyAscent.csproj -c Release`，输出到 r2modman terrain profile，0 warnings，0 errors。
- 2026-05-10 再次构建通过：增强对象引用诊断后 Release 输出到 r2modman terrain profile，0 warnings，0 errors；这次仍只改诊断输出，不改地形生成行为。
- 现有诊断抽样结果：2026-05-10 最新地图组合有大量 `PSM_SetMaterial*` / `PSM_ReplaceMaterial` / `PSC_RequiredMaterial`，但没有 `PSM_ChildSpawners` / `PSM_SingleItemSpawner`；2026-05-09 Roots/Desert 组合里有 `PSM_ChildSpawners=4`、`PSM_SingleItemSpawner=2`。
- 具体位置样本：`Roots Segment / WallRocks / Roots`、`Big Redwood` 使用 `PSM_ChildSpawners`；`Desert_Segment / Props / Dynamite` 和 `Dynamite_Outside` 使用 `PSM_SingleItemSpawner`；多处 Rocks/Waterfalls/Palms/Bushes/Shrub 使用材质 modifier 或 material constraint。
- 复核本地参考代码：`引用参考代码\BepInEx\plugins\TerrainCustomiser`、`引用参考代码\BepInEx\plugins\TerrainRandomiser` 和 `引用参考代码\1.61.b\Assembly-CSharp`。
- 原 TerrainCustomiser 的 `TerrainData` / `TerrainExporter` 证明当前 DreamyAscent 的 `Segment -> Grouper -> LevelGenStep -> Properties/Constraints` 数据结构方向正确；它本身不解决新增物体、外部资源和材质引用问题。
- 原生 `PSM_ChildSpawners` 会在父物体生成后执行其子物体下的 `LevelGenStep.Execute()`，这正好可作为椰子树/椰子等父子依附生成的核心机制参考。
- 原生 `PSM_SingleItemSpawner` 会把 `SingleItemSpawner.prefab` 指向 `objToSpawn`，说明“生成器壳 + 具体物品 prefab”是游戏已有模式；但 `objToSpawn` 是 `GameObject` 引用，仍需要对象库/稳定资源 ID 才能存档和导入导出。
- 原生 `PSM_SetMaterial`、`PSM_SetMaterialOnChild`、`PSM_SetRandomMaterial`、`PSM_ReplaceMaterial` 均通过 `sharedMaterial`/`sharedMaterials` 替换材质；可作为规则参考，但运行时 UI 改色不要直接照抄共享材质写法，避免污染全场同源物体。
- TerrainRandomiser 的价值主要在多人同步：主机通过房间属性同步 map settings，并收集/分发生成物 `PhotonView` ID；这对 DreamyAscent 未来手动放置物/外部物体同步有参考意义。
- 新增后期需求：UI 参数按区域划分；每个区域可手动添加物品（如灌木），并配置数量、大小等参数。
- 新增绑定物需求：物品可能存在父子/依附关系，例如椰子依附椰子树、椰子树落地；生成、移动、清理、导入导出时必须按依赖图处理。
- 新增外部物品需求：允许外部手动加入 Unity 物品并放到地形上。
- 新增材质需求：评估物品材质/颜色是否可换，并为后续 UI 暴露材质配置留位置。
- 现有资源判断：当前数据模型已是 `Segment -> PropGrouper -> LevelGenStep -> Properties/Constraints`，可支撑“按区域编辑已有生成器参数”和“按区域重新生成”。`nrOfSpawns`、`minSpawnCount`、`scaleMinMax`、`area` 等字段已在白名单中，数量/大小类参数可直接沿用现有属性编辑器。
- 现有缺口：当前导出会跳过 `UnityEngine.Object` 引用，因此 prefab、material、renderer、外部 asset 等不能只靠现有 JSON 保存；需要新增资源注册表/对象库和手动放置数据层。
- 代码线索：`DaLocalization` 已包含 `PSM_ChildSpawners`、`PSM_SingleItemSpawner`、`PSM_SetMaterial`、`PSM_SetMaterialOnChild`、`PSM_SetRandomMaterial` 等名称，说明游戏生成系统里已有子生成器和材质修改概念，但当前导出/编辑层还没有把这些对象引用安全暴露出来。
- 诊断资源：r2modman terrain profile 下已有 2026-05-10 的 `RuntimeExport.json` 和 `NameMap.json`，可用于继续抽样确认具体 step/constraint 分布；当前沙盒可列目录，但直接读取大 JSON 内容受权限限制，需要在允许后进一步分析。

## 2026-05-10 关卡预览位姿补齐

- 读取 r2modman terrain profile 下 `DreamyAscent PreviewPoses.json`，确认用户新增保存了 3 个关卡预览位姿：`Jungle_Segment`、`Snow_Segment`、`Volcano_Segment`。
- 已将这 3 个位姿补入 `DaMapPreview.DefaultPreviewPoses`；目前 7 个主要区段均有代码内置默认预览位姿，外部 JSON 仍可覆盖。

## 2026-05-09 源码编码与预览文案检查

- 继续 DreamyAscent：确认 `dotnet build -c Release` 通过，输出到 r2modman terrain profile，0 警告、0 错误。
- 扫描 DreamyAscent 源码中的典型乱码标记（`�`、`Ã`、`Â`、`鏇`、`锛` 等），未发现源码损坏；`memory` 早先显示乱码是 PowerShell 默认编码读取 UTF-8 文件造成的显示假象。
- `DaMapPreview` 里两处直接中文预览提示已收进 `DaLocalization`，源码层只保留 `AssemblyInfo.cs` 模板中文注释，UI 文案走本地化表。
- 根目录 `.editorconfig` 已补 UTF-8 BOM，和 `charset = utf-8-bom` 规则保持一致，降低后续编辑器/终端误读中文的概率。

## 2026-05-09 预览位姿校准

- 按用户确认的范围实现预览校准：保留滚轮缩放，新增 WASD 平移、Shift 加速、Space 上升、Ctrl 下降。
- 只新增一个保存快捷键：`F6` 保存当前区段预览位姿；不增加 F5/F7/F8/F9 等额外快捷键。
- 新增 `DaPreviewPoseService`，读取/写入插件目录下 `DreamyAscent PreviewPoses.json`。切换区段时优先读取已保存位姿，没有保存则走原有自动 fallback。
- 飞行控制只在鼠标位于预览区域且不在左侧控制面板上时生效，避免编辑输入框时误移动预览。
- 已将用户采集到的 `Beach_Segment`、`Roots Segment`、`Desert_Segment`、`Caldera_Segment` 位姿整合进 `DaMapPreview.DefaultPreviewPoses`。外部 JSON 仍可继续覆盖/新增其它区段。
- 2026-05-09：正式 `dotnet build -c Release` 通过，输出到 r2modman terrain profile，0 警告、0 错误。

## 2026-05-08 暂停存档

- 本轮暂停 DreamyAscent，不继续追加预览雾相关改动。
- 用户确认：第一遍预览雾修复已解决主要问题；后续由于上下文切换产生的第二遍修复不作为继续方向。
- 当前建议状态：保留已验证有效的预览修复，后续再做时转向 UI/交互、中文残留、生成刷新、同步和性能问题。

## 近期已完成

- 2026-05-08：针对预览雾状/白雾遮挡做了第一轮修复。`DaMapPreview` 的 RenderTexture 渲染改为纯色清屏，并在预览渲染期间临时关闭 `RenderSettings.fog`，渲染后恢复原全局雾效；诊断日志新增 fog/clearFlags/fogDensity 等参数。
- 2026-05-08：根据日志确认 `renderFog=False`，白雾更可能来自 `StormVisual` 雪暴/风暴视觉；第二轮修复在预览渲染期间临时隐藏 `StormVisual` 下的 Renderer/ParticleSystemRenderer，渲染后立即恢复。
- 2026-05-08：参考原 TerrainCustomiser 的处理方式，新增预览环境抑制：预览期间关闭 `Misc/Post Fog`、`Post Fog`、`FogSphereSystem`，关闭预览时恢复。原 MOD 在创建 UI 后也会关闭 `Misc/Post Fog` 和 `FogSphereSystem`，说明这些是游戏运行时自带的雾/遮罩来源。
- 2026-05-08：`dotnet build -c Release` 通过，输出到 r2modman 测试 profile 的 `BepInEx/plugins/DreamyAscent.dll`；当前 0 警告、0 错误。
- 建立 DreamyAscent 独立工程。
- 将版本号固定为 `0.1.0`。
- 根据 TerrainCustomiser 反编译代码补齐扫描、生成组、生成段、步骤、属性编辑、导入导出和运行时刷新等能力。
- UI 已完成大部分中文化，并加入动态名称映射和诊断导出。
- 预览从早期右侧小窗口方案调整为左侧控制窗口加游戏画面预览。
- 废弃了直接 `Camera.rect` 分屏预览方案，改为 RenderTexture 预览。
- 停止在打开 F1 界面时隐藏游戏 Canvas，避免输入异常。
- XYZ 属性拆成独立输入，降低一起解析失败导致整组参数失效的概率。
- 加入关键日志和诊断输出，用于扫描、生成、预览、运行时刷新和异常排查。

## 当前验证结论

- 用户确认：当前预览主体已经可用。
- 预览雾状/白雾问题已做三轮代码侧修复：全局 fog 隔离 + StormVisual 渲染隔离 + 按原 MOD 关闭 Post Fog / FogSphereSystem；2026-05-10 多张实机截图显示主预览已清晰，不再作为 P0 阻塞。
- 旧 `Camera.rect` 方案会导致屏幕分割、输入冲突和窗口抖动，不可恢复。
- Canvas 隐藏逻辑曾引入 F1 打开后需要按 ESC 或鼠标异常，不可直接恢复。
- 第二关曾出现生成内容明显偏少，后续仍需继续验证所有地图轮换。

## 最近用户反馈

- 预览主体已经正常，2026-05-10 多张实机截图未再显示白雾遮挡。
- 2026-05-16 再次澄清当前阶段：现在是有 UI、诊断和模板库，但额外生成物功能还没进入实现阶段；后续再做。
- 2026-05-16 日志复查确认：之前发现的 `LayerMask` 回填和少量字段名不匹配属于真实修复项，不是纯噪音。
- 当前反馈重点转到区段模板库残留英文和生成器范围高亮表现。
- memory 需要从根目录堆叠改成 MOD 独立、通用规则独立、永久待办独立。
- memory 已重构为全项目结构，其他 MOD 不再只靠历史文件兜底。

## 2026-05-17 父子对象关系注册表改为样本直出

- `data/tools/build_map_data_artifacts.py` 已把 `parent-child-registry.json` 的来源切到 Snapshot V2 / TerrainRandomiser Snapshot V2 的 `GeneratedChildrenSnapshot.relationshipCandidates`，不再从 `object-registry-input.json` 二次过滤拼接。
- 新生成的 `data/map-data/generated/parent-child-registry.json` 已升到 `schemaVersion=2`，并补了 `children` / `interestingComponentFields` 明细；JSON 已用 Python 复核可正常反序列化。
- `DreamyAscent.csproj` 继续把 `parent-child-registry.json` 复制进插件目录，Release 构建已通过，`DreamyAscent.dll` 已更新到 r2modman terrain profile。
- 这个父子关系文件当前仍是只读诊断/UI 数据，不直接放开运行时父子生成逻辑；后续若要做椰子/椰子树这类绑定物生成，再单独接 `ParentChildPlacementGroup`。


