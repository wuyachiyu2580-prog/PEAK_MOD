# PEAK MOD Memory

更新时间：2026-05-17

这是项目记忆的唯一入口。目标是让新的 AI 智能体在 1 到 3 分钟内知道：当前有哪些 MOD、近期做了什么、还有什么没做、哪些规则不能违反。

## 先读什么

1. `CHANGELOG.md`：**先看**。按时间倒序列出最近的结构/内容变更，判断要不要重读。
2. `MEMORY_INDEX.md`：总览当前结构、全部 MOD 四件套、通用规则清单。
3. `TODO.md`：永久待办和风险。
4. `common/00_用户偏好.md` + `common/01_协作与记忆规则.md`：四同步铁律、Get-Date 日期规则、说人话风格。
5. `mods/<ModName>/README.md`：当前 MOD 的专属上下文。
6. `mods/<ModName>/RECENT.md`：近期已经做过什么。
7. `mods/<ModName>/DECISIONS.md`：已经确认过的技术决策和禁止回退项。
8. `common/02-05`：只在需要通用规则时读取（工程/日志/联机/发布）。

## 接手前检查（必做）

- 先 `list_dir memory/`，按文件大小和修改时间判断是否有新增/变更。
- 翻一遍 `CHANGELOG.md` 顶部最近的两条时间戳条目。
- 有变更再按需 `read_file` 对应文件；没变更直接按内存记忆里的结论走即可。

## 目录职责

- `mods/`：每个 MOD 一个目录，四件套 `README.md` / `RECENT.md` / `DECISIONS.md` / `FILES.md`。不要把某个 MOD 的细节写进 `common/`。
- `common/`：跨 MOD 共享的规则和规范，只写可复用结论，不写某个 MOD 的流水账。
- `TODO.md`：永久待办。任何未完成、待验证、已知风险都必须同步到这里。
- `CHANGELOG.md`：只记录 memory 结构或重要内容变更，按时间倒序追加。

## MOD 开发项目

- `ItemInfoCN`：物品信息中文化（1.0.0 已发布）。入口：`mods/ItemInfoCN/README.md`。
- `Lantern_ShootZombies_Night`：灯笼、打僵尸、日夜和寒冷/回暖相关功能整合（0.2.1）。入口：`mods/Lantern_ShootZombies_Night/README.md`。
- `PlayersInfo`：队友状态、物品栏、灯笼状态等 HUD 信息（0.1.0）。入口：`mods/PlayersInfo/README.md`。
- `DreamyAscent`：地形定制中文化与功能修复（0.1.0 开发中）。入口：`mods/DreamyAscent/README.md`。
- `WhySoLaggy`：性能、RPC、Harmony 和异常行为观测（1.0.3）。入口：`mods/WhySoLaggy/README.md`。

## 写入规则（四同步铁律）

任何记忆相关变更，必须同时完成：

1. `update_memory`（内存记忆）。
2. 改对应 MD 文件。
3. 同步 `MEMORY_INDEX.md`（新增/重命名/删除条目时）。
4. 追加 `CHANGELOG.md`（格式：`- [新增/修改/删除/规则/索引] 文件名：一句话说清楚`）。

四者缺一不可，否则新接手的 AI 会读到不一致的结论。

## 日期铁律

- 首选 PowerShell 命令：`Get-Date -Format "yyyy-MM-dd"`，用返回值写入文档。
- 命令异常才问用户。
- 禁止凭系统时间戳或对话历史猜日期。

## 2026-05-10 收口状态

- `DreamyAscent` 今天主要推进后期物品编辑基础：对象引用诊断、`ObjectCatalog.json`、只读“区段模板库”UI、区段模板库翻译和生成器范围高亮。
- 当前区段模板库是 Segment 级模板/材质清单，不等于未来山顶、山腰、洞口这类 XYZ 放置子区。
- `InspectTcCn` 是一次性诊断工具，已确认不参与发布；`tmp/InspectTcCn` 可删除。
- 最新 DreamyAscent Release 构建成功，输出到 r2modman terrain profile，0 警告 0 错误。

## 2026-05-11 续作状态

- `DreamyAscent` 的 `CustomBlank` 第一版已在实机跑通并通过构建。当前语义是跳过官方生成器、清理该段已生成的官方子物，但保留基础机制对象，例如 Caldera 的 `River`、Volcano 的 `RisingLava/Lava`。
- 最新复测把 `Caldera_Segment` 的剩余候选进一步收敛到 `ash`、`Bubbles`、`River`、`Coll`，把 `Volcano_Segment` 收敛到 `Coll`、`Plane`；这轮残留已确认主要是机制或粒子，不是未清理的普通装饰。
- 本轮结论已同步到 `mods/DreamyAscent/RECENT.md`、`DECISIONS.md` 和根 `TODO.md`，后续重点转到地图里剩余漂浮物与后期区域物品编辑架构。

## 2026-05-12 地图生成研究补强

- `DreamyAscent` 已新增 `mods/DreamyAscent/CROSS_SEGMENT_PLACEMENT.md`，专门记录跨区段物品放置。核心模型是“来源模板 + 目标子区”：来源模板提供 prefab/默认参数/组件/风险，目标 `SubArea` 提供 XYZ、范围、ray、layer 和落地约束。
- 后续不要再只说“模板库可混合”。第一条建议验证例子是把 Jungle `Jungle_PalmTree_*` 低风险模板放到 Desert 平台/山腰子区；Roots `Redwood`、行李、藤蔓、虫类、岩浆机制等因父子/Photon/机制风险先只读标记。
- `DreamyAscent` 已新增 `mods/DreamyAscent/IMPLEMENTATION_MATRIX.md`，把官方模板、空白自定义、当前区段自选、跨区段、父子依赖、外部 Unity 物品、材质/颜色、UI 拆窗、内置模板快照和多人同步逐项落到实现路径、依据、例子和资料缺口。
- `MAP_GENERATION.md` 和 `MAP_GENERATION_RESEARCH_NOTES.md` 已补强为多轮、多角度研究记录；下一步开发先做稳定 path、`DaObjectRegistry`、`SubArea` 和低风险 `PlacementRule`。

## 2026-05-13 改名状态收口

- 已收口 DreamyAscent 改名后的 Git 索引脏状态：旧 `TerrainCustomiserCN` 删除和新 `DreamyAscent` 未跟踪已整理为 staged rename/add，避免后续误恢复旧目录或误删新目录。
- 当前仍有两个无关未处理脏项：`.gitignore` 修改、`MOD开发/PlayersInfo/合并输出.txt` 删除。它们不属于 DreamyAscent/memory 改名冲突，后续处理前需确认来源。
- 已检查 PEAK 1.62.a 反编译更新：核心反编译源码与 1.61.b 哈希一致，五个 MOD 均 Release 构建通过，暂不需要代码更新；后续若实机出现新日志异常，再按具体栈定位。

## 2026-05-13 DreamyAscent 样本审计收口

- `MOD开发\DreamyAscent\data\map-data` 已完成集中审计：官方自然样本 22 个 JSON、19 个完整诊断目录；TerrainRandomiser 验证样本 29 个 JSON、7 个完整诊断目录。
- 批量复核结果：26 个诊断目录均包含 `RuntimeExport.json`、`NameMap.json`、`ObjectCatalog.json`、`ObjectReferenceMap.json`，`0 grouper` segment 为 0，未知 variant 为 0。
- 当前 `Beach / Jungle / Roots / Snow / Desert` 的全部已知变体均已覆盖；`Caldera / Volcano` 按 `DirectSegmentRoot` 处理。下一步不是继续刷图，而是基于 `sample-index.json`、`RuntimeExport`、`ObjectCatalog` 和 `ObjectReferenceMap` 提取模板快照、对象注册表和回归检查。
- 已新增 `data/tools/build_map_data_artifacts.py` 和 `data/map-data/generated/`：当前生成 `template-snapshots.json`、`object-registry-input.json`、`sample-regression-report.json`，回归为 `pass`。下一步是把离线产物接入源码侧 `DaObjectRegistry` 和稳定 path 匹配。

## 2026-05-14 DreamyAscent UI 重构停点

- DA UI 当前处于重构测试阶段。结构面板已从旧树形缩进改为三层联动：关卡横排、区域/生成组横排、当前区域生成物/步骤列表。
- 生成物行显示 step 类型和运行时 catalog 匹配到的 item/material 数量，用于先判断每个区域扫到了哪些生成物；这仍是 UI/诊断联动，不是实际自定义生成或重叠检测。
- 明天优先实机测试三层联动是否符合截图预期，并复测 `SubArea` / `PlacementRule` 导入回显 `placementConfigs=1`。两项通过前不要直接进入 Instantiate 生成。

## 2026-05-17 DreamyAscent Snapshot V2

- `DreamyAscent` 已新增 `GeneratedChildrenSnapshot.json` 诊断，用于记录官方已生成结果、loose/special objects、脏样本原因和 TerrainRandomiser 来源标记。
- 旧 `data/map-data/1.62.a/` 与 `TerrainRandomiser/` 样本目录已删除；新采集只放 `1.62.a-snapshot-v2/` 和 `TerrainRandomiser-snapshot-v2/`。
- 下一步必须先跑一份示范样本验字段，不能直接全量重跑；示范通过后再采官方自然样本和 TR 补变体样本。


