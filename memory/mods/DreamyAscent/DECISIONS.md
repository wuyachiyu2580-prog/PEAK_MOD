# DreamyAscent Decisions

更新时间：2026-05-20

## 已确认决策

- 版本号固定为 `0.1.0`，发布前不自动更新。
- 这是独立 MOD，不包原 TerrainCustomiser DLL。
- 不基于 `Map Saves` 文件夹做兼容层。
- 改名后的工作树以 `MOD开发\DreamyAscent` 和 `memory\mods\DreamyAscent` 为唯一新入口；旧 `TerrainCustomiserCN` 路径只作为 Git rename 的历史来源和旧运行时数据迁移来源，不再作为开发入口。
- 如果再次看到旧 `TerrainCustomiserCN` 删除、新 `DreamyAscent` 未跟踪的状态，优先判断为改名索引未收口；不要恢复旧目录，也不要删除新目录。应只整理 Git 索引，让它识别 rename。
- 预览使用 RenderTexture，不再使用直接 `Camera.rect` 分屏输出。
- RenderTexture 预览渲染应隔离全局雾效：渲染前临时关闭 `RenderSettings.fog`，渲染后恢复；预览相机使用纯色清屏，避免继承 Skybox/fog 造成白雾污染。
- RenderTexture 预览渲染应隔离天气视觉：渲染期间临时禁用 `StormVisual` 子 Renderer，渲染后恢复；不要关闭天气系统本身，也不要修改地图 `enableSnowStorm` / `enableRain` 设置。
- 预览开启期间可以临时关闭游戏原生雾系统 `Misc/Post Fog`、`Post Fog`、`FogSphereSystem`，关闭预览时必须恢复。这来自原 TerrainCustomiser 的做法，不是随意隐藏场景对象。
- F1 打开时不直接隐藏游戏 Canvas，避免输入和 ESC 状态异常。
- 预览位置不再继续靠硬编码猜关卡位置；允许用户在 F1 预览中用 WASD/Shift/Space/Ctrl 微调视角，并用 `F6` 保存当前区段位姿到 `DreamyAscent PreviewPoses.json`。不要额外扩展 F5/F7/F8/F9 等快捷键。
- XYZ 属性拆分成独立输入。
- 动态英文名称通过诊断收集后再补中文映射。
- 区段模板库里的 prefab/material 显示名优先走 `DaLocalization.TranslateOrOriginal` 静默兜底；常见编号名用规则翻译，避免同类 prefab 逐个硬编码且避免 `Missing localization mapping` 刷屏。
- 当前生成器范围高亮只表达 LevelGenStep 的生成范围，不表达未来手动放置子区；选中 Step 使用半透明实心矩形面 + 外框/中心标记，以保证看得清；未选中的已修改 Step 仍用橙色线框，避免画面过满。
- 中文本地化主表改为外置优先：`DreamyAscent Data\localization.zh-CN.json` 是主来源，DLL 内字典只保留 fallback，不再继续扩成唯一主表。
- 外置 JSON 读取必须以插件目录为基准拼接路径，并允许上一级目录兜底，兼容 DLL 放在 `plugins\` 或 `plugins\DreamyAscent\` 子目录的两种部署方式。
- 当前 UI 结构暂时收口为三层联动 + 左下参数说明方案；除非复现回归，不再继续扩 UI 布局和交互。

## 后期物品编辑架构决策

- 当前 DA UI 重构的结构面板方向已经确定为三层联动：关卡（Segment）横排选择、区域/生成组（Grouper）横排选择、当前区域生成物/步骤（Step）列表。切换上层必须实时刷新下层，并让右侧参数与场景高亮跟随当前 Step；不要再回到旧的深缩进树形按钮作为主导航。
- 当前生成物列表显示的是原版 `LevelGenStep` 与 catalog 诊断摘要，不等于未来手动放置物列表，也不等于重叠检测结果。后续若要判断区域内容是否重叠，应继续补 step path、catalog 明细、子区摘要或诊断统计，而不是在 UI 里把诊断联动误写成实际生成逻辑。
- 后续地图编辑需求必须先对照 `IMPLEMENTATION_MATRIX.md` 拆工作面，再决定改哪一层。官方模板、空白自定义、当前区段自选、跨区段放置、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照和多人同步是不同问题，不要再合并成“模板库可混合”或 `RunSegment` 单点特判。
- 地图生成相关改动必须先按三层归类：`OfficialTemplate` 只编辑/重跑原版生成器；`CustomBlank` 清理官方普通生成物并执行 DreamyAscent 自定义规则；模板库/外部资源/材质/多人同步走独立对象注册和放置规则。不要继续把所有问题塞进 `RunSegment` 的单点特判。
- 后期 UI 必须以区域（Segment）为第一层边界；区域下再管理生成组、生成步骤、手动放置物和材质规则。
- “区段模板库”和“放置子区”必须分开：`ObjectCatalog` 只表示某个 Segment 里可复用的模板/材质，不代表玩家要编辑的山顶、山腰、洞口等 XYZ 子区域。后续可添加物品 UI 应先选择/创建子区域，再从区段模板库挑模板放入该子区域。
- 后期每个地图组合/变体应内置一份干净模板快照。当前 live scene 扫描只适合诊断和运行时绑定，不适合作为唯一长期模板来源，因为它会被 `CustomBlank`、官方重跑、变体切换和重复生成污染。
- 官方模板模式调用原版生成器前不得预清理原版层级。反编译依据：`DecorSpawner` 依赖 `spawnPoints`，`DesertRockSpawner.Clear()` 依赖 `Enterences` / `Inside`，父子生成器依赖父物体层级；预清理会造成 NRE、空心或恢复不完整。
- 后期编辑模式至少分为两条路径：`OfficialTemplate` 保留原版各区段生成器并编辑其参数；`CustomBlank` 让各区段默认不继承官方物品，只执行用户自己添加的生成规则。后续可加 `Hybrid`，即保留官方生成器并叠加自定义规则。
- 官方模板生成不要再恢复“额外 late-step 手动补跑”方案。原版 `PropGrouper.RunAll(bool)` 已经负责 early / late / deferred steps，DreamyAscent 只保留原版入口和必要的过滤，不要把 late steps 再强制跑一遍。
- “自己选区段物品”只是官方模板/全局模板复用方向之一，不等于完整目标；另一个同等重要目标是从空白区段开始创造地图内容。
- 空白自定义模式不能依赖已有 `LevelGenStep` 作为唯一编辑载体，因为区段为空时没有可编辑 Step；必须新增自定义生成规则/手动放置规则数据层。
- 空白自定义模式必须显式记录 ownership：DreamyAscent 生成物、原版结构、原版机制对象和外部资源分开处理。Roots 的地形/水体/墙体 splitmesh、Caldera/Volcano 的岩浆机制对象不能默认当普通装饰清理。
- 区段模板库后期应改造成“全局模板注册表 + 当前 Segment 默认过滤 + 目标放置子区兼容规则”。允许跨区段混合选择模板，但不要把各 Segment 的 item/material 直接合并成无上下文列表。
- 模板归属 Segment 只表示来源和默认参数，不表示唯一可放置位置；真正决定生成结果的是目标子区的 `area/ray/raycast/layer/constraints`、落地策略和兼容警告。
- 跨区段放置必须采用“来源模板 + 目标子区”模型：来源模板提供 prefab、默认参数、组件和风险；目标子区提供 XYZ、范围、ray、layer、落地面和约束。禁止直接把来源区段的 `PropGrouper/LevelGenStep` 拿到目标区段运行。
- 第一批跨区段可执行模板只允许低风险普通物体：无 `PhotonView`、无子 `LevelGenStep`、无 `SingleItemSpawner`、无 AI/物理/全局机制。Jungle `Jungle_PalmTree_*` 放到 Desert 平台是首个推荐验证例子；Roots `Redwood`、行李、藤蔓、虫类、岩浆机制默认只读标记。
- `ObjectCatalog.json` 当前只读索引不能直接当可执行模板库。后续必须抽出正式 `DaObjectRegistry`，记录稳定模板 ID、来源 path、加载方式、组件摘要、PhotonView 风险、默认参数和资源缺失行为。
- 后期模板库应从当前参数窗口拆成独立窗口或固定侧栏。参数页负责当前 Step/子区/生成规则；模板库窗口负责搜索、分类、全局/当前区段过滤、右键添加或拖拽添加。
- 添加流程优先设计为：先选中 Segment 下的放置子区，再从模板库右键“添加到当前子区”或拖到预览/高亮区域，添加后在参数面板编辑数量、缩放、随机旋转、材质覆盖和清理策略。
- “编辑已有生成器参数”和“新增/外部导入物品”要分成两条数据路径：前者继续使用 `LevelGenStep` 属性编辑，后者新增对象库/放置清单，不要把 prefab/material 引用硬塞进当前 `DaPropertyData.Value`。
- 绑定物关系必须显式建模为依赖图或父子模板，例如 `PalmTree -> Coconut`；生成、移动、缩放、删除、导入导出和多人同步都要按组处理。
- 父子依附生成优先参考原生 `PSM_ChildSpawners`：先生成父物体，再执行父物体子层级里的 `LevelGenStep`。不要手写平级散点模拟椰子挂树。
- 外部 Unity 物品导入必须先走资源注册表：记录来源、稳定 ID、显示名、加载方式、默认缩放、碰撞/落地策略和依赖关系。不要依赖场景临时 instance id 作为存档键。
- 材质/颜色编辑优先做实例级覆盖，避免改 `sharedMaterial` 污染全场同源物体；保存时记录 renderer 路径、材质槽位、颜色/材质 key 和是否递归作用子物体。
- 原生 `PSM_SetMaterial*` 可作为材质规则参考，但它直接写 `sharedMaterial`；DreamyAscent 的运行时 UI 不直接照搬这个写法。
- `CustomBlank` 第一版的语义是“跳过官方生成器并清理该区段已生成的官方子物”，不是“把整段场景绝对清空”。像 Caldera 的 `River`、Volcano 的 `RisingLava/Lava` 这类基础机制物会保留，后续若要做绝对空白模式再单独设计开关。
- 当前如果“生成本段”后只剩地形，优先先判断当前编辑模式是不是 `CustomBlank`。`CustomBlank` 本身就不会跑官方 `PropGrouper`，因此只剩地形可能是模式语义而不是生成失败。
- `CustomBlank` 不能无脑清掉承载地形骨架的生成组。当前先以白名单保留 `Desert_Segment` 的 `Platteau` 底板本体，但会清掉 `Platteau/Start` 子物和内嵌 `Canyon` grouper 生成物；不再保留顶层 `Rocks`，否则空白自定义会留下过多官方岩石。Caldera 按用户要求只保留岩浆机制，`LavaRivers` / `Rocks` 等官方生成组不再保留。后续可继续细分结构岩、装饰岩和机制对象。
- 官方模板模式调用 `PropGrouper.RunAll(true)` 前不要由 DreamyAscent 先手动 `Destroy` 子物。Desert 的 `DesertRockSpawner.Clear()` 会依赖原有层级状态，提前清理会导致 NRE 和空心化；自定义空白模式才使用 DreamyAscent 的清理逻辑。
- 1.62.a 当前反编译和实机日志表明，DreamyAscent 的官方生成入口不能只依赖公开 `PropGrouper.RunAll(true)` 完成 Late 内容：它会清理全部 step，但公开路径只稳定执行 Early；Late 必须由 DreamyAscent 在 `RunAll(true)` 后补跑。补跑 step 收集必须手动沿 `Transform.parent` 查找最近 `PropGrouper`，不能依赖 `GetComponentInParent<PropGrouper>()`，否则 inactive 父层级下 Jungle `Pops_Plat` / `Props_Wall` 会收集到 0 个 Late step。
- Segment fallback 扫描不能导出 inactive 互斥变体下的 `PropGrouper`。Roots/Jungle 这类地图可能没有可直接识别的 active `BiomeVariant`，但 whole segment 下会挂多个 `- xxx Variant` 分支；不要用全局 `activeInHierarchy` 硬过滤，因为会导致 Roots/Jungle 或直接根段扫不到。当前策略是只排除 inactive 且名称匹配 `- xxx Variant` 的分支。
- 2026-05-13 全量样本审计确认：`Beach / Jungle / Roots / Snow / Desert` 的所有当前已知变体均已有导出样本覆盖；`BiomeVariant` 与 `VariantObject` 都能通过 `normalizedVariantName`、`activeVariantNames`、`activeVariantPaths` 和完整 `hierarchyPath` 判断当前激活分支。后续判断变体污染时，不要只看 step 名。
- `Desert` 的 `ScorpionsHell`、`TumblerHell` 以及 `Jungle/SkyJungle` 样本中的少量 `Lava/Pillars/Ivy` step 名可以出现在当前激活层级内，它们属于官方共用生成步骤或资源命名；只有未激活 variant root 整支泄漏才应判定为污染。
- `TerrainRandomiser` 样本只作为变体识别和纯净导出验证样本，不混同为官方自然跑图样本；但它们可以用于补齐官方自然样本很难遇到的 variant 覆盖缺口。
- `data/map-data/sample-index.json` 是样本覆盖和完整性检查的机器可读入口；2026-05-17 已更新为 Snapshot V2 真实计数。后续新增样本或变体规则变化时，应同步更新该索引，并让回归工具优先读它，而不是只解析 Markdown 审计记录。
- Snapshot V2 原始 `GeneratedChildrenSnapshot.json` 不做瘦身。它们合计约 8.87GB，但承载官方地形重建所需的已生成子物、关系候选、关键组件字段和脏样本证据；若需要轻量数据，只能从原始诊断离线派生，不能删原始字段后再要求完整重建。
- 运行时不要在启动或 UI 打开时自动加载开发期离线资产。`template-snapshots.json` / `object-registry-input.json` 只在用户手动打开样本资产或注册表候选时加载；诊断写出和离线 JSON 读取优先使用流式 API，避免大字符串或大文件一次性进内存。
- Roots 官方整段生成要跳过 `PlateauRocks` / `WallRocks`，否则会生成不该存在的乱石。`CustomBlank` 是运行时清理，不保证能切回 `OfficialTemplate` 后完整恢复原版依赖层级；UI 只提示限制，建议重开或新图复测，不再把自动恢复作为承诺。
- Jungle 官方整段生成同样要跳过岩石组 `Rocks_Plat` / `Rocks_Wall`，否则在已有地图上重跑会把平台/墙体岩石再次堆叠。当前先保留 `Pops_Plat` / `Props_Wall` 的官方重生成；若后续发现 `Rocks_Wall` 内 `Waterfalls` 需要保留，应改做 step 级白名单，而不是恢复整组岩石生成。`Pops_Plat` / `Props_Wall` 都是 Late grouper，必须依赖 inactive-safe 的 Late supplement 才能重生灌木、树、藤蔓、蘑菇和 runtime item spawner。
- 生成入口不能假设 `DaTerrainData` 一定保有 Unity 运行时引用。导入 JSON、重新扫描、预览隐藏/恢复、`CustomBlank` 清理后，都可能让 `SourceObject` 为空或旧引用失效；执行“生成本段/生成本组/参数修改后自动生成”前必须允许按当前场景 segment/grouper/step 名称重新绑定运行时引用。
- Jungle 是否“不显示”必须以当前地图组合确认；如果日志/诊断中没有 `Jungle_Segment`，不能把 Roots 或 Tropics 组合误判为 Jungle 扫描失败。
- 若复用游戏内现有物体作为模板，优先从当前场景扫描 prefab-like 原型并建立稳定路径/名称索引；若要加载外部 asset bundle，需要先补 Unity asset bundle 加载和版本兼容资料。
- 多人同步可参考 TerrainRandomiser：主机同步设置/种子，生成时收集需要同步的 `PhotonView` ID，再分发给客机补齐。
- HazardSpam 可作为后期地图生成交互和同步设计参考：`HazardTemplateManager` 将 prefab 模板和 `(Zone, SubZoneArea)` 放置区域分开，`HazardManager` 复制目标区域 `PropSpawner` 参数生成新 spawner，`NetComm` 由主机广播 spawner 标识和 positions/rotations。DreamyAscent 可借鉴这个分层，但不能只按危险物类型照搬。
- 反编译原版 `PropSpawner` 说明生成核心是 `area`、raycast、constraints、modifiers、props 和 post constraints；因此 UI 的“放置子区”应最终映射到这些生成参数或新增手动放置规则，而不是只保存一个中文区域名。
- 后续多人自定义生成采用主机权威：主机决定配置/种子/positions/rotations/必要 PhotonView ID，再广播给客机。TerrainRandomiser 的 room property + ViewID 分发和 HazardSpam 的 positions/rotations RPC 是主要参考。
- 跨区段放置和示例的专门记忆放在 `CROSS_SEGMENT_PLACEMENT.md`；后续实现前必须先读该文件，避免再次漏掉“别的区段物品如何放到当前区段”的流程。

## 禁止回退

- 不要恢复旧 `Camera.rect` 预览路径。
- 不要把 Canvas 隐藏作为默认开窗行为恢复。
- 不要把临时待办继续写进根目录散文件。
- 不要把有父子依赖的物体当作普通独立散点生成；否则椰子、树、地面约束会失配。
- 不要直接修改共享材质；材质变色必须使用实例材质或可回滚的 MaterialPropertyBlock/复制材质策略。
- 不要把 `area` 再画成立体盒子或倾斜斜面；这会被误解成可编辑放置区域。
- 不要承诺 `CustomBlank -> OfficialTemplate` 能自动恢复官方原状；需要重开或新图验证官方模板。
- 不要把当前场景 `UnityEngine.Object` instance id 当存档键或模板 ID；换图、重进、客机会失效。
- 不要把 Roots/Jungle 的岩石组保护性跳过发展成无限特判；长期改为模板快照、稳定 path 和 step 级规则。
- 不要恢复“发现外部 `PropGrouper.RunAll` postfix 就跳过 DreamyAscent Late supplement”的判断。实测证明外部 postfix 不保证在 DreamyAscent 的“生成本段”路径里补回 Jungle Late 内容。
- 不要恢复失败的 post-generation material modifier replay 或 custom placement child-scale propagation 作为 Beach 材质修复；用户已要求退回该版本，材质问题需要以后重新基于 renderer/material 诊断设计。

## 可参考但不能照搬

- TerrainCustomiser 反编译代码可参考 UI、生成刷新、布局和性能处理。
- TerrainRandomiser 反编译代码可参考多人同步和中途加入处理。
- HazardSpam 源码和反编译代码可参考 Zone/SubZone、模板注册、跨区段模板选择、主机生成位置再广播的结构。
- 参考代码必须改造成 DreamyAscent 自己的结构和风格。


