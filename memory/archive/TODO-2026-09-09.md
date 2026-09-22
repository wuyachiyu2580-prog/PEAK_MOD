# Permanent TODO

更新时间：2026-09-09

这里只记录未完成、待验证、已知风险和后续优化。已经稳定或已经写入各 MOD `RECENT.md` / `DECISIONS.md` 的内容，不再重复放在这里。

## ModConfig 本地化安全迁移

- [ ] P0：在 PEAKLib.ModConfig/PEAKLib.UI 的 `SettingsCell` 克隆链修复 `LOC: 0`：禁用克隆 `SettingsUICell.localizedText.autoSet` 后再写配置显示名；不要伪造本地化 ID `0`，不要让业务 MOD各自修改共享模板。
- [ ] P0：移除 PlayersInfo、Lantern&ShootZombies&Night 的 `RefreshCache()` 调用和全局重注册实现；语言变化只更新自身描述和当前可见 UI。
- [ ] P1：将 PlayersInfo、Lantern&ShootZombies&Night、WhereIsThing、WhereIsMyAmulet、WhySoLaggy 的菜单反射迁移到 `ModSettingsMenu`，旧 `ModdedSettingsMenu` 只作兼容回退。
- [ ] P1：对 ModConfig `GetDisplayName()` Harmony 补丁按声明方法或 `MethodBase` 去重，消除 PlayersInfo、WhereIsThing、WhySoLaggy 当前的 inherited-method 警告。
- [ ] 增强 ModConfigDiagnostics：活动 UI 与预制体分栏，输出 `LocalizedText` 字段、当前 mod/section/filter/search、cell 到配置身份映射和相关 Harmony owner。
- [ ] 保持问题页面可见时按 `F9` 再生成报告；当前报告只确认未激活 `SettingsCell/Text (TMP)` 上存在 `LOC: 0`，不能冒充已捕获屏幕活动实例。
- [ ] 在同时安装多个 MOD 的环境中连续切换游戏语言至少 5 次，确认活动 UI 不再出现 `LOC: 0`，且配置项数量不增长。

## StateKeeper

- [ ] 09-09英文按钮/重命名修复已部署并同步0.1.0包，需重启确认显示、输入、确认/取消、语言切换与再次打开。资源已确认原版Resume持久监听和PauseMenu204/旧弹窗100排序问题，不能把静态字体核查当实机验收。
- [ ] 当前发布见 `mods/StateKeeper/RELEASE_AUDIT_2026-09-08.md`；0.1.0本地包已生成但未上传Thunderstore。短时实机验证新版六页报告、中英文/长名称、1080p/1440p、IME、鼠标/键盘/控制器、命名遮罩和返回取消，不能把64项测试当实机通过。
- [ ] 实机核对Enabled开关在已有局禁用/恢复、正式结束时保存和封存，以及DebugLogging性能日志开关；普通.NET无法执行Unity原生ObserveSync回调。
- [ ] 短时验证collectionRevision3单调时钟、恢复中断和切图；已有七局回归完成，不要求再录两小时长局。
- [ ] 分别测采集器/库存/UI/后台分析帧耗时与GC；插件入口0.00ms/frame不能排除StateKeeper开销。实例摘要100行、聚合桶回放精度及历史跨块时钟边界见报告。
- [ ] 按 `mods/StateKeeper/PLAN.md` 实现后台碎块合并为单局完整 `*.data.json.gz`，完成校验后再清理碎块，失败/取消必须保留可恢复数据。
- [ ] 独立的整体合并进度仍为后续范围；已有后台分析真实进度/取消、统计引擎、双语面板、收藏和自动测试，不再按早期“尚未实现”重做。
- [ ] 在 `PEAK-MAP` 新增独立 StateKeeper 匿名提交接口、Supabase 表和私有 Cloudflare R2 前缀；加入用户确认、双重脱敏、限流、压缩炸弹防护和可关闭开关。
- [ ] 实现适度脱敏导出：每次提交重新生成“玩家 A/B/C”编号，删除账号/连接标识和 GUID，保留局内相对时间，距离按约 5-10 米粗化，坐标默认删除或粗粒度化；默认不上传原始详细数据。
- [ ] 用真实 8 人长局验证整体合并、按需加载和分析内存峰值，不能在 Unity 主线程执行合并/分析/上传前大 JSON 构建。
- [ ] 先用已有长局与短时采集比较事件量、GZip体积和工作路径耗时；必要的多人压力测试不作为用户再次录两小时才能使用分析的前提。
- [ ] 调查旧数据中 `RunManager.TimeSinceRunStarted` 少量倒退；基础分析暂采用原始时间保留 + 单调 `analysisTime`/质量警告，后续再决定是否增加独立时间校正事件。
- [ ] 调查单次采样短时出现 9-10 个角色是实际加入/离开，还是离场角色对象暂存。
- [ ] 结合新数据决定是否把分块封存改为精确 150 个样本，而不是 5 秒检查点封存。
- [ ] 按五局数据确定的基础分析结果设计分析面板；综合评分、最佳队友和复杂战术评级继续后置。`发行/0.1.0` 当前仍是旧名草稿，不更新、不发布。

## PEAK 2.1.a

- [ ] 实机验证 `AntiSphere` 清理空物品时是否触发 `InvalidOperationException`；若出现，后续 MOD/兼容补丁只记录并绕开该路径，不把游戏逻辑改写成未授权的玩法修复。
- [ ] 实机验证 2.1.a 邀请加入时 quicksave 是否会被直接消费/覆盖，确认 `GameBooter` 移除 `SAVE_DESTROY_ON_JOIN` 确认页后的存档行为。
- [ ] 实机验证 checkpoint、青蛙 10 秒全局冷却、滑翔机 ascent 体力倍率和仪式匕首延迟消耗；异常时以 `引用参考代码\反编译\2.1.a` 为当前基线。

## DreamyAscent

### 状态：永久暂停/归档

- 2026-05-24 用户明确要求 `DreamyAscent永久暂停`。
- 本区块以下所有 DA 待办均冻结为历史资料，不再执行、不再主动复测、不再构建部署，除非用户明确恢复 DreamyAscent。

### P0：当前阻塞或高风险

- [ ] 先实机复测 `CustomBlank` 清理边界：按用户最新定义，空白模板只保留起始点过渡，桥、绳子、终点、边缘中段等都应清掉。重点确认普通关卡 `Start` 过渡还在，同时 Beach `Ropes/Bridges/Small_End`、Snow `End/End_L/End_R/Bridges`、Volcano `Edges Start/Middle/End`、Caldera `Rocks/Bridges` 不应残留；Volcano 的 `Mechanics/RisingLava` 和 `Mechanics/Rock_Round.010` 岩浆涨落机制必须保留；Desert/Oasis 与 Snow 中出现的卡皮巴拉对象也必须被空白模板清掉，当前规则按名称 `Capy/Capybara` 或当前 Renderer 材质 `M_Capybara` 兜底删除。
- [ ] 进游戏复测新版 Snapshot V2 Data 加载：打开 UI 默认不应自动加载样本资产；点击“加载样本资产”后应显示模板快照/注册表状态，`template-snapshots.json` 应是 135 snapshots，`object-registry-input.json` 应是 193 templates / 25 materials；同时确认启动内存和打开 UI 内存没有明显回退。
- [ ] 实机验证当前变体内置默认模板基线：代码已接入 `HasCurrentVariantDefaultTemplate()` / `GetCurrentVariantDefaultTemplate()`，官方模板生成会按当前 variant snapshot 过滤，UI 样本资产面板会显示“当前变体默认模板”状态；下一轮需进游戏点击加载样本资产并确认各 Segment 显示可用，生成日志出现 `Using current variant default template for segment generation`。
- [ ] 复测 Jungle/Roots/Snow 官方 `Generate Segment` 的 root-scoped selected-grouper pipeline + 零输出保底恢复：2026-05-20/21 已把生成本段改为 segment root selected-step pipeline，并在 selected PropSpawner/Line/Sphere 执行前备份直接子物体；若 `PropSpawner.Execute()` 正常返回但生成 0，会恢复旧子物体，避免点击后整段空掉。重启/重新加载游戏后，预期日志出现 `official segment root level pipeline used` / `official segment root pipeline used`、`backedUpSteps=<n>`；如果底层仍失败，应出现 `restoredZeroedSteps>0` 和 `zero-output PropSpawner diagnostics`，视觉上不应再大面积清空 `Bushes/Trees/Vines/Mushrooms/Pine/Shrub`。
- [ ] 若下一轮官方生成仍空，优先查完整堆栈诊断：搜索 `levelgen-execute-failed`、`levelgen-clear-failed`、`levelgen-deferred-failed`、`runtime-item-spawn-refresh-failed`、`runtime-rope-refresh-failed`、`grouper-validate-afterwards-failed`、`TargetInvocationException wrapper`。2026-05-20 已把生成链 catch 改为完整 `Exception.ToString()` 并加入 step/grouper hierarchy path。
- [ ] Beach 地形材质仍未解决：用户确认椰子/物品正常，但 BlueBeach 视觉仍像默认 Beach。已退回失败的 post-generation material modifier replay 和 child-scale sync；后续不要直接恢复旧方案，应先新增 renderer/material 诊断，确认是材质池随机、sharedMaterial 污染、还是非 PropSpawner 静态地形没有被 modifier 覆盖。
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
- [ ] 复测 Jungle 官方整段生成的岩石组跳过仍正常：日志应继续显示跳过 `Rocks_Plat` / `Rocks_Wall` 且只重跑 `Pops_Plat` / `Props_Wall`，不能为了修灌木/藤蔓恢复整组岩石生成导致重复堆叠。
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

## PlayersInfo 0.2.4 / PEAK 2.4.b

- [ ] Clean-session verify `AfflictionIconDisplayMode`: `ShowAll`, `HideTeammates`, and `HideAll`; confirm local/team/spectator ownership isolation and no impact on extra stamina, shield, campfire, or inventory icons.
- [ ] Verify normal, `passedOut`, `fullyPassedOut`, and `dead` teammate range transitions, including last-living position for dead players, no out-of-range roster retention, 5 m hysteresis, `NearbyRange=0`, max count, stable/distance sorting, revive, and reconnect.
- [ ] Verify a teammate turned into a skeleton by Book of Bones still shows a teammate bar unless PEAK also marks that character `dead=true`.
- [ ] Verify zero-stamina hunger countdown behavior while status widths change: after-value placement while space exists, hidden when the green fill is too narrow, centered only at true zero in the available `maxStaminaBar` region, and hidden immediately after recovery.
- [ ] Verify countdown suppression for hidden stamina values, local death, remote spectator targets, no hunger growth, immunity, and airport scenes.
- [ ] Confirm `Advanced.DebugLogging=false` produces no diagnostic snapshot/binding/layout spam; distinguish unrelated `WhySoLaggy` and PEAK save errors from PlayersInfo warnings/errors.
- [ ] Complete PEAK 2.4.b multiplayer and spectator regression testing before declaring 0.2.4 behavior fully verified.

## PlayersInfo historical 0.2.1 verification

- [ ] 实机验证本地额外体力条已恢复图一/原版布局：保持原缩进和短宽度、黑色内边框与闪电图标正常、石化段不跑出条外，绿色填充内部只显示当前值（例如 `40`），不出现 `+40/100` 或 `40/100`。
- [ ] 实机验证队友额外图形条仍完全隐藏，队友安全侧 `current/cap` 数值正常；同时确认队友物品栏的紧凑喷气背包燃料条位置、宽度和填充没有被本轮改动影响。
- [ ] 实机验证观战中心下拉项 `LocalCharacter` / `ObservedCharacter`，确认死亡、切换目标和无效观战目标不会串用本机数据。
- [ ] 实机验证饥饿倒计时、队友耐久进度条、熟食图标颜色和三档队友背包显示；同时确认开启物品栏时的帧率和 GC 开销。
- [ ] 实机验证 2026-05-21 配置修复：旧配置是否迁移到 `Display` / `Advanced`，`EnableStaminaBar=false` 是否只隐藏队友条，`Anchor` / `OffsetX` / `OffsetY` 是否能移动 HUD，中文语言 + PEAKLib.ModConfig 时配置分区/选项/枚举/描述是否中文显示，无 ModConfig 时是否正常启动。
- [ ] 验证 6 人以上队伍 HUD 布局是否溢出，并决定滚动、分栏或折叠策略。
- [ ] 验证超高 DPI 下 TMP 描边是否仍清晰，必要时加入缩放补偿。
- [ ] 细调 `TeamRosterTracker` 的重排时机，避免队友条目排序抖动。
- [ ] 本地额外条实机验证通过后，移除或降级 `LocalExtraRuntime` 等临时布局诊断，继续遵守默认安静日志规则。

## WhySoLaggy

- [ ] 整理与其他 MOD 联合诊断的操作步骤，尤其是 DreamyAscent 生成卡顿、RPC、PhotonView 和 Harmony 耗时归因。
- [ ] 用 PEAK 2.3.a 双客户端实机验证 1.0.4：远端 sender/new owner 归因、Ownership Request/Transfer/Update 分类、单 watched RPC 单 `RpcCall`、队列有界/溢出汇总。
- [ ] 验证同一进程卸载重载：无重复 Harmony patch、日志可重新初始化、所有监控计数从零开始。
- [ ] 重新运行当前 18 项 `CoreBehaviorTests`，特别确认最近新增的 ModConfig 分类测试；通过后再把当前工作区本地化改动视为可发布状态。

## OldPC

- [ ] 完成 `OldPC.slnx` 的 Release 构建并核对 `0.0.1.0`、profile DLL 和 0 warnings / 0 errors。
- [ ] 实机验证旧电脑模式与望远镜模式的画质捕获/恢复、F8 切换、场景切换和禁用配置。
- [ ] 实机确认 OldPC 不影响其他玩家、不发送 RPC、不修改房间属性，并评估 CRT 覆盖层和低渲染比例的性能。

## WhereIsMyAmulet

- [ ] 实机验证 1.0.3 的 Scout Statue 名称映射、双行标签间距、定时到期清理和多人显示。
- [ ] 惊喜模式尚未实现；后续需求确认后再定义配置、默认值和显示范围，不要从历史记忆推断为已完成。

## TerrainCustomiserCN

- [ ] 后续收到 `TerrainCustomiserCN_missing_translations.tsv` 后，继续补 `UI\DisplayNameTranslator.cs`，再 Release 构建、清空 TSV 表头、重打发布包。
- [ ] 每次 TerrainCustomiserCN 更新都必须在 README 和 memory 中写明对应的原版 TerrainCustomiser 版本；当前 `TerrainCustomiserCN 0.1.2` 对应 `TerrainCustomiser 0.3.2`。
- [ ] 原版 TerrainCustomiser 更新后，优先复查网络管理器 ID、`mapData` / `propViews` / `playerInfo` / `TC_inCustomMap`、Sirenix 序列化类型名和地图保存结构是否变化。
- [ ] 若用户反馈 0.1.2 后旧地图找不到，先检查新持久化目录 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves` 和旧插件目录 `Map Saves`；不要自动迁移、复制、删除或恢复地图文件。
- [ ] 第四关相关问题：`0.1.2` 已同步原版 `0.3.2` 的 Caldera/Volcano 自定义变体修复；若仍出现预览和游玩不一致，应先对照原版行为判断，除非用户明确转为功能修复任务，否则不要在 CN 版里单独改原版地形生成逻辑。
- [ ] 每次发布前检查 zip 内不得包含 `.deps.json` 或 `.OLD`，并确认 `TerrainCustomiserCNCollector.dll` 是否仍按发布策略随包上传。

## PeakMapBrowser

- [ ] 进游戏实机验证登录、自动刷新 token、退出登录和 guest cookie 点赞。
- [ ] 进游戏验证上传地图的账号归属、我的地图编辑/删除、JSON 替换、封面选择和 MOD 版本下拉列表。
- [ ] 进游戏验证社区地图详情弹窗的下载/点赞行为，以及弹窗打开时没有点击穿透到底层按钮。
- [ ] 确认线上 Supabase 已应用 `20260720120000_add_accounts_map_ownership_and_likes.sql` 和 `20260720130000_add_map_json_revisions.sql`，并确认 `SUPABASE_SERVICE_ROLE_KEY` 配置存在。
- [x] 网页端已部署 `POST /api/auth/sign-out`；线上空 JSON 请求不带 Bearer token 返回 `401` 和 `Cache-Control: no-store`。仍需使用真实账号在游戏内验证有效 token 能撤销当前 refresh session。
- [ ] UI 美化/框架重构后置：用户暂不要求从 IMGUI 改为 uGUI 或 UI Toolkit；恢复任务前先重新确认范围和视觉目标。

## WhereIsThing

- [ ] 在 PEAK `2.4.b` 由房主和客户端分别验证普通绳索、反重力绳索、绳索炮、反重力绳索炮、岩钉、踏板菇、弹力菇、云雾菇、检查点旗、童子军大炮和锁链发射器；目标应在生成后约 0.5 秒内出现，可确认 owner 时显示正确玩家名。
- [ ] 验证 owner 负例：野生踏板菇/弹力菇/云雾菇不得错误显示房主；候选不唯一或超时的蘑菇只显示名称和距离；魔豆始终不猜测玩家名。
- [ ] 验证系统目标负例：机场绳索、神庙入口绳索、`PeakSequence` 绳索、`BreakableRopeAnchor` 及海滩 `BreakableBridge` 不得被识别成玩家绳索或锁链发射器。
- [ ] 验证目标销毁、拾取/丢弃、场景切换、玩家离开和主机迁移，确保标签、投掷证据和缓存正确清理，不残留错误 owner。
- [ ] 验证玩家名全局开关、预设选择/共享、选择窗口范围滑块、紧凑布局、中文字体、常驻/计时显示和 C/Alt+C 输入行为。
- [ ] 验证分类窗口：Jetpack/Rocketpack、Heat Pack、Healing Dart、Healing Puff Shroom、Early Worm、Beehive、Honeycomb、Scorpion、棋子和全部放置工具是否显示在预期分类；确认同名变体及场景目标没有重复入口。
- [ ] 可选：相同地图和预设下记录至少 60 秒 WhySoLaggy 日志，确认没有固定约 0.5 秒扫描尖峰、WhereIsThing 平均低于 `0.2ms/frame`，且没有插件单次扫描造成的 50ms 以上帧。
- [ ] 实机验收前保持 `0.1.2` 测试状态，不更新 `发行/1.0.3`，不创建 ZIP。


