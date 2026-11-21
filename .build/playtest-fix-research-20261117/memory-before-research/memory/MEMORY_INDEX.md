# Memory Index

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

新增 mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md，索引修复内容、281 项逻辑验证、隔离产物及未完成实机范围。

[实施与验收记录](mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md)。仅同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 修复方案已调研，未实施

用户要求先调研如何修。确定共享按 Room 会话/actor/模式去重、目录就绪自动补发、无关/重复属性过滤、内容刷新与计时分离；保留协议 2、旧配置、默认客机限制和三秒房主过渡。隔离候选 47/47 检查通过，不能当作生产修复或实机通过。

新增 mods/WhereIsThing/RESEARCH_Fixes_2026-11-16.md：可执行修复设计、兼容边界、47 项隔离证据和正式验收清单。

[修复方案与验证边界](mods/WhereIsThing/RESEARCH_Fixes_2026-11-16.md)。只同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 整体审查完成，问题未修复

确认四类状态流问题：相同 payload 跨房/模式漏发布；目录解析后未重新发布；无关房间属性续时并重建窗口；外部常驻→定时保留无穷截止。现有 184/184 回归仍通过，新增隔离审计使用真实方法体和测试替身，未做实机。生产源码未改，问题尚未修复。

索引新增 REVIEW_2026-11-16.md（问题、残留、性能候选、证据等级与后续验收）；同步 WhereIsThing 六件套/current 与全局入口。

[完整审查报告](mods/WhereIsThing/REVIEW_2026-11-16.md)。仅同步本地 memory；本次没有可用的外部 update_memory 接口。

## 2026-11-16 晚安莓统一标注已实施，未部署

用户“开始”已授权实施：食物分类中的单一晚安莓条目覆盖未摘/已摘；未摘锚定果实，隐藏/采摘/更换后旧标签失效，空株不再标记。Ground控制未摘，已摘沿用地面/手持/背包规则；旧itemID或场景token预设兼容，取消勾选清除两者，共享协议不变。184/184逻辑回归、Release 0警告0错误。开发标识0.1.3-dev.20261116.napberry；仅隔离产物，未部署/改cfg/实机/发行。

此条覆盖前一阶段仅方案状态；不将逻辑回归视为实机通过。

[实施与验收记录](mods/WhereIsThing/IMPLEMENTATION_Napberry_2026-11-16.md)。仅维护本地memory。

## 2026-11-16 晚安莓统一标注可行（仅方案）

用户追问统一标注可行性：可用单一“晚安莓”入口/标签，未摘定位果实，采摘后撤掉植株标签并沿用物品扫描；内部仍分FakeItem/Item两条发现路径，旧物品ID/场景选择应兼容。沿用先调研要求，未改生产代码、配置、构建或部署。此前空株误标缺口仍未修复。

[方案与边界](mods/WhereIsThing/RESEARCH_Napberry_2026-11-16.md)。

## 2026-11-16 晚安莓植株扫描调研（未改代码）

只选普通晚安莓不会自动扫描植株；需另选自然危险中的“未摘下的晚安莓”。实际扫描NapBerrySpawn植株对象，未检查fakeBerry采摘/激活状态，采摘后若植株仍激活会残留标签。已用3.1源码与sharedassets4资源交叉核查，未改生产代码/配置、未构建部署或实机。该旧缺口未修复，也不在此前143项回归覆盖内。

[调研证据与修复边界](mods/WhereIsThing/RESEARCH_Napberry_2026-11-16.md)。只维护本地memory。

## 2026-11-16 客机配置与改版整合完成，额外识别仅调研

- [WhereIsThing整合及识别能力调研](mods/WhereIsThing/IMPLEMENTATION_PresetsAndTraits_2026-11-16.md)：默认客机限制、143项回归、隔离构建、额外候选证据。
- 六件套/current已同步；外部update_memory接口不可用，仅维护本地memory。

## 2026-11-16 WhereIsThing 新反编译对比

- [renlei 0.1.3.1 与开发版0.1.3](mods/WhereIsThing/COMPARISON_Renlei_0.1.3.1_2026-11-16.md)：多预设、客机限制取消、放置物门控、蘑菇毒性和范围优化；仅静态对比，未移植/构建/部署。

## 2026-11-16 录制保留修复已部署，实机待验

已完成上次实机反馈后中断的录制保留修复：帧/关键事件/明细分区，方法按秒汇总并保留有限尖峰；导出显示真实覆盖时间及分类省略。78/78回归通过，Release零警告零错误。构建1.0.5-dev.20261116.retention（插件仍1.0.5）于00:19安装2.0.a/plugins根目录，仅一份DLL/PDB，旧版可回退，用户配置哈希未变。发行未改、未上传/提交。

[WhySoLaggy修复及下一步](mods/WhySoLaggy/IMPLEMENTATION_Retention_2026-11-16.md)。实机性能与联机矩阵仍未完成。

## 2026-11-15 开发构建已实施部署，实机验收待完成

落实用户批准的准确性、RPC/日志、自动深入、诊断包/HUD和最后国际化；1.0.5-dev.20261115，版本仍1.0.5，70/70自动回归，Release零警告零错误。游戏关闭后安装2.0.a/plugins根目录单份DLL/PDB，旧版可回退，配置SHA256未改变；发行未改、未上传/提交。

[实施、容量边界及验收记录](mods/WhySoLaggy/IMPLEMENTATION_Development_2026-11-14.md)。五模式实机A/B、30分钟四人、完整三语言界面验收未执行，不能记为通过。


## 2026-11-14 WhySoLaggy 后续调研

- [后续功能与优化](mods/WhySoLaggy/RESEARCH_Roadmap_2026-11-14.md)：现有能力、准确性/开销、候选优先级与验证边界。
- 用户确认原 LOC: 0 症状消失；完整矩阵仍待验。六件套和 temp/current 已同步。
- 证据：.build/whysolaggy-roadmap-20261114/。仅调研和本地 memory 更新，无新功能实施。

## 2026-11-13 修复已实施并部署，实机待验

用户已授权开始。计时目标规范化/去重、独立 Harmony id、主线程开关、并发统计和旧会话保护已实现，国际化说明最后更新。真实故障回归由 2/4 失败变为全部通过；最终 37/37 测试通过，Release 0 警告 0 错误。仍为 1.0.5，发行包不变。

[WhySoLaggy 实施与验收](mods/WhySoLaggy/IMPLEMENTATION_ModConfig_2026-11-13.md)：已部署 2.0.a/plugins 根目录单份 DLL、旧 DLL 备份、配置未变。实机工具截图超时，完整 profile 界面验收待完成；未发行或提交。

## 2026-11-13 下一步方案已调研，未实施

- [WhySoLaggy 下一步实施方案](mods/WhySoLaggy/NEXT_STEPS_ModConfig_2026-11-13.md)：三策略隔离验证、修改范围、配置语义、自动/实机矩阵与部署边界。未实施。

## 2026-11-13 补充：旧版本同样具备触发条件

WhySoLaggy LOC: 0 并非已证实的 PEAK 3.1.0 新缺陷：历史源码/依赖及独立 Harmony 实验说明旧版也有条件触发。本次 ShootZombies 继承目标与启用补丁计时组合相关。详见 [历史调查](mods/WhySoLaggy/HISTORY_ModConfig_LOC0_2026-11-13.md)。无生产代码/配置/安装变更。

## 2026-11-13 ModConfig LOC: 0 已定位，未实施修复

- [WhySoLaggy ModConfig 3.1.0 调研](mods/WhySoLaggy/RESEARCH_ModConfig_3.1.0_2026-11-13.md)：复现日志、泛型句柄异常、计时补丁归因、临时绕过与待实施方案。
- WhySoLaggy 六件套与 temp/current 已同步；common/07 更新 3.1.0 当前基线。只记录调查，修复未实施。

## 2026-10-31 已下载地图转发包已准备

- [修改] [离线地图包记录](RESEARCH_PeakWorkshop_0.5.0_OFFLINE_MAPS_2026-10-31.md)：新增用户下载转发目的、保留 _net/.forge-online.json 的 ZIP 策略及已核验的“自群山来”在线 r13 分享包。产物位于 地图素材/PeakWorkshop离线分享/。

## 2026-10-31 PeakWorkshop 0.5.0 离线地图包调研

- [新增] [RESEARCH_PeakWorkshop_0.5.0_OFFLINE_MAPS_2026-10-31.md](RESEARCH_PeakWorkshop_0.5.0_OFFLINE_MAPS_2026-10-31.md)：原生 .peakmap（ZIP）安装链路、单图资源完整性、CID 与 cache、远征依赖、当前 profile 样本和后续工具建议。仅调研，未实施。

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

- [修改] mods/WhereIsThing 六件套、PLAN.md、temp/current.md：0.1.3 发行五件套已备齐，用户确认版本笔误，保留实机待验状态。发行路径及 hash 见 mods/WhereIsThing/FILES.md。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

- [新增] `mods/WhereIsThing/IMPLEMENTATION_0.1.3_2026-10-29.md`：五项更新、国际化最后统一、71 项回归、3.0.1 构建、0.1.3 单份部署与实机清单。
- [修改] WhereIsThing 六件套、PLAN/current、根 README/TODO/CHANGELOG 同步当前已实施状态；旧同日调研保留为历史。

## 2026-10-29 21:36 3.0.1 反编译补充

- `common/07_PEAK版本与反编译基线.md`：当前反编译已切为 3.0.1，3.0.0 在作废目录。
- `mods/WhereIsThing/RESEARCH_0.1.3_2026-10-29.md`：12 个相关类比对一致，原五项调研结论保留；源码仍 0.1.2，未实施/构建/部署。同步 STATUS、FILES 与 temp/current。

## 2026-10-29 WhereIsThing 更新前调研

- `mods/WhereIsThing/RESEARCH_0.1.3_2026-10-29.md`：0.1.3 候选版本、截图语言、真实拾取身份（Mandrake_Hidden/MedicinalRoot/FakeItem）、箱子分组/去重/旧选择迁移、关卡范围过滤；用户只要求调研，未实施。
- 同步该项目六件套、PLAN 与 temp/current；当前源码/安装仍 0.1.2。游戏 DLL 已变化，资源版本标记 3.0.1，旧 3.0.0 审核不自动代表当前安装状态。

## 2026-10-23 PlayersInfo 复核补充

[PlayersInfo 专项报告](mods/PlayersInfo/COMPATIBILITY_3.0.0_2026-10-23.md)：补查 2.4.b → 3.0.0，0.2.5 暂无确定的强制 API/核心读数适配项；新增昵称读取顺序维护建议，并保留 3.0.0 实机验收。terrain 中 0.1.1 仍需更新。仅更新审核记录，源码及部署未改。

## 2026-10-23 PEAK 3.0.0 兼容性审核

当前游戏检查基线为 3.0.0，对照归档 2.6.b；12 个活动插件工程中 8 个隔离 Release 构建 0 警告/0 错误。ItemInfoCN、Lantern_ShootZombies_Night、TerrainCustomiserCN（含 Collector）需要适配；WhySoLaggy 另需更新写死 2.4.b 的测试路径。详见 [COMPATIBILITY_3.0.0_2026-10-23.md](COMPATIBILITY_3.0.0_2026-10-23.md)。本轮只审核并记录，插件源码/配置/版本/已安装 DLL/发行文件未改，未部署或启动游戏。

新审核报告包含构建矩阵、失效成员、反射/RPC 检查、测试结果和各项目实机边界；优先于下方旧版本兼容结论。

## 2026-10-12 PeakWorkshopCN 与扫描器已作废

按用户要求，PeakWorkshopCN 和 PeakWorkshopCNScanner 均已停止维护并集中到 `MOD开发/作废/`。源码、扫描证据、发行文件、8 个专属构建目录、散落审核文件及旧 memory 完整保留，1,518 个搬移或快照文件的大小与 SHA256 校验一致。

- [PeakWorkshopCN 作废说明](../MOD开发/作废/PeakWorkshopCN/作废说明.md)；当前 memory 入口：`mods/PeakWorkshopCN/README.md`。
- [PeakWorkshopCNScanner 作废说明](../MOD开发/作废/PeakWorkshopCNScanner/作废说明.md)；当前 memory 入口：`mods/PeakWorkshopCNScanner/README.md`。

指定 2.0.a profile 原本已无这两个插件的 DLL/PDB；两份残留配置也已移入归档。原 PeakWorkshop 保留原状。本次未构建、部署或启用任何插件。旧漏翻、复扫、性能验收、发行及自动同步 profile 的约定停止执行，仅在用户明确恢复后重新评估。

原六件套/current 与公共入口旧内容分别保存在各项目 `归档资料/memory/` 和中文补丁的 `归档资料/global-memory-before-2026-10-12/`；历史记录中的“当前”“下一步”不代表活动任务。归档不表示未完成的实机验收已通过。

## 其他近期事项

- 发行文案最新规则（2026-09-21）：使用英文，保留上一版格式，README 和 CHANGELOG 均明确列出本版更新；见 common/00、05。四 MOD 已纠正，仍无 ZIP。

- 四 MOD ModConfig 更新已构建部署：`mods/ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`（实现设计见同目录 INTEGRATION_PLAN_2026-09-21.md）。PlayersInfo 0.2.5、WhySoLaggy 1.0.5、WhereIsThing 0.1.2、WhereIsMyAmulet 1.0.4；23 项测试通过，最终 DLL 完整实机验收待完成；四个新版发行目录已备齐五件套，无 ZIP、未上传，历史目录未改。

- StateKeeper当前使用归因：`mods/StateKeeper/research/USE_RULES_2026-09-12.md`，analysisVersion7；使用事实与效果归因分离，按字段资源汇总。当前12局Runs回放含一局缺6块，原扩展分析待办见该MOD的TODO。

## 当前结构

- `README.md`：唯一入口和读取顺序。
- `CHANGELOG.md`：memory 变更履历（按时间倒序追加）。
- `TODO.md`：跨 MOD 待办和风险；各 MOD 专属待办在对应目录 `TODO.md`。
- `MEMORY_INDEX.md`：本文件，总览索引。
- `common/`：跨 MOD 通用规则（00-08 共 9 个主题）。
- `mods/`：每个 MOD 的独立入口（README + STATUS + TODO + RECENT + DECISIONS + FILES）和 `temp/current.md` 临时恢复摘要。

## 通用规则（common/）

- `common/00_用户偏好.md`：用户身份、沟通风格、接手基线。
- `common/01_协作与记忆规则.md`：四同步铁律、接手前检查、日期规则、每 3 次阶段性判断写入 MOD 临时 MD 的规则。
- `common/02_工程与构建规范.md`：OutputPath/HintPath 规约、L&SZ&N 作为重构参照。
- `common/03_日志与诊断规范.md`：日志分级、FieldProbe DSL、JSON 禁注释。
- `common/04_联机与同步规范.md`：主客机权限、RPC 校验、中途加入同步。
- `common/05_发布与版本规范.md`：README/CHANGELOG/manifest 三文件协同、135 字符限制。
- `common/06_UI与字体规范.md`：CJK 字体四级兜底、FontHelper 标准实现、描边与字号规范。
- `common/07_PEAK版本与反编译基线.md`：PEAK 2.1.a 相对 2.0.a 的业务级变化、MOD 兼容结论和待验证风险。
- `common/08_ModConfig本地化与安全集成规范.md`：ModConfig 中英文显示的 section/key 方案、禁止全局缓存重建、生命周期、迁移步骤和验证清单。

## MOD 四件套（mods/）

### PeakWorkshopCN（0.1.0 / 已作废）

- 当前入口：`mods/PeakWorkshopCN/README.md`、`STATUS.md`、`TODO.md`、`RECENT.md`、`DECISIONS.md`、`FILES.md`、`temp/current.md`；仅保留作废状态与归档指针，无活动待办。
- 归档：`MOD开发/作废/PeakWorkshopCN/`，源码、报告、发行文件、七个专属构建目录、原 memory 及 profile 配置已集中。详见 [作废说明](../MOD开发/作废/PeakWorkshopCN/作废说明.md) 与 `归档资料/归档清单-2026-10-12.json`。
- 不再构建、部署、发行或执行旧验收计划，先前自动同步 profile 约定失效。

### PeakWorkshopCNScanner（0.1.0 / 已作废）

- 当前入口：`mods/PeakWorkshopCNScanner/README.md`、`STATUS.md`、`TODO.md`、`RECENT.md`、`DECISIONS.md`、`FILES.md`、`temp/current.md`；无活动扫描或复核任务。
- 归档：`MOD开发/作废/PeakWorkshopCNScanner/`，637 条首扫及 359 条复扫原件、审核、源码、独立构建、原 memory 和配置保留。详见 [作废说明](../MOD开发/作废/PeakWorkshopCNScanner/作废说明.md) 与 `归档资料/归档清单-2026-10-12.json`。
- 不自动启用、恢复或部署。归档不代表未完成的实机、联机和性能验收通过。

### 临时思考记忆区

- 每个 MOD 目录下如有临时记录，当前摘要固定为 `temp/current.md`。
- 旧日期临时文件统一放在 `mods/<ModName>/temp/archive/`。
- 每形成 3 次明确的阶段性判断、排查结论或方案取舍，就追加一次摘要。
- 上下文压缩、会话中断或换 AI 后，先读该 MOD 最新临时 MD，再读正式四件套。
- 各 MOD 当前临时摘要统一为 `mods/<ModName>/temp/current.md`；历史日期文件位于对应 `temp/archive/`。

### ItemInfoCN（1.0.0 已发布）

- `mods/ItemInfoCN/README.md`：中文化 HUD 整体架构与功能轮廓。
- `mods/ItemInfoCN/RECENT.md`：1.0.0 发布态、配置项表、翻译覆盖面、已知边界。
- `mods/ItemInfoCN/DECISIONS.md`：架构决策、翻译策略、补丁边界、EasyBackpack 兼容。
- `mods/ItemInfoCN/FILES.md`：源码路径、项目文件、构建命令、关键文件（含 `Helpers/FontHelper.cs` CJK 字体四级兜底）。

### Lantern_ShootZombies_Night（0.2.1）

- `mods/Lantern_ShootZombies_Night/README.md`：灯笼/打僵尸/日夜/寒冷回暖整合入口。
- `mods/Lantern_ShootZombies_Night/RECENT.md`：近期修复，含客机备用池/灯燃料双扣、本地燃料权威和重复灯处理。
- `mods/Lantern_ShootZombies_Night/DECISIONS.md`：版本、灯笼同步、本地燃料权威、配置同步、兼容和禁止回退。
- `mods/Lantern_ShootZombies_Night/FILES.md`：源码路径、构建命令、关键 Helper / Patch 清单。

### PlayersInfo（0.2.5 发行文件已备齐 / PEAK 2.4.b）

- `mods/PlayersInfo/README.md`：队友 HUD 聚合概览与功能轮廓。
- `mods/PlayersInfo/RECENT.md`：含 0.2.4 试发行、死亡条排除、骸骨之书影响结论、0.2.3 三档异常图标、死亡/晕倒距离修复、零体力饥饿倒计时、统一刷新、日志审计和发行产物结论，以及此前 0.2.1 功能历史。
- `mods/PlayersInfo/DECISIONS.md`：只读展示不发 RPC、HUD 架构、图标所有权隔离、安全位置解析、原生 `maxStaminaBar` 倒计时、统一刷新和日志门控边界。
- `mods/PlayersInfo/FILES.md`：当前 `0.2.5` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/PlayersInfo/temp/2026-09-07.md`：本轮 0.2.4 试发行、骸骨之书影响判断和待实机验证入口。
- `mods/PlayersInfo/temp/2026-09-06.md`：此前 0.2.4 死亡条修复、0.2.3 实现、构建、日志审计和待实机验证入口。

### StateKeeper（0.1.0 / 开发中）

- `mods/StateKeeper/temp/2026-09-09.md`：最新英文按钮/重命名修复、实际资源证据、字体宽度和新版包哈希。
- `mods/StateKeeper/RELEASE_AUDIT_2026-09-08.md`：最新发布入口；0.1.0自审清理、64项测试、本地ZIP和DLL哈希、未上传与未实机验收边界。
- `mods/StateKeeper/REPORT_REBUILD_2026-09-08.md`：当前实现/验收入口；analysisVersion5复盘报告、62项测试与七局回放、部署结果及Unity待验收项，优先于旧报告。
- `mods/StateKeeper/temp/2026-09-08.md`：最新接续摘要，纠正死亡重复、六个进度点、127处时间回退与卡顿误判。
- `mods/StateKeeper/README.md`：项目定位、展示名称、采集内容、RunId 结束判定、存储结构和当前路径。
- `mods/StateKeeper/PLAN.md`：整体 JSON 合并、合并/分析双进度、双语面板、面板收藏和 PEAK-MAP 匿名提交的后续计划。
- `mods/StateKeeper/RECENT.md`：StateKeeper 改名、本机旧数据迁移、体力阈值和性能优化停点。
- `mods/StateKeeper/DECISIONS.md`：版本、GUID、采样频率、事件降噪、后台写盘和禁止回退项。
- `mods/StateKeeper/FILES.md`：源码、测试工程、构建命令、profile 输出和数据结构。
- `mods/StateKeeper/temp/2026-09-06.md`：本轮恢复入口，含旧数据分析结果和下一步实机验证。

### PEAK-MAP（网站集成调研）

- `mods/PEAK-MAP/README.md`：网站现状、StateKeeper 集成边界和当前未改源码状态。
- `mods/PEAK-MAP/PLAN.md`：网站独立 API、Supabase/R2 存储、适度脱敏、安全校验、关闭机制和实施顺序。
- `mods/PEAK-MAP/DECISIONS.md`：独立 API/表/R2、匿名化、隐私和安全决策。
- `mods/PEAK-MAP/FILES.md`：网站现有上传/R2/安全/UI 文件和计划新增文件。
- `mods/PEAK-MAP/RECENT.md`：2026-09-06 网站调研停点。

### ModConfigDiagnostics（0.1.0 / 诊断与修改建议）

- `mods/ModConfigDiagnostics/README.md`：项目定位、真实报告结论和接手入口。
- `mods/ModConfigDiagnostics/RECENT.md`：0.1.0 已完成能力、2.4.b 报告结果和未完成边界。
- `mods/ModConfigDiagnostics/DECISIONS.md`：只读诊断、活动对象证据和禁止误归因规则。
- `mods/ModConfigDiagnostics/FILES.md`：源码、发行包、profile 报告和反编译依据路径。
- `mods/ModConfigDiagnostics/PLAN.md`：公共 `SettingsCell` 根修复、五个 MOD迁移、诊断增强和验证顺序。
- `mods/ModConfigDiagnostics/temp/2026-09-09.md`：当前报告、根因候选和下一步摘要。

### DreamyAscent（永久暂停/归档）

- 2026-05-24 用户明确要求 `DreamyAscent永久暂停`；除非用户明确恢复，否则不要继续 DA 的功能开发、排查、构建、诊断或 TODO。

- `mods/DreamyAscent/README.md`：项目概览和接手入口。
- `mods/DreamyAscent/RECENT.md`：近期完成内容、当前验证结论和后期物品编辑需求初评。
- `mods/DreamyAscent/DECISIONS.md`：已确认的技术决策、禁止回退项和后期区域物品编辑边界。
- `mods/DreamyAscent/FILES.md`：关键路径、构建命令、诊断目录和关键源码文件。
- `mods/DreamyAscent/archive/MAP_GENERATION.md`：地图生成链路、需求拆分、分层实现路线和历史资料。
- `mods/DreamyAscent/archive/IMPLEMENTATION_MATRIX.md`：后期需求到实现矩阵（归档）。
- `mods/DreamyAscent/MAP_GENERATION_RESEARCH_NOTES.md`：按用户要求多轮通读资源的过程记录、原始依据和多角度实现分析。
- `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`：跨区段物品放置专门记忆，记录来源模板 + 目标子区模型、雨林棕榈放沙漠等例子、风险等级和资料缺口。
- `MOD开发/DreamyAscent/data/map-data/SAMPLE_AUDIT_2026-05-13.md`：项目内地图样本集中审计，记录官方自然样本、TerrainRandomiser 验证样本、完整性、变体覆盖和纯净性判断。
- `MOD开发/DreamyAscent/data/map-data/sample-index.json`：项目内地图样本机器可读索引，供模板快照、对象注册表和回归检查工具使用。
- `MOD开发/DreamyAscent/data/map-data/COLLECTION_V2.md`：Snapshot V2 重采规则，规定官方自然样本和 TerrainRandomiser 强制变体样本分目录采集，且必须包含 `GeneratedChildrenSnapshot.json`。
- `MOD开发/DreamyAscent/data/tools/build_map_data_artifacts.py`：从诊断样本生成模板快照、对象注册表输入和样本回归报告的离线工具。
- `MOD开发/DreamyAscent/data/map-data/generated/`：离线生成产物目录，当前包含 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`。

### WhySoLaggy（1.0.5 发行文件已备齐 / PEAK 2.4.b）

- `mods/WhySoLaggy/README.md`：性能和网络诊断项目入口与 mermaid 能力矩阵。
- `mods/WhySoLaggy/RECENT.md`：1.0.4 全量修复、自动测试、构建结果和联机测试配置。
- `mods/WhySoLaggy/DECISIONS.md`：远端入站阈值、Ownership 分类、有界队列、批量日志和卸载顺序等禁止回退项。
- `mods/WhySoLaggy/FILES.md`：当前 `1.0.5` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/WhySoLaggy/temp/2026-08-28.md`：本轮修复状态和双客户端实机验收入口。

### OldPC（0.0.1 开发中）

- `mods/OldPC/README.md`：本地旧电脑画面模式和望远镜清晰模式入口。
- `mods/OldPC/RECENT.md`：当前功能范围和未完成的构建/实机验收。
- `mods/OldPC/DECISIONS.md`：本地视觉边界、画质恢复和风险。
- `mods/OldPC/FILES.md`：源码、工程和构建命令。

### UprightCockroach（本地四臂 FBX 已转换接入，待实机验收）

- `mods/UprightCockroach/README.md`：直立蟑螂客户端视觉替换定位和接手入口。
- `mods/UprightCockroach/RECENT.md`：2026-10-14 模型初筛、截图反馈、触须 config 与正式模型导入待办。
- `MOD开发/UprightCockroach/MODEL_RESEARCH_2026-10-14.md`：历史候选初筛；用户后选 Cockroach alien，再提供不同的本地 FBX。
- `MOD开发/UprightCockroach/FBX_INSPECTION_2026-10-14.md`：本地四臂直立蟑螂 FBX 无骨架/蒙皮；用户确认去麦克风和草丛，转换资源、贴图和 Scout 绑定已完成。
- `MOD开发/UprightCockroach/ANTENNA_CONFIG.md`：48项触须参数、热重载、坐标语义和13项配置检查；已部署 profile，游戏内未验收。
- `mods/UprightCockroach/DECISIONS.md`：只做直立版本、纯客户端边界、骨骼与生命周期风险。
- `mods/UprightCockroach/FILES.md`：源码工程、构建命令、输出 DLL 和调研依据。
- `MOD开发/UprightCockroach/TEST_PLAN.md`：首次实机验收步骤和失败日志保留规则。
- `mods/UprightCockroach/TODO.md`：模型、2.6.b 核对、MVP 和实机验收待办。

### TerrainCustomiserCN（0.1.2 已发布，对应原版 0.3.2）

- `mods/TerrainCustomiserCN/README.md`：TerrainCustomiser 中文 UI 版入口、版本对应、玩家侧说明和接手要求。
- `mods/TerrainCustomiserCN/RECENT.md`：0.1.2 发布状态、原版 0.3.2 同步、`.json.old` 只读列表、Collector 状态和已知风险。
- `mods/TerrainCustomiserCN/DECISIONS.md`：中文 UI 边界、原版联机/存档兼容、版本对应、翻译策略、发布决策和禁止回退项。
- `mods/TerrainCustomiserCN/FILES.md`：源码路径、构建命令、输出目录、关键文件、保存路径和发布包内容。

### PeakMapBrowser（0.1.1，已打包）

- `mods/PeakMapBrowser/README.md`：地图库客户端概览、账号功能和接手入口。
- `mods/PeakMapBrowser/RECENT.md`：0.1.1 发布包、账号/个人地图/点赞同步、DPAPI session 安全、当前 IMGUI 页面状态和待实机验证。
- `mods/PeakMapBrowser/DECISIONS.md`：点赞身份规则、DPAPI session 安全边界、服务端退出接口、UI 暂缓决策和禁止回退项。
- `mods/PeakMapBrowser/FILES.md`：客户端/服务端路径、关键源码、API 文档和构建命令。
- `mods/PeakMapBrowser/temp/2026-07-30.md`：0.1.1 发布、安全改动和线上退出接口验证摘要。

### WhereIsThing（0.1.2 发行文件已备齐 / PEAK 2.4.b）

- 发行 README Notes 已声明玩家名仅供娱乐，晚加入可能缺此前放置者记录；部分目标仅显示物品名称和距离。依据及边界见该 MOD DECISIONS/RECENT。

- 2026-09-20最新：Canvas排序降为20000并部署普通0.1.2测试版，保留交互Raycaster；TFA共存待实机复测，见STATUS/RECENT/temp/current。

- `mods/WhereIsThing/README.md`：0.1.2 当前能力、2.4.b 测试状态、owner 边界和分类审计入口。
- `mods/WhereIsThing/RECENT.md`：2.4.b 放置来源索引、绳索/蘑菇 owner 修复、分类校正、正式测试 DLL 和未完成验收。
- `mods/WhereIsThing/DECISIONS.md`：互斥用途分类、ItemSpawnerEnhanced 证据边界、纯客户端 owner、系统排除和禁止回退项。
- `mods/WhereIsThing/FILES.md`：当前 `0.1.2` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/WhereIsThing/PLAN.md`：0.1.2 房主/客户端正反例、分类回归、生命周期和性能验收计划。
- `mods/WhereIsThing/temp/2026-09-04.md`：2.4.b 当前停点、测试 DLL hash 和下一步恢复摘要。
- `mods/WhereIsThing/temp/2026-09-06.md`：ItemSpawnerEnhanced 分类审计、51 项精确覆盖、正式 profile 构建和最新 DLL hash。
- `mods/WhereIsThing/temp/2026-08-14.md`、`temp/2026-08-16.md`：2.1.a 早期开发历史。

### WhereIsMyAmulet（1.0.4 发行文件已备齐 / 实机待验收）

- 最新1.0.4字号补丁：投影Z清零、TMP自动字号关闭；已编译部署，远近字形尺寸实测待确认，见STATUS/RECENT/temp/current。

- `mods/WhereIsMyAmulet/STATUS.md`、`TODO.md`、`temp/current.md`：2026-09-20最终对照30000失败、20000成功；固定20000，移除探针，普通1.0.4已部署。Manual mode禁用行为与排序故障分别判断。

- `mods/WhereIsMyAmulet/README.md`：护符定位 MOD 概览和接手入口。
- `mods/WhereIsMyAmulet/RECENT.md`：1.0.3 Scout Statue 映射修复、标签间距、构建和发布状态。
- `mods/WhereIsMyAmulet/DECISIONS.md`：标签生命周期、雕像映射、FakeItem 识别边界和 ModConfig 本地化规则。
- `mods/WhereIsMyAmulet/FILES.md`：当前 `1.0.4` 发行目录五件套（无 ZIP）、DLL/hash、源码和 profile 路径；旧版记录保留。
- `mods/WhereIsMyAmulet/temp/archive/`：历史1.0.2/1.0.3实现和发布记录。

## 当前重点

- `ModConfigDiagnostics` 当前已确认公共 `SettingsCell` 模板上的 `LocalizedText row=0` 是 `LOC: 0` 潜在来源；先按 `PLAN.md` 修公共克隆链和危险 `RefreshCache()`，再迁移五个 MOD的新版菜单类型。当前尚未实施代码修复。

- `WhereIsThing` 当前为 `0.1.2` / PEAK `2.4.b`；发行文件已按用户要求备齐，无 ZIP、未上传。仍需验收分类窗口、绳索、岩钉、蘑菇、owner 和系统目标排除。

- `StateKeeper` 当前为 `0.1.0` 开发阶段；展示名为 `STATE KEEPER` / `状态分析`。当前重点是长时间多人实机验证，并按 `PLAN.md` 规划整体 JSON 合并、合并/分析双进度、双语 ESC 面板、面板收藏和 PEAK-MAP 自愿匿名提交；具体分析算法和发行包暂缓。

- `Lantern_ShootZombies_Night` 当前重点是实机验证客机本地燃料权威：有备用池时只降备用池、不降灯燃料，且远端 fuel 下降不覆盖本地。
- `PlayersInfo` 当前为 `0.2.5` / PEAK `2.4.b`；发行文件已备齐，无 ZIP、未上传。仍需验收语言/下拉、死亡条、图标、晕倒距离、倒计时、观战及多人边界。
- `DreamyAscent` 已于 2026-05-24 永久暂停/归档。此前预览、模板库、Snapshot V2、官方生成链和 zero-output 恢复等资料仅作为历史记录，不作为当前重点推进。
- `TerrainCustomiserCN` 已发布 0.1.2，对应原版 0.3.2。当前重点是后续玩家反馈漏翻时补 `DisplayNameTranslator.cs`、重建 Release、更新发布包；若玩家反馈旧地图缺失，先核对新持久化目录和旧插件目录，不要自动迁移；任何功能改动前先读 `DECISIONS.md` 的联机/存档兼容禁止回退项。
- 其他 MOD 新增功能前先读对应 `RECENT.md` 和 `DECISIONS.md` 的"禁止回退"条款。

## 记忆维护规则

### 定位说明

`memory/` 是**给其他 AI 软件/会话快速上手用**的公开知识库。每次人类 session 之间切换都可能换 AI，所以结论必须完整落盘，不能只存在 BepInEx 内存记忆里。

### 写入流程（四同步铁律）

1. `update_memory`：内存记忆（本 AI session 可直接读）。
2. 改对应 MD 文件：让其他 AI 能读到。
3. 同步本索引 `MEMORY_INDEX.md`：新增/重命名/删除条目时必改。
4. 追加 `CHANGELOG.md`：按时间倒序记录。

### 临时思考记忆

- 具体 MOD 的阶段性思考结果写入 `mods/<ModName>/temp/YYYY-MM-DD.md`。
- 每 3 次明确阶段性判断追加一次摘要。
- 该文件用于压缩恢复，不替代正式 `RECENT.md` / `DECISIONS.md` / `TODO.md`。

### 读取流程（接手前检查）

1. `list_dir memory/`：看文件大小/修改时间。
2. `read_file CHANGELOG.md`：看最近两条时间戳条目。
3. 处理具体 MOD 时先读该 MOD `temp/` 下最新日期文件，尤其是压缩恢复后。
4. 有变更 → 按需读对应 MD；无变更 → 直接按内存记忆走。

### 日期铁律

- 首选 `Get-Date -Format "yyyy-MM-dd"`。
- 命令异常才问用户。
- 禁止凭系统时间戳或对话历史猜。

### 禁止

- 不要把某个 MOD 的待办写进 `common/`。
- 不再依赖已删除的历史文件；缺失信息必须重新从源码、日志或用户反馈确认。
- 不要在 JSON 配置里写注释；用 `_doc`（全局）/ `note`（单条）承载说明。
