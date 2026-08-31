# Memory Changelog

## 2026-08-31

- [新增] `mods/OldPC/` 四件套：记录 0.0.1 本地旧电脑视觉模式、望远镜清晰模式、网络边界和未完成验收。
- [修改] `mods/WhereIsMyAmulet/` 四件套：更新为 1.0.3，记录 Scout Statue 槽位/类型映射修复、距离文字间距、实际 profile 路径、发行 DLL hash 和惊喜模式未实现状态。
- [修改] `mods/WhySoLaggy/` 四件套：更正 1.0.4 已建立发行包，补充发行 DLL/ZIP hash、18 项测试源码现状和未提交本地化测试改动风险。
- [新增] `mods/WhereIsMyAmulet/temp/2026-08-31.md`、`mods/WhySoLaggy/temp/2026-08-31.md`：保存当前版本、发行产物和剩余验收入口，并修正旧临时记录中的过时 hash/测试数量表述。
- [修改] `README.md`、`mods/README.md`、`MEMORY_INDEX.md`、`TODO.md`：加入 OldPC，更新 WhereIsMyAmulet/WhySoLaggy 状态，并保留 WhereIsThing 原样不动。

## 2026-08-29

- [修改] `mods/WhySoLaggy/FILES.md` / `RECENT.md` / `temp/2026-08-28.md`：构建输出改为 r2modman `2.0.a` profile，并记录 1.0.4 DLL 版本、大小和哈希。
- [修改] `mods/WhySoLaggy/README.md` / `RECENT.md` / `DECISIONS.md` / `FILES.md`：同步 1.0.4 的 PEAK 2.3.a 全量修复、版本链、网络归因、日志/Profiler/生命周期决策、测试结果和建议开关。
- [新增] `mods/WhySoLaggy/temp/2026-08-28.md`：记录 1.0.4 当前停点、禁止回退项和双客户端实机验收入口。
- [修改] `TODO.md`：移除已完成的默认开关调查，新增 1.0.4 双客户端归因、队列、单记录和卸载重载验收待办，并锁定验收前不发布。
- [索引] `README.md` / `MEMORY_INDEX.md` / `mods/README.md`：WhySoLaggy 更新为 1.0.4 开发维护、PEAK 2.3.a 基线和待双客户端验收状态。
- [规则] 当前会话未提供专用 `update_memory` 工具；已完成所有可用的本地 MD、索引、TODO、临时记录和变更日志同步。

## 2026-08-20

- [WhereIsThing] 完成 `发行/0.1.1` 非 ZIP 发行目录，包含 DLL、README、CHANGELOG、manifest 和 icon；发行 DLL 与 `2.0.a` profile 产物版本/hash 一致。
- [WhereIsThing] 0.1.1 发行说明补充护符雕像碎片范围和 ModConfig 安全本地化修复。

## 2026-08-19

- [新增] `common/08_ModConfig本地化与安全集成规范.md`：统一说明 section/key 本地化、禁止全局缓存重建、生命周期、迁移步骤和验证标准。
- [修改] `mods/WhereIsThing/DECISIONS.md`：更正 ModConfig 决策，明确只刷新自身描述和当前 UI，不得刷新全局注册缓存。
- [修改] `MEMORY_INDEX.md` / `TODO.md`：加入 ModConfig 安全规范入口和其他 MOD 迁移待办。

## 2026-08-18

- [修改] `mods/WhereIsThing/`：清理已完成的发布前素材准备记忆，同步 0.1.0 已发布状态、发行包路径和后续反馈收口重点。
- [修改] `README.md` / `MEMORY_INDEX.md` / `mods/README.md` / `TODO.md`：WhereIsThing 从“待发布”改为“0.1.0 已发布”，移除发行包制作待办。
- [删除] `mods/WhereIsThing/temp/2026-08-18.md`：删除仅用于发布前素材准备的临时记录，避免后续误当待办。
- [规则] 当前会话未提供专用 `update_memory` 工具；已按本地 MD、索引、TODO 和变更日志完成可用同步。

## 2026-08-17

- [PlayersInfo] 完成 `发行/0.2.1` 非 ZIP 发行目录：包含 DLL、manifest、简洁完整的 README/CHANGELOG 和 icon；发行 DLL 与 profile 产物版本/hash 一致。
- [PlayersInfo] 根据清晰截图定位最后的重叠来自队友 `BarAffliction.isPetrify` 克隆，而非 `extraBar*` 本体；队友不再克隆石化上限图形，普通异常条保留，石化后的额外体力上限继续通过右侧 `current/cap` 分母显示，并重新构建部署 `0.2.1`。
- [PlayersInfo] 用户确认红框内是本机主体力条与队友图形额外体力条重叠，不是右侧 `current/cap`；现在仅从队友克隆树内按字段和明确 `Extra*` 名称移除额外条、外框、图标和光晕，保留 `current/cap`、主体力条、异常条、物品栏和喷气背包燃料条，并重新构建部署 `0.2.1`。
- [PlayersInfo] 修复部分克隆把本地原版 `BarAffliction` 当作队友组件销毁，导致额外体力物品在 `GUIManager.bar.ChangeBar()` 中断、进度完成却不生效的问题；新增克隆根所有权检查和队友异常条显式复制，并完成 `0.2.1` 重新构建部署。
- [PlayersInfo] 本地额外条已恢复原版布局后，将整体 HUD 的 `Display.OffsetY` 默认值恢复为 `0`，并删除底部锚点 `0 -> 138` 的自动迁移；已有用户自定义偏移不覆盖。
- [PlayersInfo] 额外条实机确认完成后，移除临时 `[LocalExtraRuntime]` 采样和未调用的手动尺寸接管方法，恢复安静日志。
- [修改] `mods/PlayersInfo/README.md` / `RECENT.md` / `DECISIONS.md` / `FILES.md`：记录本地额外体力条恢复 PEAK 原版层级和运行时缩放控制，当前值只在绿色填充内部显示；明确观战目标、队友额外图形条抑制和紧凑喷气背包燃料条均保留。
- [新增] `mods/PlayersInfo/temp/2026-08-17.md`：保存本轮源码证据、方案取舍、构建部署状态和实机验证入口。
- [修改] `TODO.md`：新增本地额外条缩放/黑边/闪电图标/石化 containment/current-only 数值、观战目标、队友额外条隐藏及燃料条回归验证，并记录验证后清理临时日志。
- [索引] `README.md` / `MEMORY_INDEX.md` / `mods/README.md`：同步 PlayersInfo 2026-08-17 当前状态和最新临时记忆入口。
- [规则] 当前会话未提供专用 `update_memory` 工具；已完成所有可用的本地 MD、索引、TODO 和变更日志同步，后续若工具恢复需补同内容的内存实体更新。

## 2026-08-16

- [PlayersInfo] 最终确定额外体力显示规则：本地额外条固定在主体力条下方并修正外框/填充重叠，去掉独立的 `40/100` 外层文字，内部正常与石化数值保留；队友主体力条、主体力数值和异常条保留，额外图形条不显示，仅在安全侧显示无 `+` 的 `当前/上限`（含 `0/上限`）。底部 HUD 默认 `OffsetY=138`，仅迁移旧的精确零值。
- [PlayersInfo] 同步 0.2.1 最新实现到 memory：稳定 ID 固定绑定队友体力条、统一 `observedCharacter -> localCharacter` 显示目标、独立 `TeammateBarAffliction`、本地饥饿倒计时、队友耐久进度条、熟食图标颜色和 TMP 实际宽度布局。
- [PlayersInfo] 同步队友背包类型显示：根据实际容量显示无背包/两格滑稽背包/四格普通背包，新增可选喷气背包燃料显示；沿用 `Display.EnableInventoryRow` 配置键，将旧布尔值迁移为 `Disabled` / `ContentsOnly` / `ContentsAndJetpackFuel`，不影响其他配置。
- [PlayersInfo] 更正并落实 DLL 输出规则：`PlayersInfo.csproj <OutputPath>` 已改为 `C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\`，后续 PlayersInfo 编译直接写入 profile，不再输出到测试环境或依赖手动复制。
- [规则] 更新 `common/02_工程与构建规范.md`：以后实际使用某个 MOD 时再将其 DLL 输出切换到 2.0.a profile；本轮不批量修改其他 MOD 的项目文件。

## 2026-08-14

- [新增] `WhereIsThing`：建立 PEAK 2.1.a 多物品位置显示 MOD 的 memory 四件套和临时恢复入口，记录动态 ItemDatabase、游戏内 Localization、类别初稿、快捷键、窗口边界和实机待办。
- [新增] `MOD开发/WhereIsThing`：完成 0.1.0 基础工程、物品选择窗口、实时标签、常驻/计时显示和配置持久化；Release 构建通过，0 warnings / 0 errors，尚未制作发布包。
- [修改] `WhereIsThing` 构建输出：从工作区测试环境切换到 `r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins`，并同步使用该 profile 的 BepInEx 引用。
- [修改] `WhereIsThing`：合并同名 itemID 组，接入 `Luggage.ALL_LUGGAGE`，新增地面/手持/背包/行李箱范围复选框，细化分类，改为自适应多列窗口，并在窗口存续期间接管鼠标指针；Release 构建继续为 0 warnings / 0 errors。
- [修改] `WhereIsThing`：目录首次加载时将旧配置命中的单个同名变体扩展为完整选择组，避免旧配置出现显示组与实际扫描不一致。

- [PlayersInfo] 修复 0.2.0 发布前两个 HUD 问题：本地额外体力条不再被队友条布局组排到面板底部，改为跟随本地主条安全侧；死亡状态清空并隐藏本地/队友体力、额外体力和异常百分比文字，避免旧数值重叠。Release 构建 0 警告 0 错误；loose DLL 已更新，现有 zip 仍是修复前版本且按要求未重打。
- [PlayersInfo] 准备 0.2.0 发布：默认 HUD 锚点改为左下角；旧版保存的左上角配置启动时迁移到左下角，其余锚点保留；版本链、发行说明和 loose release 文件已同步，未生成 zip；Release 构建 0 警告 0 错误。
- [新增] `common/07_PEAK版本与反编译基线.md`：记录 PEAK 2.1.a 相对 2.0.a 的 16 个业务级变化、现有 MOD 兼容结论和 AntiSphere/邀请/存档待验证风险；同步入口、索引和根 TODO。
- [PlayersInfo] Changed local and teammate extra-stamina text to show current/cap (`+current/cap`), using the game's petrify-aware cap formula. Release build passed 0/0; release package awaits in-game verification.
- [PlayersInfo] Reviewed PEAK 2.1.a source and the installed game assembly. HUD, stamina, petrify, character sync, and inventory APIs used by PlayersInfo remain compatible; baseline Release build passed 0/0, with no source change required.
- [PlayersInfo] Reviewed PEAK 2.0.a decompiled changes and updated compatibility: petrify bars read `CharacterData.petrifyAmount`, extra stamina keeps the game's petrify-clamped value, teammate inventory uses the new `BackpackSlot.IsEmpty()` API, and a default stable teammate-bar ordering mode was added. Release build passed 0/0; release package awaits in-game verification.

## 2026-07-30

- [PeakMapBrowser] 发布 `0.1.1`：同步 `.csproj`、BepInEx 插件版本、发行 manifest、README、CHANGELOG 和 API 文档；构建 `0` warnings / `0` errors，生成 `wuyachiyu-PeakMapBrowser-0.1.1.zip`。
- [PeakMapBrowser] 记录 DPAPI 加密 refresh token、内存 access token、自动刷新和服务端退出接口；线上 `https://peakmap.top/api/auth/sign-out` 已验证存在，空请求返回 `401` 和 `Cache-Control: no-store`。

## 2026-07-23

- [新增] `mods/PeakMapBrowser/`：补齐 README / RECENT / DECISIONS / FILES 和 `temp/2026-07-23.md`，同步账号、个人地图、上传归属、点赞规则、当前 UI 状态和构建输出。
- [规则] `PeakMapBrowser`：记录用户明确暂缓 UI 框架重构，当前继续使用 IMGUI；后续 UI 方案只作为待评估的 uGUI + TextMeshPro，不得自动实施。
- [修改] `memory/README.md` / `mods/README.md` / `MEMORY_INDEX.md`：加入 PeakMapBrowser 入口、读取顺序和最新临时记忆路径。
- [修改] `TODO.md`：新增 PeakMapBrowser 的实机验证、线上迁移确认和后续发布待办。

## 2026-06-04

- [PlayersInfo] 更新记忆状态快照：新增 `mods/PlayersInfo/temp/2026-06-04.md`，记录 0.1.1 当前 DLL 已含 2026-05-24 临时体力数字裁切修复、2026-05-30 发布文档已同步。
- [PlayersInfo] 同步更新 `mods/PlayersInfo/README.md` / `RECENT.md` / `FILES.md` / `TODO.md` 和根 `TODO.md`：确认 `发行/0.1.1/wuyachiyu-PlayersInfo-0.1.1.zip` 已存在，大小 `158802`，zip 内 README/CHANGELOG 含临时体力裁切修复说明，发布 DLL 与测试环境 DLL 哈希一致。
- [索引] 同步更新根 `README.md`、`MEMORY_INDEX.md` 和 `mods/README.md`：PlayersInfo 当前状态从旧 0.1.0/5-21 摘要更新到 0.1.1 最新记忆入口和已打包发布状态。

## 2026-05-30

- [TerrainCustomiserCN] 更新四件套到 `0.1.2` 发布状态：记录对应原版 `TerrainCustomiser 0.3.2`、主 DLL 版本纠正、Collector `0.1.1`、发布包路径和构建 0/0。
- [TerrainCustomiserCN] 记录原版 0.3.2 同步项：地图保存目录改为 `%USERPROFILE%\AppData\LocalLow\LandCrab\PEAK\TerrainCustomiser\Map Saves`，并同步 Caldera/Volcano 自定义变体修复。
- [TerrainCustomiserCN] 记录 `.json.old` 策略：只在机场开始游玩列表中只读显示和读取，编辑器与 `MapSerializer.SaveFiles` 仍只处理正常 `*.json`，不自动复制、改名、删除或迁移。
- [TerrainCustomiserCN] 记录最新漏翻处理：`Spires -> 尖塔`，Collector 继续只输出 `TerrainCustomiserCN_missing_translations.tsv`。
- [新增] `mods/TerrainCustomiserCN/temp/2026-05-30.md`：作为本轮 0.3.2 差异复核、0.1.2 同步、保存路径和发布文档状态的压缩恢复入口。
- [索引] 同步更新根 `README.md`、`MEMORY_INDEX.md`、`mods/README.md` 和 `TODO.md`：TerrainCustomiserCN 当前状态从 0.1.0 更新为 0.1.2，对应原版 0.3.2。

## 2026-05-24

- [DreamyAscent] 按用户要求将 DreamyAscent 标记为永久暂停/归档：新增 `mods/DreamyAscent/temp/2026-05-24.md`，并更新 DA `README.md` / `RECENT.md` / `DECISIONS.md`，说明除非用户明确恢复，否则不再继续功能、日志、构建、诊断或 TODO。
- [索引] 同步更新根 `README.md`、`MEMORY_INDEX.md`、`mods/README.md` 和 `TODO.md`：DreamyAscent 从当前重点改为暂停/归档，旧待办冻结为历史资料。

## 2026-05-23

- [新增] `mods/TerrainCustomiserCN/`：补齐 README / RECENT / DECISIONS / FILES 四件套和 `temp/2026-05-23.md`，记录 TerrainCustomiserCN 0.1.0 中文 UI 版、对应原版 TerrainCustomiser 0.3.0、联机/存档兼容策略、Collector 漏翻收集和发布包状态。
- [索引] `README.md` / `MEMORY_INDEX.md` / `mods/README.md`：加入 TerrainCustomiserCN 入口、临时记忆路径、当前发布状态和后续接手顺序。
- [修改] `TODO.md`：新增 TerrainCustomiserCN 后续漏翻、原版版本对应、第四关原版 BUG、发布包检查和原版更新复核待办。
- [澄清] `TerrainCustomiserCN`：明确当前项目是 2026-05-23 新建中文 UI 版，不要和 2026-05-12 的旧 `TerrainCustomiserCN -> DreamyAscent` 改名历史混淆。

## 2026-05-21

- [PlayersInfo] 修复配置审查发现的问题：`EnableStaminaBar` 现在真实控制队友条，`Anchor` / `OffsetX` / `OffsetY` 现在应用到 HUD 布局，RetainOnLoss 不再突破 `MaxNearbyCount`，coordinator 销毁时解绑 tracker 事件。配置分区合并为 `Display` / `Advanced`，启动时迁移旧分区值；新增可选 `ModConfigLocalization`，检测到 PEAKLib.ModConfig 且语言为中文时本地化配置分区/选项/描述，无 ModConfig 时静默跳过。Release 构建 0/0，DLL 输出到测试环境，更新 PlayersInfo memory 与 0.1.1 发行 README/CHANGELOG，未重打 zip。
- [DreamyAscent] 今日任务收尾并新增压缩恢复入口：创建 `mods/DreamyAscent/temp/2026-05-21.md`，记录官方 `Generate Segment` 零输出保底恢复、构建部署状态、下一次 Jungle/Roots/Snow 复测日志字段，以及 Beach 地形材质仍未解决且不得恢复失败旧方案。同步更新根 `README.md`、`MEMORY_INDEX.md`、`TODO.md`、DreamyAscent `README.md` / `RECENT.md` / `DECISIONS.md` / `FILES.md` 日期和索引。

## 2026-05-20

- [DreamyAscent] 官方 `Generate Segment` 增加零输出保底恢复：selected PropSpawner/Line/Sphere 执行前先把直接子物体移入隐藏备份根，执行后仅对“原本有子物体、生成后为 0”的 step 恢复旧物体，防止 Jungle/Roots/Snow 被清空；同时新增 `zero-output PropSpawner diagnostics`，记录 ray/constraint/material 采样。Release 构建 0 警告 0 错误，DLL 已覆盖 terrain profile，大小 `421376`。
- [DreamyAscent] 根据最新日志继续收窄官方 root pipeline：完整异常关键词没有出现，说明 Props 为空不是 catch 吞异常，而是 `PropSpawner.Execute()` 正常执行后生成 0。`RunOfficialSegmentRootLevelGeneration()` 现只执行 `groupersToGenerate` 导出的 selected steps，不再从 segment root 扫 `activeSteps`；按 owning `PropGrouper.timing` 分 Early/Late，并在清理后、Early/Late 之间 `Physics.SyncTransforms()`。临时输出目录构建 0/0；正式覆盖 r2modman profile 失败仅因 PEAK 仍运行并锁定 `DreamyAscent.dll`，需关游戏后重跑 Release build。
- [DreamyAscent] 按最新日志继续加固诊断：root-scoped pipeline 已加载，但 Snow/Roots 仍存在 prop groups 归零且没有可用 caught exception stack。`DaRuntimeEditService` 生成链相关 catch 已从 `ex.Message` 改为完整 `Exception.ToString()`，反射 `TargetInvocationException` 会展开内部异常堆栈；LevelGen clear/execute/go 和 grouper validate 的 OnceWarn key/message 加入 hierarchy path，避免同类型 `PropSpawner` 只记录第一处。Release 构建 0/0，DLL 大小 `410112`。
- [DreamyAscent] 修复官方 `Generate Segment` 的 piecemeal child-grouper 生成问题：复查确认运行时加载的是新版 Snapshot V2，JSON 不缺 Jungle/Roots/Snow 的生成步骤；真正风险是逐个 child `PropGrouper.RunAll(true)` 会让父子 grouper 互相清理。`RunOfficialSegment()` 现优先使用 segment root 范围内的 selected-grouper pipeline，只执行当前 baseline/skip policy 允许的 groupers：selected Early steps -> `AfterCurrentGroupTiming` deferred -> selected Late steps -> deferred，保留整段 runtime item/rope preclean/postrefresh 和 PhotonView ID 分配。Release 构建 0/0，DLL 更新时间 `2026/5/20 21:39:17`。
- [DreamyAscent] 继续修复官方 `Generate Segment` Late supplement：日志证明 inactive-safe 父链收集已生效，但 `Go()` 补跑仍让 Roots/Snow 多个 PropSpawner/DecorSpawner 归零。`RunLateStepsAfterOfficialRunIfNeeded()` 已改为贴近原版 `PropGrouper.RunAll` Late 阶段：调用 `LevelGenStep.Execute()`、按 `DeferredTiming` 收集 `IDeferredStep`，最后统一执行 `DeferredStepTiming.AfterCurrentGroupTiming`；日志新增 `deferredAfterGroup=<n>`。Release 构建 0/0，DLL 更新时间 `2026/5/20 20:57:50`。
- [DreamyAscent] 修复 Jungle 官方 `Generate Segment` 仍空的问题：最新日志确认新 DLL 已加载但 `Pops_Plat` / `Props_Wall` 仍 `lateSupplementSteps=0`，根因从外部 postfix guard 进一步收窄为 Late step 收集依赖 `GetComponentInParent<PropGrouper>()`，在 inactive 父层级下拿不到最近 grouper。`DaRuntimeEditService` 已改为手动沿 `Transform.parent` 查找最近 `PropGrouper`，官方生成仍保持整段 preclean、Early-before-Late、`RunAll(true)` 后 Late `Go()` supplement、整段 postrefresh。Release 构建 0/0，DLL 更新时间 `2026/5/20 0:09:54`；下次需实机复测 Jungle 日志 `lateSupplementSteps>0` 和 `itemSpawners>0`。
- [DreamyAscent] 收口今日状态并同步 memory：Beach 椰子/物品正常；Beach 地形材质仍未解决，失败的 post-generation material modifier replay 和 custom placement child-scale sync 已按用户要求退回。更新 `README.md`、`MEMORY_INDEX.md`、`TODO.md`、`mods/DreamyAscent/README.md`、`RECENT.md`、`DECISIONS.md`、`FILES.md` 和 `temp/2026-05-20.md`。

## 2026-05-19

- [规则] 新增每个 MOD 的临时思考记忆规则：`mods/<ModName>/temp/YYYY-MM-DD.md` 记录阶段性判断；每形成 3 次明确判断追加摘要；上下文压缩、会话中断或换 AI 后先读最新临时 MD，再读正式四件套。
- [新增] 为 `DreamyAscent`、`ItemInfoCN`、`Lantern_ShootZombies_Night`、`PlayersInfo`、`WhySoLaggy` 初始化 `temp/2026-05-19.md`，作为压缩恢复入口。
- [索引] 同步更新 `README.md`、`MEMORY_INDEX.md`、`common/01_协作与记忆规则.md`，把临时思考记忆区加入读取顺序、目录职责和归类规则。

## 2026-05-18

- [DreamyAscent] 修复官方模板生成本段清空 Props 组：日志确认 Beach `PlateauProps/WallProps` 在 baseline 命中且 runtime refs 完整时从非 0 变 0，根因是 1.62.a `PropGrouper.RunAll()` 可见路径只执行 Early steps，Late steps 被收集但未执行。`DaRuntimeEditService` 现对 `PropGrouper.timing == Late` 使用替代 Late pipeline（`ClearAll` -> Late `LevelGenStep.Execute` -> `AfterCurrentGroupTiming` deferred -> 可选 validate），Early grouper 仍走原版 `RunAll(true)`；同时生成按钮前释放预览隔离。Release 构建 0 警告 0 错误，DLL 已同步到 terrain profile。复测需新开/新图，旧 DLL 已清成 0 的场景不能作为干净基准。
- [DreamyAscent] 为官方生成链添加临时 trace：`RunSegment` 入口记录调用堆栈，`RunGrouper`/白名单 step 记录生成前后直接子物与后代数量差异，重点输出 `zeroedSteps`。Release 构建 0 警告 0 错误，DLL 已同步到 terrain profile。memory 同步记录经验：遇到“生成后只剩地形/子物归零”时先比较前后 `GeneratedChildrenSnapshot` 或 `Generation trace` delta，再判断 baseline、变体、运行时引用和原版 `RunAll(true)` 行为。

## 2026-05-17

- [PlayersInfo] 体力条网络波动加固（现象 D2：单条偶发闪一下消失再出现）。三处加固：(1) `GetStableCharacterId` 加 `viewID → actorNumber` 反查缓存（`s_viewIdToActor`），让 `photonView.Owner` 短暂为 null 时同一玩家仍能命中原 ActorNumber，避免 fallback 到 ViewID 让 stableId 漂移。(2) 删除 `RefreshNearby` 中 `if (c.photonView.Owner == null) continue;` 的硬剔除，sidForRange 改用 `GetStableCharacterId(c)` 走缓存。(3) 新增成员丢失保留窗口 `RetainOnLossDelay = 1.5f`：本帧未出现但仍在 `_displayOrder` 里的玩家，1.5s 内用上次 `Character` 引用补回 `s_visibleScratch`/`s_visibleById`，让 `ResolveDisplayOrder` 看到的成员集合不变 → `HasSameMembers` 仍 true → 不触发 `ApplyDisplayOrder`。`ClearAll` 同步清两份缓存防脏数据残留。Release 构建 0/0；同步更新 `发行/0.1.1/CHANGELOG.md` 加第三条英文 Bugfix、`README.md` What's new 顶部加一行；dll 从 50176 → 50688 字节，重打 `wuyachiyu-PlayersInfo-0.1.1.zip` 152552 字节。仍在 0.1.1 包内累计三条 Bugfix。

- [PlayersInfo] 合补重打 0.1.1 发行包：将今日两条 BUG 修复（SettingChanged 全订阅 + NearbyRange 边缘拖动）并入 0.1.1 包。`MOD开发/PlayersInfo/发行/0.1.1/CHANGELOG.md` 加 `### Bugfixes` 小节（二条英文描述）；`README.md` 的 "What's new in 0.1.1" 顶部加两行 Bugfix；manifest.json 未变（版本号仍 `0.1.1`，描述仍原文）。Release 重构 0/0，dll 从测试环境 plugins 复制到 `发行/0.1.1/PlayersInfo.dll`（同一次Release构建产出，50176 字节）；删旧 zip 重打 `wuyachiyu-PlayersInfo-0.1.1.zip`（151816 字节，5 件套）。本次为"同版本号覆盖"（上传 Thunderstore 需人工覆盖处理），理论上更规范的做法是走 0.1.2，但用户明确要求更新 0.1.1。
- [PlayersInfo] 修复体力条边缘拖动闪烁 BUG（现象 B）：在 `TeammateBarsCoordinator.RefreshNearby` 加入距离滞回（hysteresis）。已在 `_displayOrder` 里的玩家，阈值从 `range` 放宽为 `range + HysteresisMargin (5f)`；不在里的玩家仍使用原 `range` 才能进入。这样玩家距离恶在 `NearbyRange`（默认 30m）边缘抖动不会反复进/出可见集，避免 `BindTarget`/`SetActive(false)` 闪烁。玩家真的拉开 5m 以上才会被剔除，过闸不拖泲。实现只加4 个补丁：新增 `HysteresisMargin` 常量 + `s_displayOrderSet` 静态 HashSet（避免 GC），在距离过滤处查询，O(1)。Debug 构建 0 警告 0 错误。仍为 0.1.1 内部修复，未升版、未重打发行包。
- [PlayersInfo] 修复部分玩家体力条整体重刷/抖动 BUG：根因是 `PlayersInfoPlugin.BindConfig` 把 `Config.SettingChanged` 全配置事件绑到了 `OnAnyConfigChanged → coord.OnConfigChanged → ClearAll → 全部 _pool Destroy 重建`，因此拖动 OffsetX/NearbyRange 滑块、ConfigurationManager 实时事件、BepInEx 启动回写都会让所有体力条一起跳。改为只对真正影响"克隆体结构"的 3 个开关单独订阅：`CfgModEnabled`、`CfgShowStaminaValue`、`CfgEnableInventoryRow`；其余运行时数值类（NearbyRange/MaxNearbyCount/RoundStamina/DebugLogging/Anchor/Offset）由 Update/RefreshNearby 直接读 `Cfg.Value` 生效，无需事件。Debug 构建 0 警告 0 错误，dll 已落入测试环境；本次修复为 0.1.1 内的源码修复，未升版本号、未重打发行包。
- [DreamyAscent] 进入 Snapshot V2 采集重置：新增 `GeneratedChildrenSnapshot.json` 诊断和 TerrainRandomiser 来源标记，删除旧 `data/map-data/1.62.a/` 与 `TerrainRandomiser/` 样本目录，改用 `1.62.a-snapshot-v2/` 与 `TerrainRandomiser-snapshot-v2/`，离线脚本强制诊断五件套并拒绝脏样本/来源混用；新样本通过前不覆盖当前 DLL 仍依赖的 `generated/template-snapshots.json` 与 `object-registry-input.json`。
- [PlayersInfo] 采纳外部 AI 三条性能建议，过滤掉夸大与已做项后落地：(1) `IconSpriteCache.Clear` 增加 `Object.Destroy(sp)` 遍历销毁，`Sprite.Create` 后设 `HideFlags.DontSave`，避免场景切换/MOD 关闭时 Sprite 资源累积；(2) `TeammateBarDriver.UpdateValueTexts` 加脏检查，缓存上次显示的整数（round 模式）和 value*10（F1 模式），相同值不再写 `text`，省 ToString 与 TMP mesh 重建；`extraValueText` 同处理；(3) `GetIsInvincible` 反射结果缓存，0.25s 探一次替代每帧 `FieldInfo.GetValue` 装箱。`BindTarget` 切目标时同步重置三类缓存。Release 构建 0 警告 0 错误，dll 已落入测试环境。AI 报告的“对象池泄漏”实为 maxN 上限兜底+`OnConfigChanged→ClearAll` 已覆盖，未采纳。
- [PlayersInfo] 发布 0.1.1：三处版本号同步升到 `0.1.1` / `0.1.1.0`（`PlayersInfoPlugin.PluginVersion`、`AssemblyInfo.cs`、`PlayersInfo.csproj <Version>`）。`MOD开发/PlayersInfo/发行/0.1.1/` 新增 README/CHANGELOG/manifest 三件套，icon.png 从 0.1.0 复用；dll 从测试环境 plugins 复制（同一次构建）；`wuyachiyu-PlayersInfo-0.1.1.zip` 以 README/CHANGELOG/manifest/PlayersInfo.dll/icon.png 五件打包，总包 ~148KB。本版为“安静维护版”：无新增功能与行为变更，仅含上述三条性能/资源优化。

## 2026-05-16

- [DreamyAscent] 记录客机端日志问题并后置：客机端 `Player.log` 曾出现 DreamyAscent IMGUI 布局异常以及多人 DropItemRpc/PhotonView/RPC 噪声；左下摘要面板已加固定控件数防护，但当前不继续追客机端兼容，优先完成官方模板、空白模板、混合模板、自定义放置、模板基线和诊断闭环。

## 2026-05-14

- [DreamyAscent] 根据用户截图建议继续重构 DA 结构面板：旧树形缩进改为“关卡横排 -> 区域横排 -> 当前区域生成物列表”的三层联动，生成物行显示 step 类型和 catalog item/material 数量；右侧参数和场景高亮随生成物选择同步。Release 构建 0 警告 0 错误。
- [DreamyAscent] 今日任务收尾：本轮停在 `SubArea` / `PlacementRule` 配置保存层和导入/UI 修复，不进入实际自定义生成；最新 Release 构建 0 警告 0 错误，样本回归 `pass`。下次优先用 `Beach-Segment__Jungle-Segment__Desert-Segment__Caldera-Segment__Volcano-Segment_20260513_235007.json` 复测导入回显，预期 `placementConfigs=1` 且 Beach 页显示 2 个子区、1 条规则。

## 2026-05-13

- [DreamyAscent] 修复 `SubArea` / `PlacementRule` 导入回显问题并调整 UI：诊断确认最新导出 JSON 已包含 `Beach_Segment subAreas=2 placementRules=1`，但导入日志 `placementConfigs=0`；根因是 Json.NET 反序列化填充已有 List 未触发字段存在标记。现对 `subAreas` / `placementRules` 使用 replace 反序列化并用非空配置兜底识别，导入时还会把误填的子区显示名解析回内部 ID；Catalog 页改为折叠区块，目标子区改为左右按钮选择，运行时 item/material 列表默认折叠。Release 构建 0 警告 0 错误，样本回归 `pass`。
- [DreamyAscent] 完成 `SubArea` / `PlacementRule` 配置数据层第一版：`DaSegmentData` 现在可保存 `subAreas` 与 `placementRules`，规则包含目标子区 ID、registry ID、数量、缩放、放置模式、旋转模式和 ownership；Catalog 页可从 `recommendedFirstPassCandidate` 添加规则，但不执行生成；导入时按 `SegmentName` 复制配置，旧 JSON 无字段不会清空，新 JSON 空数组可显式清空；Release 构建 0 警告 0 错误。
- [DreamyAscent] 接入 `DaObjectRegistry` 只读源码骨架：新增 `DaObjectRegistryData.cs`、`DaObjectRegistryService.cs`，构建时复制 `data/map-data/generated/object-registry-input.json` 到插件目录 `DreamyAscent Data/object-registry-input.json`；UI 区段模板库页显示全局注册表摘要和当前 Segment 首批推荐候选，不提供生成按钮；Release 构建 0 警告 0 错误。
- [DreamyAscent] 建立样本产物工具链：新增 `MOD开发/DreamyAscent/data/tools/build_map_data_artifacts.py` 和 `data/map-data/generated/`，可从现有诊断样本重复生成 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`；当前回归 `pass`，0 issue / 0 warning。
- [DreamyAscent] 生成第一版离线开发输入：130 个 Segment snapshot、520 个 grouper、4001 个 step、193 个模板候选、25 个材质候选；对象注册表候选区分技术低风险与第一批推荐测试候选，首批推荐候选 20 个，已排除桥、岩浆、行李等结构/机制对象。
- [DreamyAscent] 完成 `data/map-data` 样本审计收口并补根级 memory：官方自然样本 22 个 JSON / 19 个完整诊断目录，TerrainRandomiser 验证样本 29 个 JSON / 7 个完整诊断目录；批量复核确认诊断四件套完整、`0 grouper` segment 为 0、未知 variant 为 0，`Beach / Jungle / Roots / Snow / Desert` 全部已知变体均覆盖。
- [DreamyAscent] 新增 `MOD开发/DreamyAscent/data/map-data/sample-index.json` 机器可读索引，并同步 `README.md`、`MEMORY_INDEX.md`、`TODO.md`、`mods/DreamyAscent/README.md`、`RECENT.md`、`FILES.md`、`MAP_GENERATION.md`：下一步从继续刷样本切换为模板快照、`DaObjectRegistry` 输入和变体纯净性回归检查。
- [all mods] 检查 PEAK 1.62.a 反编译更新：`Assembly-CSharp`、`Assembly-CSharp-firstpass`、`pworld`、`Zorro.Core.Runtime`、`PhotonUnityNetworking`、`PhotonRealtime`、`Photon3Unity3D`、`Unity.TextMeshPro` 与 1.61.b 反编译源码哈希一致；ItemInfoCN、Lantern_ShootZombies_Night、PlayersInfo、WhySoLaggy、DreamyAscent Release 构建均 0 警告 0 错误，暂不需要代码更新。
- [memory] 完整收口今日记忆：同步根 README、MEMORY_INDEX、TODO、mods/common 索引和 DreamyAscent README/FILES 的日期与改名边界；明确源码内 `TerrainCustomiserCN Files/Exports/Imports/PreviewPoses.json` 旧名字符串只用于迁移兼容，不是待删除脏代码。
- [DreamyAscent] 收口改名后的 Git 脏状态：确认脏项主要是旧 `TerrainCustomiserCN` 路径删除、新 `DreamyAscent` 路径未跟踪造成的索引未识别改名；已取消错误 staged 删除，并仅对 DreamyAscent 代码路径与 memory 执行 `git add -A`，让 Git 识别 rename/add，避免后续冲突。
- [DreamyAscent] 更新 `RECENT.md` 和 `DECISIONS.md`：明确后续开发入口只使用 `MOD开发\DreamyAscent` 与 `memory\mods\DreamyAscent`，旧 `TerrainCustomiserCN` 只作为历史来源和旧数据迁移来源；若再出现旧删新未跟踪，优先整理索引，不恢复旧目录。

## 2026-05-12

- [DreamyAscent] 再次补强地图生成研究记忆：新增 `IMPLEMENTATION_MATRIX.md`，把官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照和多人同步逐项落到实现路径、证据、例子和资料缺口；同步补强 `CROSS_SEGMENT_PLACEMENT.md` 的正误实现、`PlacementRule` JSON 和兼容矩阵，并更新索引、README、TODO 与 RECENT。
- [DreamyAscent] 补强地图生成研究记忆：新增 `CROSS_SEGMENT_PLACEMENT.md`，专门记录跨区段物品放置的“来源模板 + 目标子区”模型、Jungle 棕榈放 Desert 等例子、风险等级、UI 流程和资料缺口；同步更新 `MAP_GENERATION.md`、`MAP_GENERATION_RESEARCH_NOTES.md`、`DECISIONS.md`、`RECENT.md`、`TODO.md`、`README.md` 和 `MEMORY_INDEX.md`。
- [DreamyAscent] 按用户要求完成地图生成资源初版多轮通读并落盘：新增 `MAP_GENERATION.md` 和补全 `MAP_GENERATION_RESEARCH_NOTES.md`，覆盖 DreamyAscent 源码、memory、诊断 JSON、PEAK 1.61.b 反编译、TerrainCustomiser、TerrainRandomiser、HazardSpam/NetGameState；明确官方模板、空白自定义、模板库/外部资源/材质/多人同步的分层实现路线、已知故障反推、资料缺口和下一步路线。
- [DreamyAscent] 更新地图生成长期决策和 TODO：后续不再依赖被清理/重跑污染的 live scene 作为唯一模板来源；新增干净模板快照、稳定 path 诊断、SubArea 数据模型、DaObjectRegistry、低风险自定义放置规则等待办；强调 `CustomBlank -> OfficialTemplate` 不能承诺自动恢复。
- [DreamyAscent] 修正 Jungle 官方整段生成重复堆岩石：日志确认 `Jungle_Segment` 正常扫描 4 组，问题是 `Rocks_Plat` / `Rocks_Wall` 在已有地图上被再次执行；现官方整段生成跳过这两个岩石组，保留 `Pops_Plat` / `Props_Wall`；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正 Roots 生成入口运行时引用丢失：最新日志显示非跳过 grouper 全部 `runtime grouper reference is missing`，导致“生成本段”实际 `groupers=0`；已新增生成前按当前场景重绑 segment/grouper/step/constraint 引用，并覆盖生成本段、生成本组和参数修改后的自动生成；Release 构建 0 警告 0 错误。
- [DreamyAscent] 澄清“雨林不显示”本轮日志边界：当前日志地图组合不含 `Jungle_Segment`，因此需要下一轮进入含 Jungle 地图后再验证列表、预览和 RuntimeExport。
- [DreamyAscent] 修正用户反馈的高亮可见性和空白模式删多问题：选中生成器高亮改为半透明实心矩形面；`CustomBlank` 先保留 Desert 的 `Platteau` / `Rocks` 与 Caldera 的 `LavaRivers` / `Rocks`，避免地形空心；Release 构建 0 警告 0 错误。
- [DreamyAscent] 根据用户确认调整 Caldera 空白语义：Caldera `CustomBlank` 改为清理官方生成组、只保留岩浆机制；同时让 `CustomBlank` 返回实际清理数量，避免“生成本段”状态显示 0 像没反应；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正 Desert 官方模板生成空心化风险：日志确认 `Platteau` / `Rocks` 在 `DesertRockSpawner.Clear` 报 NRE，原因是生成前预清理破坏原版 Clear 依赖；已移除官方模板生成前预清理，仅 `CustomBlank` 使用自定义清理；Release 构建 0 警告 0 错误。
- [DreamyAscent] 收窄 Desert `CustomBlank` 保留规则：官方模板已正常，空白自定义不再保留 `Rocks` 组，只暂保留 `Platteau`，减少官方岩石/峡谷残留；Release 构建 0 警告 0 错误。
- [DreamyAscent] 继续收窄 Desert `CustomBlank`：诊断显示剩余主要来自 `Platteau/Canyon` 的 `Small` / `Big` / `Pillar`，已在保留 `Platteau` 后额外清理内嵌 `Canyon` grouper；Release 构建 0 警告 0 错误。
- [DreamyAscent] 最后收窄 Desert `CustomBlank` 剩余：诊断显示剩余 151 个候选中除 `GroundMesh` 外都来自 `Platteau/Start` 的 `RockFinal_Desert*` 和 LOD，已清理 `Start` 子物，仅预期保留底板本体；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正 Roots/Jungle 变体叠加风险：fallback 到 whole segment 时不再导出 inactive 变体下的 PropGrouper，只保留 `activeInHierarchy` 的生成组，并记录 active/total grouper 数；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正上一条 Roots/Jungle 过滤过严问题：全局 `activeInHierarchy` 过滤导致 Roots/Jungle 扫不到，现改为只排除 inactive 的 `- xxx Variant` 分支；Release 构建 0 警告 0 错误。
- [DreamyAscent] 继续修正 Roots 乱石：最新日志显示变体组已排除，剩余乱石来自默认 `PlateauRocks` / `WallRocks`；Roots 官方整段生成现跳过这两个 grouper；Release 构建 0 警告 0 错误。
- [DreamyAscent] 撤销 Roots 官方生成跳过岩石组的规则：用户测试发现 `CustomBlank -> OfficialTemplate` 后只剩蘑菇，日志显示 Redwood NRE 且官方结构恢复不完整；切回官方模板时现自动 Rescan；Release 构建 0 警告 0 错误。
- [DreamyAscent] 根据用户确认再次调整 Roots 策略：恢复官方整段生成跳过 `PlateauRocks` / `WallRocks`，避免乱石；`CustomBlank -> OfficialTemplate` 不再承诺完整恢复，UI 提示建议重开或新图复测；Release 构建 0 警告 0 错误。
- [DreamyAscent] 项目由 `TerrainCustomiserCN` 改名为 `DreamyAscent`：源码目录、命名空间、程序集名、BepInEx 插件 ID、运行时文件夹、memory 四件套入口和构建命令均已切换；旧 `TerrainCustomiserCN` 和误名 `DreamAscent` 测试 DLL 已从 terrain profile 删除，只保留旧数据迁移兼容字符串。
- [DreamyAscent] 明确用户此前发送的截图属于改名前功能基线测试，用于后续改名回归对照；不再误判为改名后功能测试。
- [DreamyAscent] 复核改名前 Caldera/Volcano `CustomBlank` 测试诊断：Caldera 最新剩余仅 `River` / `Coll`，`ash` / `Bubbles` 已清掉；Volcano 最新剩余仅 `RisingLava/Lava` 下的 `Coll` / `Plane`。
- [DreamyAscent] 继续完成改名收尾：源码类名、文件名和 `.csproj` 编译项中的旧 `Tc*` 前缀统一改为 `Da*`；启动日志中不再直接提旧 DLL 名，避免和新插件名混淆。
- [DreamyAscent] 完成 `CustomBlank` 第一版复测：补充 Caldera 粒子残留清理，确认空白模式只保留岩浆/上升熔岩等基础机制对象；同步更新 `RECENT.md` / `DECISIONS.md` / 根 `TODO.md`。
- [DreamyAscent] 清理根 TODO：预览雾状/白雾遮挡和 F1 输入稳定性从 P0 移出；修正预览代码清理待办，明确 `_screenPreviewActive` 和 `mainCamera.rect` 保存/恢复仍是当前有效路径。
- [DreamyAscent] 继续清理根 TODO：删除已写入 `RECENT.md` 的 6 条已完成对象库/区段模板库 `[x]` 项，并修正 DreamyAscent 最近反馈中的旧“仍有雾状遮挡”描述。
- [memory] 继续清理根 TODO：移除“新增 MOD 必须建四件套”这类规则项；该规则已在 `README.md` / `MEMORY_INDEX.md` / `common/01_协作与记忆规则.md` 中承载，不再占永久待办。

## 2026-05-10

- [memory] 今天任务收口：同步根 `README.md`、`MEMORY_INDEX.md` 和 DreamyAscent 四件套，记录对象库诊断、区段模板库、高亮、翻译和后续验证边界。
- [DreamyAscent] 根据新截图继续清理区段模板库英文：补 Jungle/Ice/Rock/M_ 材质条目和编号规则；高亮四角括号长度按实际范围收缩，避免小范围又拼成完整方框；Release 构建 0 警告 0 错误。
- [DreamyAscent] 根据实机截图再次修正生成器范围高亮：矩形范围改为水平四角括号 + 小中心标记，去掉完整大框和对角线；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正区段模板库残留英文：为编号 prefab 名称增加规则翻译（`Rock_Round.*`、`Rock_Lil.*`、`Desert_Rock *`、`LavaBridge *`），补 `Shader`/`PhotonView` 中文标签；Release 构建 0 警告 0 错误。
- [DreamyAscent] 修正生成器范围高亮：立体大框改为地面平面脚印 + 中心标记，选中项青色、已修改项橙色；Release 构建 0 警告 0 错误。
- [DreamyAscent] 根据用户反馈澄清“区段模板库”与未来 XYZ 放置子区的边界；UI 文案从“区域物品库”改为“区段模板库”，`area` 显示为“范围”，并补一批 catalog prefab/material 翻译。
- [DreamyAscent] 根据截图修正 F1 布局：左栏加宽，地图名改短中文组合，详情区增加“参数 / 区段模板库”标签页；Release 构建 0 警告 0 错误。
- [DreamyAscent] 接入第一版只读区段模板库 UI：当前 Segment 下展示 item/material、来源 step、默认数量/范围/概率和父子/Photon 标记；新增 catalog 缓存服务，Release 构建 0 警告 0 错误。
- [DreamyAscent] 确认 Roots 组合 `ObjectCatalog.json` 成功覆盖：Roots Segment 55 个 item、11 个 material，Redwood/Forest Cave/Mushroom tree 等父子模板进入 catalog；修复 catalog 缺翻译日志噪声。
- [DreamyAscent] 确认用户实机成功生成 `ObjectCatalog.json`：Beach/Jungle/Desert/Caldera/Volcano 组合得到 5 个区域、147 个 item、10 个 material；TODO 转入 Roots 组合覆盖和 UI 接入。
- [DreamyAscent] 新增第一版对象库诊断：`DaObjectCatalogData.cs` 和 `DaObjectCatalogDiagnosticService.cs`，写诊断时生成 `ObjectCatalog.json`；Release 构建 0 警告 0 错误。
- [DreamyAscent] 确认用户实机成功生成最新 `ObjectReferenceMap.json`；抽样验证 `step-prop-prefab`、`single-item-prefab`、材质规则和父子模板候选均可用，TODO 转入对象库设计。
- [DreamyAscent] 增强 `ObjectReferenceMap.json`：为 prefab/material 引用增加 role 分类、对象库候选原因和父子/单物品生成器计数；Release 构建 0 警告 0 错误。
- [DreamyAscent] 记录后期区域物品编辑需求：区域化 UI、可添加物品、父子绑定、外部 Unity 物品导入和材质/颜色替换；同步 TODO、DECISIONS、FILES、RECENT。
- [DreamyAscent] 复核本地参考代码：确认原生 `PSM_ChildSpawners` 可参考父子依附生成，`PSM_SetMaterial*` 需避免照搬 sharedMaterial 写法，TerrainRandomiser 可参考多人同步/PhotonView ID 分发。
- [DreamyAscent] 新增 `DaObjectReferenceDiagnosticService`：写诊断时输出 `ObjectReferenceMap.json`，记录 prefab/material 等 Unity 对象引用；Release 构建 0 警告 0 错误。
- [PlayersInfo] 发布 0.1.0：按首次公开发布口径更新 `发行\0.1.0` 三件套和 memory，公开文档只保留当前 HUD 功能；同步 AssemblyInfo 为 `0.1.0.0`。
- [Lantern_ShootZombies_Night] 记录客机备用池/灯燃料双扣修复：本地玩家灯笼以本地 tracked fuel 为权威，远端 fuel 下降不覆盖本地；重复灯只允许主灯笼消耗备用池/燃料。
- [Lantern_ShootZombies_Night] 同步 `DECISIONS.md`、`FILES.md`、根 `TODO.md`：补本地燃料权威、主灯笼规则、远端熄灯后续风险和实机验证项。
- [DreamyAscent] 将 `Jungle_Segment`、`Snow_Segment`、`Volcano_Segment` 预览位姿整合为源码默认值；Release 构建 0 警告 0 错误。

## 2026-05-09
- [DreamyAscent] 将用户采集的 `Beach_Segment`、`Roots Segment`、`Desert_Segment`、`Caldera_Segment` 预览位姿整合为源码默认值；Release 构建 0 警告 0 错误。
- [DreamyAscent] 新增 `DaPreviewPoseService` 和预览校准：WASD/Shift/Space/Ctrl 微调预览位姿，`F6` 保存当前区段到 `DreamyAscent PreviewPoses.json`；明确不加额外快捷键。
- [DreamyAscent] `DaMapPreview` 两处直接中文预览提示改走 `DaLocalization`；检查 DreamyAscent 源码未发现典型乱码标记，Release 构建 0 警告 0 错误。
- [修复] `.editorconfig` 补 UTF-8 BOM，使文件本身与 `charset = utf-8-bom` 规则一致，避免 PowerShell 默认读取时误判为中文乱码。
- [修复] 全工作区 84 个 .cs 源文件补上 UTF-8 BOM（`EF BB BF`），消除 PowerShell/cmd 读取时中文注释乱码问题。四个 MOD 编译全部通过。
- [修复] 源码 + memory MD 共 11 处“兑底/兆回”错别字统一改为“兜底”。

## 2026-05-08

- [新增] `common/06_UI与字体规范.md`：落盘 CJK 字体四级兜底、FontHelper 标准实现、三条铁律（禁默认 font/禁 defaultFontAsset/禁返 null）、描边与字号规范，作为跨 MOD 根治规范。
- [新增] `MOD开发/ItemInfoCN/.../Helpers/FontHelper.cs`：与 PlayersInfo 同源同结构的四级兜底实现；Plugin.AddDisplayObject 的 `tm.font` 改走 `FontHelper.GetChineseCapable()`，`gm.heroDayText.font` 仅作 null 回退。
- [修改] `MEMORY_INDEX.md`：补 `common/06_UI与字体规范.md` 条目，调整 common 主题数为 7，ItemInfoCN/PlayersInfo FILES 条目注明含 FontHelper。
- [修改] `mods/ItemInfoCN/FILES.md`、`DECISIONS.md`、`RECENT.md`：加 FontHelper 文件、禁止回退条款、今日根治履历。
- [规则] 内存记忆 `54b5aa17`（CJK字体乱码）升级为“根治版”：扩充三条铁律 + 跨 MOD 落地状态（PlayersInfo + ItemInfoCN 均接入）。

## 2026-05-08

- [新增] `MOD开发/PlayersInfo/.../Helpers/FontHelper.cs`：抽取 CJK 字体四级兜底共享 Helper（AscentUI→heroDayText→全场TMP→defaultFontAsset），LocalStaminaBarPatch + TeammateBarsCoordinator 将 5 处调用统一走 `FontHelper.GetChineseCapable()`，删除两份重复实现。
- [修改] `mods/PlayersInfo/FILES.md`：加 `Helpers\FontHelper.cs` 条目。
- [修改] `mods/PlayersInfo/DECISIONS.md`：显示规则加入必走 `FontHelper.GetChineseCapable()`；禁止回退加一条“不得复写同类逻辑”。
- [修改] `mods/PlayersInfo/RECENT.md`：顶部记录 FontHelper 抽取和编译结果。
- [规则] 内存记忆 `54b5aa17`（HUD字体修复）升级为通用专家经验，含四级兜底策略和“禁返 null”铁律。
- DreamyAscent：存档当前暂停状态，记录预览雾问题以第一遍已验证修复为准。
- Lantern_ShootZombies_Night：检查新版 Thanks.ShootZombies 1.3.4，调整 LSN 兼容策略，版本保持 `0.2.1`。

## 2026-05-08

- [新增] `common/00_用户偏好.md`：落地用户真实身份（乌鸦吃鱼）、说人话风格、接手基线。
- [修改] `common/01_协作与记忆规则.md`：四同步铁律（update_memory + MD + MEMORY_INDEX + CHANGELOG）+ 接手前检查 + Get-Date 日期规则全落地。
- [修改] `common/02_工程与构建规范.md`：OutputPath 指测试环境、HintPath 保 Steam 路径、L&SZ&N 作重构参照。
- [修改] `common/03_日志与诊断规范.md`：日志分级表、FieldProbe DSL 语法、JSON 禁注释（用 `_doc`/`note`）。
- [修改] `common/04_联机与同步规范.md`：主客机权限表、RPC 校验四原则、中途加入快照同步。
- [修改] `common/05_发布与版本规范.md`：README/CHANGELOG/manifest 三文件协同、135 字符上限、四维结构、三阶段文档。
- [新增] `mods/Lantern_ShootZombies_Night/` 四件套：README（含 mermaid）+ RECENT（四功能线稳定）+ DECISIONS（含禁止回退）+ FILES（源码/构建/Helper）。
- [新增] `mods/WhySoLaggy/` 三件：README（能力矩阵 mermaid）+ RECENT（1.0.3 治理）+ DECISIONS（只做诊断 + FieldProbe 默认关闭）。
- [新增] `mods/PlayersInfo/` 三件：README（mermaid）+ RECENT（HUD 聚合改造）+ DECISIONS（只读展示 + 与 L&SZ&N 边界）。
- [新增] `mods/ItemInfoCN/` 三件：README（1.0.0 发布架构）+ RECENT（配置项表/翻译覆盖）+ DECISIONS（架构/翻译/补丁边界/EasyBackpack 兼容）。
- [修改] `README.md`：新读取顺序以 CHANGELOG 打头；加入接手前检查章节、四同步铁律、日期铁律。
- [修改] `MEMORY_INDEX.md`：补 `common/00_用户偏好.md`；全部 MOD 四件套条目化列出；记忆维护规则拆写入/读取/日期/禁止四小节。

## 2026-05-07

- [删除] 删除所有 `memory` 下的历史目录，包括根目录历史目录和 DreamyAscent 旧单文件目录。
- [重写] 清理 `README.md`、`MEMORY_INDEX.md`、`TODO.md`、`common/` 和 `mods/` 中对历史目录的依赖说明。
- [规则] 后续不再把已删除的历史文件作为兜底；缺失信息必须从源码、日志或用户反馈重新确认后写入当前结构。
- [修正] 不再只围绕 DreamyAscent 建 memory；`MOD开发/` 下 ItemInfoCN、Lantern_ShootZombies_Night、PlayersInfo、DreamyAscent、WhySoLaggy 都有 `mods/<ModName>/` 入口。

## 2026-08-20

- [新增] `mods/WhereIsMyAmulet/`：记录 WhereIsMyAmulet 1.0.2 的常驻/定时显示配置、定时秒数、雕像碎片识别边界、构建输出和发行包路径。
- [修改] `MEMORY_INDEX.md`：加入 WhereIsMyAmulet 四件套入口并更新索引日期。
