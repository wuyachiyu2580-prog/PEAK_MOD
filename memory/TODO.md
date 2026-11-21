# 全局待办

## 2026-11-18 已部署 2.0.a，等待实机验证

按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。

下一步在2.0.a启动游戏，进入实际扫描场景后开启两MOD的Diagnostics/PhaseTiming；默认45秒自动关闭。先保留现有配置做对照，再读取阶段报告定位5秒尖峰。自动测试仍为WhySoLaggy 105项、WhereIsThing 184+104项；本次仅部署，未重跑测试。

## 2026-11-18 实测修复已实施，隔离验收通过

WhySoLaggy已接入预录LossLedger及范围/未知披露、独立紧凑网络存储与2%保护/5%网络配额、SelfMetrics对象复用、默认关闭的30–60秒阶段计时、默认Both的可选持续日志格式。105/105正式测试、真实依赖Release零警告零错误；10包回放保持53,651帧、网络3,297条零字段差异、补回329条且无新增旧事件丢失。剩余253条预录明细以211–262上下界披露；最终共享预算0。

WhereIsThing已加入默认关闭、45秒默认/30–60秒范围的阶段计时，覆盖网络清理、重试、20个场景步骤、标签刷新/投影及Update，并记录峰值上下文。184+104=288项回归通过，真实依赖Release零警告零错误。未改扫描语义；5秒尖峰尚待实机归因，不宣称性能问题已消除。

已按用户“开始/继续”完成正式修改；构建标识保留20261117.playtest。当前未部署、未启动游戏，66个安装DLL/PDB/cfg及历史发行文件核验不变。旧“未实施”段落仅为历史，以本条为准。

下一步：部署后在实际扫描场景采集45秒阶段报告并做开关对照，再决定雕像缓存/网络清理分批方案；继续晚安莓、护符、共享与菜单实机矩阵。Unity Mono收益仍待验。

[实施报告、配置用法、隔离产物与哈希](IMPLEMENTATION_PlaytestFixes_2026-11-18.md)。

## 2026-11-17 实测修复方案已调研，未实施

已用生产EventRing逻辑回放，本次10份ZIP的Sequence集合逐条一致。单纯新增网络分区挤占明细方案否决；紧凑NetworkSample+保护摘要候选在相同区间将缺失582降至253（补回329），无新增原事件丢失，53,651帧保持，3,297条网络字段还原与5组压力检查通过。剩余预录明细仍须LossLedger正确披露；14项时间窗计数策略检查通过但未接入生产。SelfMetrics复用对象验证减少Tuple分配，非Unity性能承诺。

已确认67个Update峰值≥8ms；57个同录制间隔中56个在4.5–5.3秒，中位5.051885秒。场景雕像发现与网络记录清理均为5秒，不能直接认定单一根因。首选先限时阶段计量，再按证据采用sceneGeneration缓存或分帧清理；保留护符随机偏移、FakeItem隐藏/恢复、附加场景/延迟生成及晚安莓语义。

用户本轮只授权先调研。仅新增隔离实验与报告，未改正式源码/正式测试/cfg/安装DLL/发行，未部署或启动游戏；87项文件哈希核验不变。研究运行在.NET 10，不是Unity Mono实机；不能把候选回放和旧78/281正式测试相加，不能标记问题已修复。

[修复设计、被否决方案与验收边界](RESEARCH_PlaytestFixes_2026-11-17.md)。实验位于.build/playtest-fix-research-20261117/；仅同步本地memory。

## 2026-11-17 一局实测复核：运行确认，仍有待修缺口

WhySoLaggy retention构建已有本局运行证据：10份保留ZIP、53,651帧均连续，约20秒预录完整；但8份包报告关键事件省略合计328条，按Sequence对照实际少582条，预录部分省略未传入manifest。主线程SelfMainMs在保留Gameplay段均值0.249ms、P99 1.628ms、峰值85.046ms，需分阶段取证。写盘/RPC队列丢弃为0；CSV+JSONL合计约603MiB，JSONL已轮换掉本局前段。

WhereIsThing fixes构建正常加载：700份周期报告累计Update 699,949次、全计时覆盖、0异常，均值0.117934ms/调用；诊断窗口反复出现约5秒一次的8–15ms尖峰，最大14.7521ms。5秒场景校验/Statue全局查询为优先候选，尚未证实具体子方法。退出配置RestrictClientPresets=false、Persistent；保留用户现值，默认仍true。晚安莓/共享/菜单等功能矩阵不能仅凭日志判通过。

本局CSV覆盖19:38–21:34，安装DLL与部署哈希一致。Player.log中Compass相关空引用17,124条，不能误归因到这两个MOD；GetDisplayName/LOC: 0未复现。此次仅审计并保存报告/证据，未改生产源码、配置、DLL或发行，未启动游戏。当前属于部分实机验收，覆盖下方从未实机的旧阶段状态；旧记录保留。

[本局完整审计、数据口径与待修优先级](PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md)。证据：.build/playthrough-20261117/；只同步本地memory。

## 2026-11-17 WhereIsThing 已部署实测目录，实机待验

按用户“放到实测目录”要求，2026-11-17 00:07:42 +08:00 已将 0.1.3-dev.20261116.fixes 的 DLL/PDB 部署到 2.0.a profile 的 BepInEx/plugins 根目录，复制前确认游戏未运行。旧子目录 DLL 已备份并移除，递归核对仅各一份 DLL/PDB；配置哈希不变。

当前状态以本条为准；下方 2026-11-16 的“未部署”等描述保留为历史。184/184 helper 与 97/97 状态流程测试共 281 项逻辑检查通过，Release 构建 0 警告 0 错误，不代表实机通过。下一步启动 2.0.a profile 实测；尚未启动游戏、未实机验证、未发行或提交。

[实施与部署记录](mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md)。哈希、安装/备份路径详见该记录；仅同步本地 memory。

## 2026-11-16 WhereIsThing 审查修复已实施，未部署

用户已授权开始修改。共享会话/发布失败重试、目录自动就绪补发、无关属性过滤、扫描计时与菜单生命周期已修；有效选择缓存、标签绑定/分类复用、距离与设置局部更新及确定残留清理已完成。184/184 helper + 97/97 生产流程回归，真实依赖 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.fixes，未部署/实机/发行/提交。

WhereIsThing 审查四类缺口及低风险优化已落地，下一步为实机与性能测量；上轮“先修共享/计时”待办已完成，复杂优化仍未执行。

[实施与验收记录](mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md)。仅同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 修复方案已调研，未实施

用户要求先调研如何修。确定共享按 Room 会话/actor/模式去重、目录就绪自动补发、无关/重复属性过滤、内容刷新与计时分离；保留协议 2、旧配置、默认客机限制和三秒房主过渡。隔离候选 47/47 检查通过，不能当作生产修复或实机通过。

WhereIsThing 待办顺序已细化：正确性 → 生命周期/内部残留 → 低风险优化 → 实测后复杂优化。仅调研，问题尚未在生产代码修复。

[修复方案与验证边界](mods/WhereIsThing/RESEARCH_Fixes_2026-11-16.md)。只同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 整体审查完成，问题未修复

确认四类状态流问题：相同 payload 跨房/模式漏发布；目录解析后未重新发布；无关房间属性续时并重建窗口；外部常驻→定时保留无穷截止。现有 184/184 回归仍通过，新增隔离审计使用真实方法体和测试替身，未做实机。生产源码未改，问题尚未修复。

WhereIsThing 新待办：先共享与计时，再清内部残留、测性能；原晚安莓实机验收继续待办。本轮仅审查，未实施/部署。

[完整审查报告](mods/WhereIsThing/REVIEW_2026-11-16.md)。仅同步本地 memory；本次没有可用的外部 update_memory 接口。

## 2026-11-16 晚安莓统一标注已实施，未部署

用户“开始”已授权实施：食物分类中的单一晚安莓条目覆盖未摘/已摘；未摘锚定果实，隐藏/采摘/更换后旧标签失效，空株不再标记。Ground控制未摘，已摘沿用地面/手持/背包规则；旧itemID或场景token预设兼容，取消勾选清除两者，共享协议不变。184/184逻辑回归、Release 0警告0错误。开发标识0.1.3-dev.20261116.napberry；仅隔离产物，未部署/改cfg/实机/发行。

下一步仅实机：自己/他人采摘、拒绝恢复、晚加入、已摘物品位置切换、旧预设与主客机、段范围/距离/中文布局。其他毒浆果/蘑莓/烹饪候选仍未实施。

[实施与验收记录](mods/WhereIsThing/IMPLEMENTATION_Napberry_2026-11-16.md)。仅维护本地memory。

## 2026-11-16 晚安莓统一标注可行（仅方案）

用户追问统一标注可行性：可用单一“晚安莓”入口/标签，未摘定位果实，采摘后撤掉植株标签并沿用物品扫描；内部仍分FakeItem/Item两条发现路径，旧物品ID/场景选择应兼容。沿用先调研要求，未改生产代码、配置、构建或部署。此前空株误标缺口仍未修复。

[方案与边界](mods/WhereIsThing/RESEARCH_Napberry_2026-11-16.md)。

## 2026-11-16 晚安莓植株扫描调研（未改代码）

只选普通晚安莓不会自动扫描植株；需另选自然危险中的“未摘下的晚安莓”。实际扫描NapBerrySpawn植株对象，未检查fakeBerry采摘/激活状态，采摘后若植株仍激活会残留标签。已用3.1源码与sharedassets4资源交叉核查，未改生产代码/配置、未构建部署或实机。该旧缺口未修复，也不在此前143项回归覆盖内。

[调研证据与修复边界](mods/WhereIsThing/RESEARCH_Napberry_2026-11-16.md)。只维护本地memory。

## 2026-11-16 客机配置与改版整合完成，额外识别仅调研

用户认可的改动已整合到开发源码：客机 RestrictClientPresets 默认 true（本地偏好），本地多预设并集、放置物门控、蘑菇毒/无毒品种及 Clone 识别、地面过滤与标签范围优化。143/143 逻辑回归，当前 PEAK 3.1.0 隔离 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.presets；未部署、未实机、未改发行。

待实机主客机切换、多选/旧配置、蘑菇/距离显示；额外候选只调研。下方未安排移植为历史。[详情](mods/WhereIsThing/IMPLEMENTATION_PresetsAndTraits_2026-11-16.md)。

## 2026-11-16 WhereIsThing 对比完成

新增反编译的 [差异报告](mods/WhereIsThing/COMPARISON_Renlei_0.1.3.1_2026-11-16.md) 已完成。用户未要求移植，本轮不新增自动实施任务；开发版实机待办保持。

## 2026-11-16 录制保留修复已部署，实机待验

已完成上次实机反馈后中断的录制保留修复：帧/关键事件/明细分区，方法按秒汇总并保留有限尖峰；导出显示真实覆盖时间及分类省略。78/78回归通过，Release零警告零错误。构建1.0.5-dev.20261116.retention（插件仍1.0.5）于00:19安装2.0.a/plugins根目录，仅一份DLL/PDB，旧版可回退，用户配置哈希未变。发行未改、未上传/提交。

[WhySoLaggy修复及下一步](mods/WhySoLaggy/IMPLEMENTATION_Retention_2026-11-16.md)。实机性能与联机矩阵仍未完成。

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](mods/WhySoLaggy/IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 WhySoLaggy 原症状反馈通过

- LOC: 0 已获用户实机通过反馈；完整语言/主客机/开关/性能矩阵仍待验。
- [候选优化](mods/WhySoLaggy/RESEARCH_Roadmap_2026-11-14.md)：先准确性、后限时取证/诊断包，再 HUD/网络，最后国际化。本轮只调研，未实施新功能。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

[WhySoLaggy 实施与验收](mods/WhySoLaggy/IMPLEMENTATION_ModConfig_2026-11-13.md)：已部署 2.0.a/plugins 根目录单份 DLL、旧 DLL 备份、配置未变。实机工具截图超时，完整 profile 界面验收待完成；未发行或提交。

## 2026-11-13 下一步方案已调研，未实施

WhySoLaggy 修复执行顺序与范围见 [下一步方案](mods/WhySoLaggy/NEXT_STEPS_ModConfig_2026-11-13.md)：先计时入口、再安全开关/并发统计，国际化最后，隔离构建后完整 profile A/B。只调研，部署与正式修复未执行。

## 2026-11-13 补充：旧版本同样具备触发条件

WhySoLaggy LOC: 0 并非已证实的 PEAK 3.1.0 新缺陷：历史源码/依赖及独立 Harmony 实验说明旧版也有条件触发。本次 ShootZombies 继承目标与启用补丁计时组合相关。详见 [历史调查](mods/WhySoLaggy/HISTORY_ModConfig_LOC0_2026-11-13.md)。无生产代码/配置/安装变更。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

WhySoLaggy 的共享按键设置异常会中断其他 MOD 菜单；优先修计时注入，补多 MOD/多语言实机和真正执行补丁的回归。配置热更新另行明确。见 [专项调研](mods/WhySoLaggy/RESEARCH_ModConfig_3.1.0_2026-11-13.md)。本轮只调查，未修复或部署。

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

WhereIsThing 0.1.3 发行五件套已完成（沿用 0.1.2 格式），不再列为未准备。仍待植物/箱子/关卡范围/语言字体/主客机实机验收；无新 ZIP、未上传。DLL 已在 2.0.a/BepInEx/plugins 根目录，版本已获用户确认。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

WhereIsThing 0.1.3 已实施、71 项逻辑回归通过、Release 0/0 并部署单份到原 WUYACHIYU-WhereIsThing 子目录。下一步为主客机、地形植物、跨段/自定义地图、箱子与语言 UI 实机；不再停留只调研。详见 [实施记录](mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md)。未准备发行包/ZIP。

## 2026-10-29 基线提醒

WhereIsThing 0.1.3 已完成更新前调研，尚未实施；专属任务见 `mods/WhereIsThing/TODO.md`。本机游戏 DLL 已异于 10 月 23 日 3.0.0 基线，资源版本标记 3.0.1；后续开发重新核对当前安装 DLL，不自动批量改其他 MOD。

## 2026-10-23 PlayersInfo 复核补充

[PlayersInfo 专项报告](mods/PlayersInfo/COMPATIBILITY_3.0.0_2026-10-23.md)：补查 2.4.b → 3.0.0，0.2.5 暂无确定的强制 API/核心读数适配项；新增昵称读取顺序维护建议，并保留 3.0.0 实机验收。terrain 中 0.1.1 仍需更新。仅更新审核记录，源码及部署未改。

## 2026-10-23 PEAK 3.0.0 兼容性审核

- 待用户后续安排实施：修 ItemInfoCN 的 Mandrake/StatusField；修灯笼整合 MOD 的背包和点灯 API/同步；TerrainCustomiserCN 建立 3.0.0 引用/依赖并迁移生成/光照。
- WhySoLaggy 测试目录需要从写死 2.4.b 更新；63 个 watched RPC 已独立核验仍在。
- terrain profile 的 PlayersInfo 0.1.1 和 ItemInfoCN 1.0.0 为失效旧 DLL，使用前需核对更新；本轮未自动覆盖。
- 其余通过构建项目需补 3.0.0 实机；StateKeeper 跨平台身份比较是条件性增强项，不判定 Steam 采集全面失效。
- 完整结果：[COMPATIBILITY_3.0.0_2026-10-23.md](COMPATIBILITY_3.0.0_2026-10-23.md)。

各 MOD 的专属待办已迁移到 `memory/mods/<ModName>/TODO.md`。历史完整清单保存在 `archive/TODO-2026-09-09.md`，冻结项目不应自动执行其中内容。2026-10-12 PeakWorkshopCN 与 PeakWorkshopCNScanner 已作废，专属待办停止执行，历史资料见各自作废入口。

当前跨项目事项：

- 四 MOD ModConfig 更新已构建部署，发行文件五件套已备齐（无 ZIP、未上传）（PlayersInfo 0.2.5、WhySoLaggy 1.0.5、两个定位 MOD 0.1.2/1.0.4）；剩余实机矩阵见 `mods/ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`，Lantern 和公共依赖仍另行处理。

- 完成 ModConfig 本地化公共依赖修复和多 MOD 语言切换复测。
- 完成 StateKeeper 与 PEAK-MAP 的接口、隐私和性能验收。
- 继续维护 memory 结构检查，避免状态、索引和临时摘要分叉。
