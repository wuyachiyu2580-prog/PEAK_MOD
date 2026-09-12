# StateKeeper Decisions

## 2026-09-09 原版UI模板合同

- 克隆原版Button后必须替换onClick事件实例；RemoveAllListeners不清持久序列化监听，实际Resume模板包含GUIManager.Resume。
- 历史页内弹窗共用父Canvas并置顶，禁止写死子Canvas排序100。实际PauseMenu为204，低序号会被遮挡。
- 操作按钮必须按英文与真实字体advance分配宽度，不能只按中文宽度配固定大字；本轮保留文字命令，不改星形收藏。

## 2026-09-08 发布前审查

- 用户已明确要求先发布一版，沿用0.1.0；允许更新原发行草稿并生成ZIP，不意味着已上传Thunderstore。
- 废弃代码清理不得删除Unity/Harmony回调或JSON历史合同；不能用只在测试中调用的旧API代替生产接口的测试覆盖。
- Enabled=false应拦截所有高频采样和事件采集，但允许保存与封存已有记录。DebugLogging=false默认不输出性能诊断。
- 测试版发布说明必须保留Unity/输入法/控制器/多人性能未验收项；版本和当前产物见RELEASE_AUDIT_2026-09-08.md。

## 2026-09-08 当前合同

- 以 `REPORT_REBUILD_2026-09-08.md` 为当前实现，下面早期“只采集/尚无面板”等状态不再有效。
- schema3保持；新局collectionRevision3采用本机单调经过时间；analysisVersion5独立缓存，不修改原始历史局。
- 死亡计数依状态佐证去重，状态转移与原始事件分别保留；不以死亡/昏迷大小关系修正数据。
- 时间重叠无法唯一归属时保留事实、不跨区间归因；库存不得整块丢弃。死亡暂存点排除半径2世界单位，不裁掉合法长距离。
- 报告只输出客观观察和证据，不输出责任/职业/能力评分；喂食消费属于实际消费者，来源持有人不自动等于帮助者。
- 比较仅用可靠Steam身份；缺能力不填0，短局/低覆盖无增减结论。不要求重录两小时，先用已有数据和合成反例。
- 性能必须看采集器、库存、UI和后台各路径；不能用插件入口0.00ms/frame替代实测。

更新时间：2026-09-06

## 已确认决策

- 程序身份固定为 `StateKeeper`，展示名称固定为 English `STATE KEEPER`、中文“状态分析”；版本保持 `0.1.0`，因为尚未正式发布。
- GUID 固定为 `com.local.statekeeper`，程序集与 DLL 固定为 `StateKeeper` / `StateKeeper.dll`。
- 项目独立于 `PlayersInfo`，不修改其 HUD 或私有代码，不发送新的游戏业务 RPC。
- 第一阶段只采集和保存，不制作游戏内分析面板；等积累足够真实数据后再单独规划展示形式。
- 第一版分析范围收敛为物品栏快照差分、位置/距离关系和体力变化区段；参考 BetterItemInfoDisplay 的 Action/组件分类只用于解释字段和辅助证据，不要求完整复制其物品使用监听。
- 物品消失只能记为库存流转或低置信度消耗候选；同 GUID 的 `uses`/`fuel`/`cookedAmount`/`useRemaining` 明确变化才作为资源变化主证据，并保留推断置信度。
- 物品定义与实际结果分层：`ItemDefinition` 来自 2.4.b 的 Action/Component/Cooking 结构，`ItemState` 来自实例字段，`ObservedOutcome` 来自快照差分、体力、状态、位置和事件；面板不得把理论作用显示成实际成功。
- `ItemUses` 和 `CookedAmount` 的字段存在不能直接解释为可消耗次数或已烹饪；特殊字段优先补 `Used`、`PetterItemUses`、`FlareActive`、`PowerEnabled`，并同步纳入指纹/差分。
- BetterItemInfoDisplay 的完整 Action 监听不是第一版前置条件；事件窗口以资源差分为主证据，Action 完成只作为时间锚点，默认输出 `Certain`、`Likely`、`Ambiguous` 置信度。
- 物品定义优先在运行时从 `ItemDatabase` prefab 只读扫描构建结构化目录；反编译代码用于核对语义，不手工维护名称表，也不在扫描时执行 Action。
- `OptionableIntItemData` 的条目存在与内部 `HasData` 必须分开记录；旧数据的 `hasUses` 只按 legacy ambiguous 处理，不能直接解释为有效次数。
- 现有 `statuses[]` 已包含 Hunger、Injury、Poison、Petrify 等状态，不重复增加独立饥饿/生命字段；若需要统计实际跳跃，则追加低频 `PlayerJumped` 事件，不增加 5Hz 样本字段。
- 实现前只追加高价值字段：有效次数语义、`Used`、`PetterItemUses`、`FlareActive`、`PowerEnabled`，以及物品事件 GUID/资源字段；`Scale`、`ScreamTime`、颜色、物理状态等延后，避免连续或低价值字段制造采样噪声。
- 分析层必须保留原始时间和原始坐标，另建单调分析时间与有效位置视图；死亡/离场 `+-5000` 哨兵坐标、无效端点和异常传送不得进入有效距离统计。
- 一局身份以非空 `RunManager.RunId` 为准；正式结束以 `GlobalEvents.TriggerRunEnded()` 为准，胜利以同局 `TriggerSomeoneWonRun` 为准。
- 单个玩家死亡、倒地、复活、传送和力竭不结束本局；主动离开、断线和崩溃保留未完成语义。
- 核心体力、状态、位置和距离保持 5Hz，不用降低核心采样率换性能。
- 库存轮询允许使用 2Hz；拾取、使用、施法完成、消耗和次数减少等关键动作继续即时记录。
- 即时体力事件由 `Advanced.StaminaEventThreshold` 控制，默认 0.01，变化量大于或等于阈值才记录；5Hz 体力样本永远保留。
- 高频数据继续使用 GZip 分块，元数据使用 UTF-8 JSON；写盘采用临时文件替换和串行后台队列。后续正式结束后后台合并为内部是完整 JSON 的 `*.data.json.gz`，校验成功前不得删除碎块。
- 合并进度和分析进度是两个独立任务、进度源和取消令牌；基础分析按五局样本规则实现，复杂评分暂缓，不允许用假进度掩盖读取等待。
- 收藏局不计入最近 10 局限制，收藏数量不设上限；后续收藏/取消收藏只通过游戏内面板完成，移除 F8 配置与处理。
- 游戏内面板入口放在 ESC 原版暂停菜单，参考 `WhereIsThing` 的 UI/字体和 `LengSword-ClimbInfoTracker` 的页面/按钮接入方式；MODCONFIG 与面板均支持中英文。
- 后续自愿匿名提交使用 PEAK-MAP 独立 API、Supabase 表和私有 Cloudflare R2 前缀；客户端和服务端双重脱敏，未明确同意不发网络请求，网站可配置关闭。
- 匿名策略采用适度脱敏：界面主要模糊化为每次提交临时生成的“玩家 A/B/C”，但技术上仍删除 Photon UserId、ActorNumber、物品 GUID、原始 detail、精确 UTC 时间、机器信息、路径和日志；局内时间保留相对秒数，距离约按 5-10 米粗化，坐标默认删除或粗粒度化。
- 默认不提供原始详细数据上传选项；未来高精度上传必须单独说明用途、再次取得同意并保持私有存储。
- 本机旧数据和配置已完成一次性迁移。正式发布不会经历改名，因此不保留旧目录/旧 GUID 的运行时迁移兼容代码。
- `发行/0.1.0` 当前旧草稿不更新，不能误当作可发布的 StateKeeper 包。

## 禁止回退

- 不把远端观察事件标记为本地权威。
- 不扫描或保存地面全部场景物品。
- 不为统计功能新增 Photon RPC 或房间共享统计。
- 不把 JSON/GZip 高频写入放回 Unity 主线程。
- 不因事件降噪删除 5Hz 原始体力曲线。
- 不自动恢复 `PeakRunAnalytics` 旧名、旧 GUID、旧配置或旧数据目录兼容层。

## 尚未解决

- `RunManager.TimeSinceRunStarted` 在旧实机数据中出现少量倒退；分析面板前应决定使用单调修正时间、双时间戳还是时间校正事件。
- 角色总数曾短时达到 9-10，需区分真实多人房与离场角色对象残留。
- 当前分块是达到阈值后的下一次 5 秒检查点封存，不是精确 150 样本；是否需要采样路径立即封块，须结合新性能数据决定。
- 当前后台结算、检查点合并、2Hz 库存和体力阈值尚缺一局新的长时间多人实机对比。
- 五局数据已经足够开始基础分析引擎设计，但当前还没有独立的分析实现和面板；实现前需用测试固定库存差分、时间倒退、无效坐标和体力区段规则。
