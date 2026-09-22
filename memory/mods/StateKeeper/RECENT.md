# StateKeeper Recent

## 2026-09-12 使用归因修订

- analysisVersion7、ItemUseRules组级分类、资源按原单位汇总、使用/流转筛选与证据展示；补测试SDK并恢复真实测试执行。
- 12局Runs只读回放，缺6个分块的一局显式降级；详细规则和回归入口见 `research/USE_RULES_2026-09-12.md`。不把以前构建成功当作全部功能完成。

## 2026-09-09 文档英文版

- 项目根目录和 `发行/0.1.0/` 的 `README.md`、`CHANGELOG.md` 已改为英文。
- 已重新生成并校验 `StateKeeper-0.1.0.zip`；本次仅涉及文档同步，DLL、schema、analysisVersion 和运行逻辑不变。

## 2026-09-09 英文排版与重命名

- 源资源确认原版按钮有持久GUIManager.Resume监听，RemoveAllListeners不会删除；改为全新ButtonClickedEvent，避免重命名等操作带出恢复游戏动作。
- 源资源PauseMenu Canvas排序204，旧弹窗100导致遮挡；去掉独立子Canvas，共用菜单Canvas并置顶。输入框明确聚焦、离页恢复交互。
- 英文按钮加宽为176/128/112，历史操作区488；紧凑文字18-20适配及16左右边距。真实字体advance核查完整容纳UNFAVORITE/RENAME/DELETE。
- 64项测试通过，Release和0.1.0ZIP已更新并部署；哈希与详细边界见 `temp/current.md`。实机画面尚待用户重启确认。

## 2026-09-08 首版发布包与废弃代码自审

- 按用户最新要求生成0.1.0发行目录和StateKeeper-0.1.0.zip，详情见`RELEASE_AUDIT_2026-09-08.md`；不再适用此前“禁止更新发行草稿”的阶段性约束。
- 清理旧F8收藏路径、仅测试在调用的冗余历史接口、旧单曲线/区段/游标分支、被覆盖的证据回调及5个未使用参数；保留JSON合同、框架反射和有效测试入口。
- 修复Enabled=false仍可能从补丁写事件，恢复采集标记中断；性能日志现在受DebugLogging控制，默认关闭。
- 64项测试全通过，Release零警告错误，最新局8确认/14原始/6重复等指标未回归；ZIP白名单与全部文件哈希验证通过。
- 发行、独立Release和profile的DLL SHA256均为B85E42B5D622CD8FF5C3A09B1CC5BB61D67B10C74C069341A5CA52C4A8D41316。未上传Thunderstore，Unity实机验证仍保留为已知边界。

## 2026-09-08 复盘报告重建与旧结论更正

- 当前入口：`REPORT_REBUILD_2026-09-08.md`；schema3/collectionRevision3/analysisVersion5，62项测试通过，Release已部署，Unity实机未验收。
- 最新局168 chunks / 28500 samples / 1676 inventory / 38674 events全部只读回放；127处时间回退、6个进度点，末段覆盖到局末。
- 死亡为8次确认、14条观察、6条重复。秋沫离1/4、云呢4/5、乌鸦吃鱼2/4（确认/原始）；不能继续使用下面直接累加的旧死亡数。
- 排除死亡暂存点2世界单位以内的漂移；保留真实远距离，异常原值和来源可查。全部库存快照处理，不因回退整块丢弃。
- 六页报告、联合证据、风险/关系/贡献、物品搜索、命名弹窗和2-4局同玩家比较已接入；确认/原始/重复/未知分开。
- 撤回下面“无山段目录”“算法无需更新”“StateKeeper已排除为卡顿来源”的说法；插件入口0.00ms/frame不覆盖采集器/UI/后台。新增实际工作计时，但实机开销仍待测。
- 用户退出游戏后部署成功；不修改原始局，旧派生缓存自动重建，不需要再录两小时。验收边界详见新报告。

## 2026-09-08 最新六人局与卡顿核查

> 历史错误结论归档：本节中的死亡次数、无山段目录、无需更新算法及排除StateKeeper卡顿的判断已被上节撤回，禁止继续引用为现状。

- 新局 `f1e61e22-26d3-4ca1-af87-0ff794ae5d0d`：Victory、6 人、约 5741.7 秒（95.7 分钟）、28,500 samples、1,676 inventory snapshots、38,674 events；分析缓存为 `analysisVersion=4`。
- 新局玩家统计：Jolyne 313 跳/0 死/2 昏迷，爬树猪 1317/0/1，乌鸦吃鱼 840/4/5，云呢 442/5/3，小小小七 1097/1/2，秋沫离 760/4/1。原始体力、额外体力、石化和 15 类状态均有进入分析；体力最大值超过 1 仍按原始游戏数值保留，不按绿色条猜测。
- 新局包含 6 次 `PlayerRescuePulled`、7 次 `PlayerRevivedObserved`，但没有可确认施救者，因此没有虚构救援者排名；`friendHealingAmount` 均为 0。
- 新局结果包含 Hunger 48、ExtraStamina 21、Injury 6、Poison 5 条 Likely 效果，其余大量状态变化保持 Ambiguous。发现 3 次重复/并存 GUID 冲突（约 2017.577、2382.987、5114.574 秒），当前算法正确保留竞争解释并降级置信度；无需新增字段或重录才能处理。
- 新局没有山段目录，面板应继续只显示“整局”，不能从纵向坐标猜测山数。
- WhySoLaggy 本次日志显示 StateKeeper 每帧约 0.3-0.7 ms 总计/约 0.00 ms 每帧，未成为卡顿来源；最高记录为 `StateKeeper total=0.7ms`。出现多次 60-87 ms 卡顿，以及 962 ms、1086 ms、1639 ms 的严重停顿，但插件归因主要为空，或只列出 WhereIsThing/Better Player Distance 各约 0.2-3.1 ms，不能归咎 StateKeeper。
- 同期 Abuse 日志在结束附近出现 50-300/s RPC flood、`SyncAfflictionsRPC` 单人最高 186/s、PhotonView +111/+124、Instantiate/Destroy flood；更符合联网对象/状态同步或场景切换压力。StateKeeper 没有新增 RPC，日志中也无其异常。


## 2026-09-08 基础归因流水补全

- 见 `ATTRIBUTION_ENGINE.md`。新增并接入ItemAttributionEngine/RecordedItemRules，覆盖事件与库存候选、受益者、竞争解释、时延、持续/到期、数量预算、烹饪和安全未知分支。
- 物品事实与效果归因分开显示；新增逐项理由与候选展示。schema仍3，analysisVersion现4，旧派生缓存自动重建。
- 40项测试全部通过，含25项专门归因测试、缓存重建和六局只读回归。最新局5条跨人候选仍保留，4条至少有一项Likely，绷带因旧statusType缺失保留Ambiguous。最终核查补齐直接治疗去重与烹饪元数据更新保留直接证据规则。
- 基础算法不再等待新局/新字段才能运行。未知机制/未录参数仍不能变成确定因果；不要求重新打两小时。实机采集/UI/帧时间需短时验证。

## 2026-09-08 互喂与救援接续实现

- 最新状态见 `RESEARCH_ASSISTANCE_2026-09-08.md`；此节晚于下方“仅调研”，本轮确实修改源码并编译。
- 纠正可见范围：GetFedItemRPC定向给被喂角色Owner；Consume消费者ID全员广播。最新182条消耗中5条跨玩家GUID候选，C#六局回归已确认，不需重录长局。
- 直接治疗、越过昏迷阈值、救援爪拉回启动、未知施助者复活分别记录；主机/库存主人不自动成为救援者。旧局救援能力未知，不能显示零次排名。
- 补齐交接时未完成的面板容器/方法，新增可读物品页、状态条、距离曲线与导航接线。schema仍3，analysisVersion现3。
- 14项测试通过，含813分块六局只读回放。Unity实机、联网三方和帧耗时尚未验证；完整候选归因和视觉改造尚未全部完成。

## 2026-09-08 采集与分析审计更正

- 最新审计见 `RESEARCH_2026-09-08.md`，复现脚本与六局结果见 `research/`。本轮只改调研资料，没有改插件源码、DLL、原局或游戏缓存。
- 六局 813 chunks / 136584 samples / 8277 inventory / 277356 events 全量读取并核对计数；旧五局均存在不同程度的样本时间倒退，最新局无倒退但有较长采样间隔。
- 更正：普通和额外体力已采集；`statuses[Petrify]` 不能表示实际石化。游戏在独立 `CharacterData.petrifyAmount` 保存并同步真实石化，原版 UI 也读该属性。旧局缺失应显示未知，不能当 0。
- 更正：当前分析没有实现基于状态、受益者与竞争候选的物品效果关联，置信度主要是事件类型映射。已有最新局包含可核对的跨玩家绷带、运动饮料、热狗、蘑菇，以及治疗护符令 Hot 下降但体力不变的例子，不需要再录长局才能改进分析。
- 最新局 216 条资源事件有 41 条前后数值相等；资源 availability 转变与数值差分需分离。普通体力存在超容量原值，不能据合计峰值推断使用了更多物品。
- 建议仅为精确石化条新增一种高频观测值；其余优先修正既有事件 actor/consumer 语义、补低频版本/定义/随机上下文与观察有效性。不补石化也不阻塞其他四视图。
- 后续先用已有六局和合成反例验证，再做 UI 和短时定点接线验证；不要求用户为了分析界面重新打两小时。详细字段分级、算法、证据、未验证边界均在新报告。

## 2026-09-07 首局 schema 3 长局复核

- 新长局 `27276489-b68f-46eb-9993-1dfd89ffbed4`：Victory、4 名稳定玩家、约 6159.7 秒、174 个连续分块、29,454 samples、1,366 inventory snapshots、38,441 events、194 item definitions。
- 采样间隔均值约 0.2091 秒、P99 约 0.244 秒，0 次时间倒退；29,378/29,454 帧含完整 4 人，适合整局体力、跳跃、距离和物品流水分析。
- 8,144 个非空物品快照、329 个 GUID；1,998 条带 GUID 的物品事件中 1,944 条可连接到库存实例，关联率约 97.3%。
- 新字段观测到有效转移：uses 58、useRemaining 121、fuel 54、cookedAmount 54、powerEnabled 6、flareActive 2；ItemUses 条目覆盖 8,076，但有效值仅 1,658，证明双层 presence 设计必要。
- 5,111 条 PlayerJumped 事件按四人分布为 1,923/1,501/794/893，极短间隔很少，`Character.OnJump` 可作为实际跳跃基础事件。
- 六局可开始做整局基础分析；旧五局 schema 2 只作为体力、距离、库存和事件量基线，实例级物品归因优先使用 schema 3。
- 当前数据没有显式山/地图段标识，虽然纵向轨迹从约 35 升至 2,100 以上，但不能可靠切出用户所述 9 座山。逐山分析需要新增低频地图/关卡切换事件或阶段索引。
- 待修正：Mandrake 的 `Used` 使用普通 `BoolItemData`，当前只读取 `OptionableBoolItemData`；同一扫描多个资源同时变化时，单个 `resourceKey` 只能表示第一个变化，分析器必须以完整快照差分为主。

## 2026-09-07 暂停菜单入口生命周期修复

- 参考 ClimbInfoTracker 的暂停菜单重建逻辑，给 StateKeeper 菜单按钮增加底层 Unity 对象存活检查。
- 新增 `PauseMenuMainPage.OnEnable` 校验；暂停页重建或按钮被 Unity 清理后会重新创建入口，避免普通 C# 包装引用阻止重建。
- 入口仍放在“离开游戏”下方、空一个按钮位，并按实际原版按钮父容器坐标重新定位。

## 2026-09-07 schema 3 与面板基础版实现

- `RunRecord`、`RunChunk` 和 `RunIndexFile` 已切换到 schema 3；schema 2 active/index 数据会被忽略且不自动删除，不参与新面板索引。
- `ItemSnapshot` 增加有效次数、PetterItemUses、Used、FlareActive、PowerEnabled 的条目/值字段；库存指纹和资源事件支持 GUID、前后 GUID、resourceKey、definitionKey。
- 物品移除事件使用 previous snapshot，避免空槽位把移除物品写成 `itemId=0`；`Character.OnJump` 增加低频 `PlayerJumped` 事件。
- 新增一次性 `ItemDatabase` prefab 定义扫描，保存 Action 触发器/参数摘要、Component 和烹饪类型，不执行 Action。
- `RunStore` 增加历史/收藏查询、按 runId 收藏切换、40 字符名称清洗与重命名，并把摘要统计写入 index；新增暂停菜单入口、历史列表、收藏/命名操作和详情占位页。
- 单元测试当前 5/5 通过；尚未完成 PEAK 实机 UI、物品语义和性能基线验证。

更新时间：2026-09-06

## 2026-09-06 当前停点

- 项目从未发布的 `PeakRunAnalytics` 完整改名为 `StateKeeper`：程序集、DLL、命名空间、插件类、GUID、BepInEx 配置、开发工程、测试工程和数据目录均已同步。
- 版本保持 `0.1.0`；profile 中只加载 `StateKeeper.dll`，旧 DLL/PDB 已移到 `MOD开发/StateKeeper/迁移备份/`。
- 本机旧数据已从 `LocalLow/LandCrab/PEAK/PeakRunAnalytics` 一次性移动到 `LocalLow/LandCrab/PEAK/StateKeeper`；旧目录不存在。用户确认正式发布不会改名，因此源码中的旧路径兼容迁移代码已删除。
- 旧 BepInEx 配置已改名为 `com.local.statekeeper.cfg`，保留 `Enabled`、`FavoriteHotkey`、`DebugLogging` 和 `StaminaEventThreshold`。
- `StaminaEventThreshold` 默认 `0.01`，绝对变化量达到阈值即记录；加入 `0.000001` 浮点比较容差，阈值为 0 时也不记录零变化调用。5Hz 体力采样不受影响。

## 2026-09-06 后续计划更新

- 展示名称确定为 English `STATE KEEPER`、中文“状态分析”；程序身份和版本 `StateKeeper` / `0.1.0` 不变。
- `PLAN.md` 已统一收录此前的一局结束判定、完整采集范围、实机数据质量问题、对其他客机影响和未来可视化方向，作为后续实现的单一计划入口。
- 采集碎块只作为采集态格式；后续在后台按局合并为整体 `*.data.json.gz`，通过独立合并进度条显示，校验成功前不删除碎块。
- 后续面板计划使用 WhereIsThing 的原生 UI/字体风格，从 ESC 暂停菜单红框位置进入，收藏功能移入面板并删除 F8 快捷键。
- 后续增加独立分析进度条和可取消任务接口；基础分析按五局样本规则实现，复杂评分暂不实现。
- 调研确认 PEAK-MAP 后续需要独立 StateKeeper 提交 API、Supabase 表和私有 Cloudflare R2 前缀；客户端/服务端双重脱敏、用户明确同意、可配置关闭。当前未改网站源码。

## 2026-09-06 历史调研补档

- 已把此前分散在对话中的完整采集边界、RunId/正式结束判定、数据模型、模块拆分、8 人 2 小时性能优化、验收矩阵和网站接入边界统一写入 `PLAN.md`。
- 已归档 5 局样本汇总：约 107,130 个样本、6,911 个库存快照、238,915 个事件、约 36.7 MB；约 4 局 Victory、1 局 Aborted。
- 已记录两局较新样本：`a120f547...` 约 97 分钟、历史 8 人、单帧 4-7 人；`635b97e3...` 约 86.5 分钟、历史 11 人、单帧 5-10 人，实际采样约 4.8Hz。
- 已记录旧异常局 `77f6bbb7-b690-4b95-9ee1-3b5091aea50d` 的时间轴从约 1719 秒重置到接近 0 秒；后续分析必须显示数据质量警告。
- 当前数据适合先做概览、同步时间线、距离关系、物品流水和局间趋势原型；样本不足以支持综合玩家评分、最佳队友和复杂战术评级。

## 2026-09-06 五局数据复核与分析范围收敛

- 重新读取 `StateKeeper/Runs` 中五局：`98fc17ac...`（21,367 samples / 1,085 inventory / 87,678 events）、`77f6bbb7...`（8,956 / 459 / 9,477）、`67eb3a83...`（23,520 / 1,932 / 61,305）、`a120f547...`（28,302 / 1,204 / 34,863）、`635b97e3...`（24,985 / 2,231 / 45,592）。五局合计与 manifest 一致：107,130 samples、6,911 inventory snapshots、238,915 events。
- 事件合计中 `StateChanged=158,310`、`StaminaChanged=70,405`、`ItemRemovedObserved=3,113`、`ItemChangedObserved=3,211`、`ItemResourceChanged=1,443`、`ItemConsumed=906`、`ItemUsesReduced=390`。物品移除明显多于明确资源减少，因此不能把移除直接显示为使用。
- 五局均能看到库存变化、体力曲线和两两距离，确认第一版只依赖这三类数据即可制作概览、玩家时间线、物品流水和队伍关系；完整物品 Action 监听暂不扩张。
- 距离复核发现死亡对象的 `+-5000` 哨兵位置以及少量异常超大距离；分析层需保留原始值，但排除无效端点、单独记录质量警告，并优先使用中位数/P90/持续时间等稳健统计。
- `PLAN.md` 已新增五局样本驱动的基础分析算法，覆盖双时间轴、物品流转置信度、体力区段和有效距离视图；源码和发行目录本轮未修改。

## 性能优化

- 高频数据约 30 秒一个 GZip JSON 分块，主 JSON 只存索引和元数据。
- JSON 序列化、压缩和写盘在串行后台队列完成；正式结束也不再同步压缩阻塞 Harmony 主线程，正常退出仍会 flush。
- 慢盘情况下跳过过时的未封存检查点；已经封存的分块写入不会被跳过。
- 玩家身份在高频数据中使用整数索引，异常状态为固定顺序 `float[]`，两两距离为索引数组与距离数组。
- 角色对象列表每 0.5 秒刷新，位置仍每 0.2 秒读取；距离直接使用同一帧已采集位置，避免重复 `Center` 访问和字典查找。
- 库存轮询从 5Hz 降为 2Hz，先比较值类型指纹，变化时才创建完整快照和事件；关键物品动作仍由即时补丁记录。
- 槽位名称改为静态字符串表，采样取整改为轻量浮点运算。

## 实机数据调研

- 已解析迁移后的 128 个 GZip 分块：序号 0-127 连续、0 个解析错误、压缩 4.80 MiB、解压 88.69 MiB、压缩率约 18.47:1。
- 约 74 分 51 秒中有 21,367 个采样，平均 4.79Hz；20,013 个间隔落在 0.18-0.22 秒，556 个间隔大于 0.3 秒。
- 事件 87,678 条，其中体力事件 65,150、状态变化 20,927；这批数据来自阈值优化前，不能代表当前构建。
- 一局累计见过 14 个身份，单次采样为 4-10 个角色；后半段长期稳定为 6 个。需要新局确认 9-10 个角色是实际加入/离开还是离场角色对象暂存。
- 发现 11 次采样时间倒退，最大约 1.353 秒；当前尚未增加单调时间保护。
- 分块实际多为 152-172 个样本，平均约 166.93，因为只在 5 秒检查点发现达到 150 样本；数据完整，但不是严格 150 样本封块。

## 验证结果

- `dotnet build MOD开发/StateKeeper/StateKeeper/StateKeeper.csproj -warnaserror`：0 warnings / 0 errors。
- `dotnet test MOD开发/StateKeeper/StateKeeper.Tests/StateKeeper.Tests.csproj -warnaserror --no-restore`：2/2 通过。
- 当前 profile DLL：`StateKeeper.dll`，文件/产品版本 `0.1.0.0`。

## 2026-09-07 物品定义与实际结果分析

- 全量读取五局 639 个 GZip 碎块：6,911 个库存快照、238,915 个事件、25,007 个非空物品条目、115 种物品组合；非空条目均有 GUID。
- 当前字段覆盖/同 GUID 变化：`ItemUses 24,798/610`、`UseRemainingPercentage 7,954/638`、`Fuel 1,767/297`、`CookedAmount 23,878/305`。
- 事件量再次确认 `ItemRemovedObserved=3,113` 不能等同使用；`ItemResourceChanged=1,443`、`ItemConsumed=906`、`ItemUsesReduced=390` 才是较强辅助证据。
- 新增 `ITEM_ANALYSIS.md`，确定 `ItemDefinition`、`ItemState`、`ObservedOutcome` 三层模型，以及基于 GUID 轨迹、资源字段差分、体力/状态/距离时间窗和置信度的第一版算法。
- 计划补齐 `Used`、`PetterItemUses`、`FlareActive`、`PowerEnabled`；`SpawnedBees`、`Scale`、`Color`、`ScreamTime`、`InstanceID` 按面板需求延后。当前未修改源码和发行目录。
- 复核发现 `OptionableIntItemData` 需区分条目存在与内部 `HasData`；旧快照的 `hasUses` 不能直接当作有效使用次数。定义目录建议运行时只读扫描 `ItemDatabase` prefab，避免手工维护 115 种物品。

## 2026-09-07 实现前缺口审计

- `PlayerTelemetry.statuses[]` 已覆盖 Hunger、Injury、Poison 等普通状态，不需要重复增加独立饥饿/生命字段。2026-09-08 更正：Petrify 虽有枚举槽，实际值另存 `CharacterData.petrifyAmount`，此前将其视为已覆盖不正确；见新审计。
- 必须追加：有效 `ItemUses` 语义、`Used`、`PetterItemUses`、`FlareActive`、`PowerEnabled`，并让物品事件保存 GUID/资源字段，保证事件与实例轨迹可连接。
- 若需求中的“跳变化”包含实际跳跃统计，建议只补 `Character.OnJump` 的低频 `PlayerJumped` 事件，不增加高频样本字段。
- 后续定向调研集中在 ItemDatabase prefab 无副作用读取、特殊物品使用顺序、RPC 事件重复、库存流转边界、OnJump 语义和长局性能基线；暂不扩展到所有 Action 或高频视觉字段。
