# StateKeeper Files

## 2026-09-12 使用归因

- `StateKeeper/ItemUseRules.cs`：组级使用分类、动作计数与按原单位聚合。
- `StateKeeper/StateKeeperItemUseUi.cs`：使用/流转视图、筛选和证据入口。
- `research/USE_RULES_2026-09-12.md`：规则边界、真实录局统计、未完成的原计划范围。
- `.build/test-results/use-final.trx`：本轮完整测试结果；测试SDK已补齐。

## 当前发行包

- 2026-09-09包已追加英文按钮/重命名修复，最新哈希见 `temp/current.md`。
- `MOD开发/StateKeeper/tools/Audit-HistoryUi.py`：只读检查原菜单持久事件、Canvas层级与Daruma Drop One字形宽度，不是Unity运行时截图测试。
- `RELEASE_AUDIT_2026-09-08.md`：发布前发现/清理/保留理由、64项测试和哈希。
- `MOD开发/StateKeeper/发行/0.1.0/`：0.1.0首版发布包文件；旧草稿已按用户明确要求更新。
- `MOD开发/StateKeeper/发行/StateKeeper-0.1.0.zip`：经内容校验的ZIP。
- `MOD开发/StateKeeper/README.md`、`CHANGELOG.md` 及发行目录同名文件：已同步为英文版。
- `MOD开发/StateKeeper/tools/Build-Release.ps1`：可重复构建、生成图标、同步文档和打包校验，-Deploy更新profile。
- `StateKeeper.Tests/ReleaseAuditTests.cs`：禁用采集入口和恢复边界测试。

## 当前复盘实现

- `REPORT_REBUILD_2026-09-08.md`：analysisVersion5合同、七局回归、62项测试、部署和未验收边界。
- `StateKeeper/ReportModels.cs`、`LifecycleAnalysis.cs`、`ReportAnalysis.cs`：证据位置、生命周期、风险和报告聚合。
- `StateKeeper/ReportComparison.cs`：可靠Steam身份、对比资格/分母、轻量摘要和搜索。
- `StateKeeper/RecordingClock.cs`、`PerformanceWindow.cs`：单调时钟与实际工作耗时窗口。
- `StateKeeper/StateKeeperReportsUi.cs`、`StateKeeperComparisonUi.cs`、`StateKeeperAxes.cs`：六页复盘、搜索、弹窗、比较和独立刻度。
- `StateKeeper.Tests/ReportTests.cs`：22项报告专项测试；完整套件62项通过。
- `.build/test-results/statekeeper-report.trx`：最新测试报告；`.build/release/StateKeeper.dll`：与profile一致的独立Release产物。

## 归因实现历史

- `research/ATTRIBUTION_ENGINE.md`：算法合同、已支持机制、置信度与测试结果。
- `StateKeeper/ItemAttributionEngine.cs`：短窗口观察、候选竞争、受益者和数量/时空证据。
- `StateKeeper/RecordedItemRules.cs`：只读定义规则、嵌套Affliction、时延/到期/持续与烹饪。
- `StateKeeper.Tests/AttributionTests.cs`：23项归因专项测试。
- 当前analysisVersion为4；下方版本/测试数量为接续历史。

## 当前接续实现

- `RESEARCH_ASSISTANCE_2026-09-08.md`：互喂/救援可见范围、五条已有局线索、实现与未验证边界。
- `StateKeeper/AssistanceEvidence.cs`、`StateKeeper.Tests/AssistanceTests.cs`：实际状态减少与急救阈值及反例。
- `StateKeeper/StateKeeperDashboard.cs`、`StateKeeperScrollFocus.cs`：详情图表、状态条、物品证据和焦点滚动。
- `StateKeeper.Tests/RecordingRegressionTests.cs`：可选本地六局只读C#分析回放，不写原局/面板索引/游戏缓存。
- 当前测试14/14通过；下文5/5为早期记录，实机验收仍待完成。

## 2026-09-08 审计资料

- `memory/mods/StateKeeper/RESEARCH_2026-09-08.md`：当前采集语义、现有算法问题、最小字段合同、六局证据、可读 UI 与验证建议；优先于早期计划的完成度描述。
- `memory/mods/StateKeeper/research/audit-recordings.mjs`：只读原始局的 Node 离线审计，含辅助规则检查。
- `memory/mods/StateKeeper/research/audit-six-runs.json`：六局复核结果，连续观察间隔上限 1 秒。
- `memory/mods/StateKeeper/research/audit-gap-0.6.json`、`audit-gap-2.json`：间隔上限敏感性复核，非游戏分析缓存。

更新时间：2026-09-06

## 工程

- `MOD开发/StateKeeper/StateKeeper/StateKeeper.csproj`：主工程，版本 0.1.0，直接输出到 r2modman `2.0.a` profile。
- `MOD开发/StateKeeper/StateKeeper.Tests/StateKeeper.Tests.csproj`：MSTest 测试工程。
- `MOD开发/StateKeeper/README.md`：开发说明；发行目录当前仍是旧草稿，不同步。
- `MOD开发/StateKeeper/CHANGELOG.md`：0.1.0 开发变更。

## 关键源码

- `StateKeeperPlugin.cs`：BepInEx 生命周期、配置、Harmony 初始化；当前仍含旧 F8 收藏入口，后续面板接入时移除。
- `RunCollector.cs`：RunId 绑定、5Hz 采样、2Hz 库存、玩家身份、距离、状态和事件收集。
- `RunStore.cs`：活动恢复、GZip 分块、后台队列、原子写入、收藏和最近 10 局清理。
- `ItemDefinitionCatalog.cs`：一次性只读扫描 ItemDatabase prefab，生成定义摘要。
- `StateKeeperUi.cs`、`StateKeeperPage.cs`、`StateKeeperButton.cs`：暂停菜单入口、历史/收藏列表和详情占位页。
- `PauseMenuMainPagePatches.cs`：将 StateKeeper 页面接入原版暂停菜单。
- `StatsModels.cs`：RunRecord、分块、玩家、体力/状态、库存、事件和索引模型。
- `GamePatches.cs`：正式结束、胜利、死亡/倒地、物品生命周期和体力事件补丁。
- `memory/mods/StateKeeper/PLAN.md`：整体 JSON 合并、双进度、双语面板和匿名提交的后续计划；当前未实现。
- `research/ITEM_ANALYSIS.md`：BetterItemInfoDisplay/2.4.b 对照、五局物品字段覆盖、实际效果判定算法和面板数据边界。
- `Properties/AssemblyInfo.cs`：StateKeeper 程序集信息和 0.1.0.0 版本。
- `StateKeeper.Tests/RunStoreTests.cs`：8 人分块恢复、收藏和最近记录联动清理测试。

## 运行路径

- DLL：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/StateKeeper.dll`
- 配置：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/config/com.local.statekeeper.cfg`
- 数据根：`C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/StateKeeper/`
- 旧 DLL/PDB 迁移备份：`MOD开发/StateKeeper/迁移备份/`

## 数据结构

```text
StateKeeper/
  Runs/
    <run-id>.json
    <run-id>.chunk-xxxxx.json.gz
  Favorites/
  active-run.json
  index.json
```

当前局主 JSON 只保存 header、players、statusTypeOrder、chunks 和 activeChunk。高频 `samples`、`inventorySnapshots`、`events` 写入 `RunId.chunk-xxxxx.json.gz`。后续合并成功后新增 `RunId.data.json.gz`，解压后为一份完整 JSON；失败时保留碎块。

## 构建与测试

```powershell
dotnet build "MOD开发\StateKeeper\StateKeeper\StateKeeper.csproj" -warnaserror
dotnet test "MOD开发\StateKeeper\StateKeeper.Tests\StateKeeper.Tests.csproj" -warnaserror --no-restore
```

当前结果：构建 0 warnings / 0 errors，测试 5/5 通过；schema 3 与基础面板已接入，尚未进行实机 UI 和性能验证。
