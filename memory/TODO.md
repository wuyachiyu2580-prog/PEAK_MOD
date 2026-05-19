# Permanent TODO

更新时间：2026-05-17

这里只记录未完成、待验证、已知风险和后续优化。已经稳定或已经写入各 MOD `RECENT.md` / `DECISIONS.md` 的内容，不再重复放在这里。

## DreamyAscent

### P0：当前阻塞或高风险

- [ ] 先实机复测 `CustomBlank` 清理边界：按用户最新定义，空白模板只保留起始点过渡，桥、绳子、终点、边缘中段等都应清掉。重点确认普通关卡 `Start` 过渡还在，同时 Beach `Ropes/Bridges/Small_End`、Snow `End/End_L/End_R/Bridges`、Volcano `Edges Start/Middle/End`、Caldera `Rocks/Bridges` 不应残留；Volcano 的 `Mechanics/RisingLava` 和 `Mechanics/Rock_Round.010` 岩浆涨落机制必须保留；Desert/Oasis 与 Snow 中出现的卡皮巴拉对象也必须被空白模板清掉，当前规则按名称 `Capy/Capybara` 或当前 Renderer 材质 `M_Capybara` 兜底删除。
- [ ] 进游戏复测新版 Snapshot V2 Data 加载：打开 UI 默认不应自动加载样本资产；点击“加载样本资产”后应显示模板快照/注册表状态，`template-snapshots.json` 应是 135 snapshots，`object-registry-input.json` 应是 193 templates / 25 materials；同时确认启动内存和打开 UI 内存没有明显回退。
- [ ] 实机验证当前变体内置默认模板基线：代码已接入 `HasCurrentVariantDefaultTemplate()` / `GetCurrentVariantDefaultTemplate()`，官方模板生成会按当前 variant snapshot 过滤，UI 样本资产面板会显示“当前变体默认模板”状态；下一轮需进游戏点击加载样本资产并确认各 Segment 显示可用，生成日志出现 `Using current variant default template for segment generation`。
- [ ] 先确认“生成本段只剩地形”到底是模式语义还是生成失败：如果当前是 `CustomBlank`，只剩地形属于预期；如果当前是 `OfficialTemplate` 但仍只剩地形，再追 `RunOfficialSegment()`、`ContainsRuntimeGrouper()`、`HasCurrentVariantDefaultTemplate()` 和运行时引用重绑日志。
- [ ] 将 Snapshot V2 新版 `data/map-data/generated/template-snapshots.json` 用于稳定模板匹配：重跑后预期覆盖 135 个 segment、536 个 grouper、4194 个 step；把 path/ID 接入运行时匹配，解决 Desert 重复 `Props/Rocks`、Roots/Jungle variant 分支和后续内置模板基线问题。
- [ ] 将 Snapshot V2 新版 `data/map-data/generated/sample-regression-report.json` 接入常规回归：脚本要求诊断五件套（含 `GeneratedChildrenSnapshot.json`）、schema 3、`relationshipCandidates`、`0 grouper`、未知 variant、覆盖、脏样本和 TR 来源标记；后续新增样本、改导出、改模板匹配或改空白保留规则后必须重跑并保持 `status=pass`。
- [ ] 复测第一版运行时低风险放置：`CustomBlank` / `Hybrid` 自动应用规则，`OfficialTemplate` 手动应用；确认数量上限 25、生成功能、清理自定义、导出再导入回显、当前变体过滤、诊断失败原因都正常。
- [ ] 严禁把高风险模板当普通 prefab 直接生成：带 `PhotonView`、`SingleItemSpawner`、子 `LevelGenStep`、子 `PropGrouper`、`Spawner` 继承链、`BerryBush/BerryVine/GroundPlaceSpawner/Luggage` 等模板继续拒绝；椰子、果子、行李、父子树、机制物和网络物后置到绑定物/spawner/Photon 方案。
- [ ] 修复或确认当前漂浮物来源：椰子和果子浮空大概率来自把父子/生成器型模板直接实例化；短期通过拒绝高风险模板规避，长期需要 `ParentChildPlacementGroup`、落地验证诊断和生成后高度检查。
- [ ] 客机端兼容问题暂缓：2026-05-16 客机端 `Player.log` 出现过 DreamyAscent IMGUI `ArgumentException`、`DropItemRpc` NRE、缺失 PhotonView 和 RPC flood 噪声。左下摘要面板已加固定控件数防护，但多人/客机端复测、同步一致性和日志降噪全部后置；当前先完成核心功能。
- [ ] 复测 Desert 官方模板生成：2026-05-12 日志确认 `DesertRockSpawner.Clear` NRE 是官方模板生成前 DreamyAscent 先手动清子物造成的高风险路径；已移除官方模板生成前预清理，需确认 Desert 不再空心且日志不再报该 NRE。
- [ ] 复测 Roots/Jungle 官方模板生成：2026-05-12 日志显示 Roots fallback 到 whole segment 后把多个互斥变体一起生成，导致不该出现的石头；第一次用 `activeInHierarchy` 过滤过严导致 Roots/Jungle 扫不到，现改为只排除 inactive 的 `- xxx Variant` 分支，需确认能扫到默认组且不叠加禁用变体。
- [ ] 复测 Roots 官方生成：官方整段生成恢复跳过 `PlateauRocks` / `WallRocks` 以避免异常乱石；`CustomBlank -> OfficialTemplate` 运行时不保证完整恢复，UI 已提示建议重开或新图复测。
- [ ] 复测 DreamyAscent 生成前运行时引用重绑：旧日志中 Roots 点击“生成本段”时非跳过组全部 `runtime grouper reference is missing`，新 DLL 已在生成本段/生成本组/参数修改自动生成前按当前场景重新绑定引用；需确认日志出现 `Runtime references rebound`，且 Roots 不再 `groupers=0`。
- [ ] 复测 Jungle 官方整段生成：日志已确认 Jungle 可扫描并显示，问题是“生成本段”重复执行 `Rocks_Plat` / `Rocks_Wall` 导致岩石堆叠。新 DLL 已跳过这两个组，需确认日志显示跳过并且 `Generated segment: Jungle_Segment, groupers=2` 左右，不再新增大量石头。
- [ ] 后续评估 Jungle `Rocks_Wall` 里的 `Waterfalls` 是否需要单独保留；如果需要，改为 step 级白名单生成，不能恢复整组 `Rocks_Wall`。
- [ ] 停止把 Roots/Jungle 岩石组问题扩展成无限 per-map skip；短期跳过保持安全，长期用干净模板快照、稳定 path、step 级白名单和子区规则替代。
- [ ] 补强 `SubArea` 编辑 UI：当前只能创建 `SegmentBounds` 默认子区并查看中心/尺寸；在模板基线稳定后，允许编辑中心 XYZ、形状/范围、ray/layer、落地约束和兼容模板，把 Segment、区段模板库、原版 `PropSpawner.area`、未来山顶/山腰/洞口 XYZ 放置子区分开。
- [ ] 持续补参数中文说明 UI：已先覆盖常见 Step 参数和 bool 开关；后续继续给 modifier/constraint 的字段补中文含义、风险提示和推荐范围，说明文字优先放左下辅助面板，不挤占右侧参数编辑区。
- [ ] 实机复测 DA 三层联动结构面板：顶部关卡横排、第二行区域横排、下方生成物列表需实时联动；切换区域后下方生成物、右侧参数和场景高亮必须同步，且生成物行的 item/material 数量要能帮助判断区域内容是否重叠。

### P1：功能完整性

- [ ] 复查预览代码遗留路径：当前 `_screenPreviewActive` 是有效状态位，`mainCamera.rect` 仅保存/恢复原状态，不等同于旧分屏方案；后续只清理确认为无用的 RenderTexture 小窗或旧注释，不要误删主相机预览所需状态。
- [ ] 全量验证地图轮换和新增地图：Beach、Jungle、Desert、Snow、Roots、Caldera、Volcano。
- [ ] 结合全量地图轮换复查历史“第二关生成内容偏少”问题；如果后续 Jungle/Roots 截图持续正常，就从 TODO 移除。
- [ ] 完善绑定物处理，例如椰子树和椰子必须一起移动、生成和清理。
- [ ] 后期 UI 改为区域优先：每个 Segment 下集中管理已有生成器参数、可添加物品、手动放置物、材质规则和导入导出状态。
- [ ] 继续追踪地图漂浮物问题：若低风险静态模板仍漂浮，再排查 raycast layer、落地法线、bounds、清理残留和生成后高度验证；不要再用椰子/果子这类父子模板验证普通放置。
- [ ] 复测 `CustomBlank` 的“只保留起始点过渡”规则：当前白名单仅 `LevelGenStep` 名称 `Start`，但 `Volcano_Segment` 例外，不保留 `Start`，因为它对应开始墙壁石头；若桥、绳子、终点、边缘中段或 Volcano 起始墙石仍残留，优先修清理边界，而不是恢复整组官方装饰。
- [ ] 将区段模板库升级为“全局模板注册表 + 当前区段过滤 + 目标子区兼容规则”：支持跨区段混合选择模板，但保留来源 Segment、默认参数和兼容警告。
- [ ] 将当前 `ObjectCatalog` 抽成正式 `DaObjectRegistry`：记录稳定模板 ID、来源 map/segment/grouper/step/path、prefab/material 加载方式、组件摘要、PhotonView 风险、默认缩放/碰撞/落地策略和资源缺失行为。
- [ ] 完善第一条低风险自定义放置规则：当前已有运行时实例化初版，后续补更细的子区编辑、落地方式、生成后诊断、失败原因、只清理 DreamyAscent ownership 和可视化验证。
- [ ] 实现第一条跨区段低风险放置例子：在子区/模板基线稳定后，从 Jungle 低风险静态模板放到 Desert 平台/山腰 `SubArea`，使用目标子区 ray/layer/范围落地，限制数量 3-10，写诊断，验证无漂浮/穿模/误清理；父子树、椰子、果子不作为第一批验证对象。
- [ ] 将模板库从参数编辑窗口拆为独立窗口或固定侧栏：支持搜索、分类、当前区段/全局过滤、右键添加到当前子区、拖拽到预览区域等交互。
- [ ] 设计放置子区物品编辑：允许按区域/子区添加物品（如灌木），配置数量、大小/缩放、分布范围、落地方式、随机旋转、生成概率和清理策略。
- [ ] 设计放置子区模型：在 Segment 下支持多个命名子区域/锚点（如山顶、山腰、洞口），保存中心 XYZ、范围/形状、法线/落地规则、允许模板和默认参数。当前 `ObjectCatalog` 只是区段模板库，不等同于放置子区。
- [ ] 建立绑定物关系模型：支持父子/依附模板，例如椰子树先按地面约束生成，椰子再挂到树上；移动、缩放、删除、保存、导入和多人同步时按绑定组处理。
- [ ] 额外生成物功能后置：当前阶段先把 UI、模板库、诊断和日志修复收干净，不要把“有界面”误记成“已经可以生成额外物品”。
- [ ] 设计外部 Unity 物品导入路径：确定是否支持 AssetBundle、场景对象克隆、游戏内 prefab 扫描，保存稳定资源 ID 和加载失败回退行为。
- [ ] 评估并实现物品材质/颜色替换：优先使用实例材质或 MaterialPropertyBlock，避免修改 sharedMaterial 污染全场；保存 renderer 路径、材质槽位和颜色/材质 key。
- [ ] 完善导入、编辑、导出、导入后直接应用的流程。
- [ ] 增加随机种子显示，最高至少 9 位；多人游玩时每次生成地形不同，但整体风格参数保持一致。
- [ ] 解决多人中途加入地图不同步问题，可参考 TerrainRandomiser 反编译代码，但不要做兼容层。
- [ ] 参考 HazardSpam 的主机生成位置并 RPC 广播方式，设计手动/子区生成物的网络数据：模板 ID、目标子区 ID、positions、rotations、必要 PhotonView ID 和资源缺失降级。
- [ ] 设计 `ParentChildPlacementGroup`：`hasChildGeneration=true` 的模板（如 Roots `Redwood`、`Mushroom tree Flat tall`、Desert 部分仙人掌）先只读标记；未来允许时必须父物落地、子 `LevelGenStep` 依附执行，并按组移动/缩放/删除/同步。
- [ ] 设计生成后验证诊断：每条 `PlacementRule` 生成后记录 raycast 命中、离地高度、法线、layer、越界/穿模/失败约束和清理 ownership，专门用于追漂浮物。

### P2：性能和体验

- [ ] 降低运行时生成耗时，参考原 TerrainCustomiser 的批处理、延迟刷新或对象复用方式。
- [ ] 优化频繁 Instantiate、RPC、PhotonView 相关开销，用 WhySoLaggy 日志做归因。
- [ ] 继续补动态英文名称映射和中文翻译。

### P2：后期物品编辑资料缺口

- [ ] 复测最新区段模板库 UI：截图已确认列表能显示和滚动；剩余重点是残留英文是否继续出现，以及日志是否无 `Missing localization mapping` 刷屏。
- [ ] 复测最新生成器范围高亮：最新 DLL 已改为水平四角短括号 + 小中心标记，并按实际范围缩短括号；需确认不再出现完整大框、跨地图对角线、倾斜斜面，或小范围短线拼成完整方框。
- [ ] 收集游戏内可复用物品模板清单：灌木、树、石头、椰子树、椰子等的层级路径、组件、碰撞体、renderer/material、是否带 LevelGenStep 或特殊脚本。
- [ ] 对照 `MAP_GENERATION.md` 继续细化 HazardSpam 可借鉴点：Zone/SubZone 描述、PropPrefabs/PropSpawners 分离、SpawnIdentifierNet、主机端 positions/rotations 广播，以及不能照搬的危险物专用逻辑。
- [ ] 建立跨区段模板兼容矩阵：低风险普通模板、中风险材质/碰撞模板、高风险父子/Photon/AI/机制模板分别列出，并给出能放到哪些目标 `SubArea` 的依据；初稿已写入 `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`，后续要随诊断数据和实机结果持续细化。
- [ ] 确认外部 Unity 物品格式：是否要求 AssetBundle、Addressables、普通 prefab 导出包，还是只允许从当前场景对象克隆。
- [ ] 确认多人同步方案：手动放置物和外部导入物是否由主机生成并广播，客机是否需要同样资源包，资源缺失时如何降级。
- [ ] 确认材质替换边界：只改颜色，还是支持整套材质；是否允许替换 shader/贴图；是否需要按区域批量随机配色。

## ItemInfoCN

- [ ] 新增原版或 MOD Action 组件时，扩展 `GetComponentEffectInfo` 和中文/颜色映射，避免 HUD 只显示英文类名或 `Value=0`。
- [ ] 如果发布 1.0.0 之后继续改动，必须同步发行包 `README` / `CHANGELOG` / `manifest`，不要只改源码。

## Lantern_ShootZombies_Night

- [ ] 实机验证 BRP `DispelFogField` NRE 是否降为 `DispelFogGuard` 10 秒节流日志，且不再刷屏。
- [ ] 实机验证客机本地灯燃料权威：有备用池时只降备用池、不降灯燃料；主机/远端广播更低 fuel 或 `OnInstanceDataSet` fuel 下降时，客机本地 tracked fuel 不被覆盖。
- [ ] 实机验证背包灯/非本机 Photon owner 但属于本地槽位的灯是否会先消耗备用池，备用池不足后才消耗灯本体燃料。
- [ ] 实机验证重复灯场景下只有主灯笼消耗备用池/燃料，`FuelSync` 不再出现同一玩家 `broadcast 2 owned lantern(s)` 污染 HUD；若仍出现副本，确认非主副本燃料被恢复而不是继续下降。
- [ ] 如果主机通过 `LightLanternRPC(false)` / `SnuffLantern` / Photon 对象销毁强制影响客机本地灯，补拦截远端熄灭或立即重燃逻辑。
- [ ] 实机验证客机端燃料从 0 恢复后是否能稳定重新点燃，重点看 `SEND LightLanternRPC(true) via fuel-add` 日志。
- [ ] 实机验证 `ExtraLanternPurger` 遇到无权限背包灯时是否会改为清理有权限的重复灯，而不是连续 `cannot destroy lantern`。

- [ ] 补大队伍测试数据：6 人以上时 RPC 频率、灯笼同步、配置快照和回暖广播是否仍稳定。
- [ ] 重新确认 BlackPeakRemix 最新版本兼容边界，尤其是灯笼功能重叠时是否仍能让渡补丁。
- [ ] 继续从源码和 BepInEx 日志抽取 0.2.1 之后更细的稳定结论，写入 `RECENT.md`。

## PlayersInfo

- [ ] 验证 6 人以上队伍 HUD 布局是否溢出，并决定滚动、分栏或折叠策略。
- [ ] 验证超高 DPI 下 TMP 描边是否仍清晰，必要时加入缩放补偿。
- [ ] 细调 `TeamRosterTracker` 的重排时机，避免队友条目排序抖动。

## WhySoLaggy

- [ ] 从 `WhySoLaggyPlugin.cs` 反查默认日志级别和各监控项默认开关，写入 `RECENT.md` / `DECISIONS.md`。
- [ ] 整理与其他 MOD 联合诊断的操作步骤，尤其是 DreamyAscent 生成卡顿、RPC、PhotonView 和 Harmony 耗时归因。
- [ ] 明确哪些诊断项可能带来额外开销，避免测试时误把诊断开销当成业务 MOD 问题。


