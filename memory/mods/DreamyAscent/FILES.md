# DreamyAscent Files

更新时间：2026-05-17

## 路径

- 源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\DreamyAscent`
- 项目文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\DreamyAscent\DreamyAscent.csproj`
- 构建输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\DreamyAscent.dll`
- 诊断目录：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\terrain\BepInEx\plugins\DreamyAscent Diagnostics`
- 中文本地化主表：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\DreamyAscent Data\localization.zh-CN.json`
- `DreamyAscent Data` 下的外置 JSON 读取都基于插件目录拼接，不再依赖绝对路径硬编码。
- 旧 `TerrainCustomiserCN` 运行时文件夹只作为迁移来源，不能再作为新入口；源码里保留的旧目录字符串是迁移兼容，不是脏代码。
- 地图生成正式记忆：`C:\Users\Administrator\Desktop\MOD\PEAK\memory\mods\DreamyAscent\MAP_GENERATION.md`
- 地图生成需求实现矩阵：`C:\Users\Administrator\Desktop\MOD\PEAK\memory\mods\DreamyAscent\IMPLEMENTATION_MATRIX.md`
- 地图生成多轮阅读草稿：`C:\Users\Administrator\Desktop\MOD\PEAK\memory\mods\DreamyAscent\MAP_GENERATION_RESEARCH_NOTES.md`
- 跨区段放置专门记忆：`C:\Users\Administrator\Desktop\MOD\PEAK\memory\mods\DreamyAscent\CROSS_SEGMENT_PLACEMENT.md`
- 项目内样本数据目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data`
- Snapshot V2 采集规则：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\COLLECTION_V2.md`
- 官方自然 Snapshot V2 样本目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\1.62.a-snapshot-v2`
- TerrainRandomiser Snapshot V2 样本目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\TerrainRandomiser-snapshot-v2`
- 样本审计记录：旧审计 `C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\SAMPLE_AUDIT_2026-05-13.md` 只作历史参考；当前有效审计结论写在 `RECENT.md` 和 `sample-index.json`。
- 样本机器索引：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\sample-index.json`，2026-05-17 已更新为 Snapshot V2 真实计数：官方 21 份、TerrainRandomiser 6 份、总计 27 份、issues 0。
- 旧样本目录 `1.62.a/` 与 `TerrainRandomiser/` 已删除，不再作为新采集或构建输入。
- 样本产物生成脚本：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\tools\build_map_data_artifacts.py`
- 生成产物目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\generated`
- 模板快照产物：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\generated\template-snapshots.json`
- 对象注册表输入产物：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\generated\object-registry-input.json`
- 样本回归报告：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\data\map-data\generated\sample-regression-report.json`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\DreamyAscent\DreamyAscent\DreamyAscent.csproj" -c Release
```

## 关键源码

- 源码前缀统一使用 `Da*`，旧 `Tc*` 前缀不再作为新代码命名。
- `DaCustomiserWindow.cs`：F1 主窗口、光标状态、预览区域调用、参数编辑、Catalog/Placement 配置 UI；2026-05-14 已将结构面板改为三层联动：关卡横排、区域横排、当前区域生成物列表，2026-05-15 左下参数说明和焦点联动已收口稳定。
- `UI\DaCustomiserWindow.Layout.cs`：主相机预览布局拆分文件；包含顶部工具栏、导航窗口、右侧详情窗口、左侧参数说明/样本资产/放置配置摘要，以及多窗口输入屏蔽矩形传给预览。
- `DaCustomiserWindow.cs`：已接入第一版只读区段模板库 UI，显示当前 Segment 下 item/material、来源、默认参数和父子/联机同步标记。
- `DaSceneHighlighter.cs`：游戏内生成器范围高亮；当前使用水平四角短括号 + 小中心标记，避免 `area` 被画成立体框或放置子区。
- `DaMapPreview.cs`：地图预览相机、RenderTexture、预览交互、视角控制。
- `DaPreviewPoseService.cs`：预览位姿 JSON 读写，保存路径为插件目录下 `DreamyAscent PreviewPoses.json`；源码根部同名 JSON 已纳入构建复制，外部 JSON 优先，代码内置 fallback 在 `DaMapPreview.DefaultPreviewPoses`。
- `DaLocalization.cs`：UI/结构名称翻译；`TranslateOrOriginal` 用于 catalog 这类大量 prefab/material 名称，避免未翻译素材名刷日志；中文本地化现在外置优先，DLL 字典只保留 fallback；含区段模板库编号 prefab 的规则翻译。
- `DreamyAscent Data\localization.zh-CN.json`：当前中文本地化主表，后续新增中文优先补这里。
- `Plugin.cs`：插件启动入口，负责初始化本地化加载流程。
- `DaRuntimeEditService.cs`：运行时生成、清理、分组刷新。
- `DaRuntimeEditService.cs`：导入时会按 `SegmentName` 复制 `SubArea` / `PlacementRule` 配置；旧 JSON 无字段不清空，新 JSON 空数组可显式清空。
- `DaTerrainExportService.cs`：运行时扫描、segment root 解析、fallback 变体过滤；也负责生成前按当前场景重绑 `SourceSegment` / `SourceRoots` / `PropGrouper` / `LevelGenStep` / constraint 引用，避免导入或空白清理后生成入口拿到空引用。
- `DaDiagnostics.cs` 或同类诊断文件：导出扫描、映射、运行状态和异常信息。
- `Services\DaGeneratedChildrenSnapshotDiagnosticService.cs`：写 `GeneratedChildrenSnapshot.json`，记录官方已生成结果、loose objects、special objects、父子/业务关系候选、关键组件字段摘要、脏样本原因和 TerrainRandomiser 标记。2026-05-17 后新版 schema 为 3；写出已改为流式 JSON，避免大快照导出时先构造完整字符串造成内存峰值。
- `DaObjectReferenceDiagnosticService.cs`：诊断导出 Unity 对象引用地图，补足 `RuntimeExport.json` 无法保存 prefab/material 引用的问题。
- `Data\DaObjectCatalogData.cs`：对象库 JSON 数据结构，供后续区段模板库 UI、手动放置、材质规则和导入导出复用。
- `Data\DaObjectRegistryData.cs`：离线 `object-registry-input.json` 的只读数据结构，记录 registry ID、来源 path、风险标签、推荐候选和材质候选。
- `Services\DaObjectCatalogDiagnosticService.cs`：从运行时对象生成 `ObjectCatalog.json`，按区域整理 prefab/material 候选、来源 step、稳定 key、父子模板和默认生成参数。
- `Services\DaObjectCatalogService.cs`：当前运行时 catalog 缓存服务，供 UI 查询，避免 IMGUI 每帧重建对象库。
- `Services\DaObjectRegistryService.cs`：从插件目录 `DreamyAscent Data\object-registry-input.json` 读取离线对象注册表；缺文件时只警告，不影响运行时 catalog；读取已改为流式 JSON。
- `Services\DaTemplateSnapshotService.cs`：从插件目录 `DreamyAscent Data\template-snapshots.json` 读取离线模板快照；读取已改为流式 JSON，UI 默认不加载，需在样本资产/注册表面板手动加载。
- `Services\DaTemplateBaselineService.cs`：当前变体官方默认模板 baseline；按 Segment + `NormalizedVariantName` 匹配 snapshot，并提供 `HasCurrentVariantDefaultTemplate()` / `GetCurrentVariantDefaultTemplate()` 供官方生成、Hybrid 合并和 UI 验证使用。
- `Data\DaTerrainData.cs`：已内置 `DaSubAreaData`、`DaPlacementRuleData`、`DaVector3Data` 和放置相关 enum；当前只保存配置，不负责实际生成。
- `UI\DaCustomiserWindow.cs`：Catalog 页已能显示放置配置、添加默认子区、从首批推荐 registry 候选添加规则、编辑规则数量/缩放和删除规则。
- `DreamyAscent.csproj`：版本、引用、构建输出路径。

## 后期需求可能新增文件

- `Data\DaPlacementData.cs` 或继续扩展 `DaTerrainData.cs`：当前 `DaTerrainData.cs` 已保存第一版 `SubArea` / `PlacementRule`；后续若变复杂再拆出手动放置物、外部物体引用、绑定关系和材质覆盖。
- `Services\DaObjectCatalogService.cs`：正式对象库运行时服务；可基于当前诊断版 `DaObjectCatalogDiagnosticService` 继续抽取，支持缓存、查询、UI 过滤和外部资源注册。
- `Data\DaSubAreaData.cs`：如果从 `DaTerrainData.cs` 拆文件，承载 Segment 下山顶/山腰/洞口/Plateau/Wall 等子区的中心 XYZ、形状、ray、layer、约束和兼容模板。
- `Data\DaPlacementRuleData.cs`：如果从 `DaTerrainData.cs` 拆文件，承载自定义生成规则，含模板 ID、数量、缩放、旋转、概率、落地、ownership、父子关系和清理策略。
- `Data\DaTemplateSourceData.cs` 或并入 `DaObjectRegistry`：未来保存跨区段模板来源、默认参数、组件摘要和风险等级。
- `Services\DaObjectRegistryService.cs`：已存在只读加载骨架；后续升级为运行时模板查询、资源缺失检查和实际 prefab 定位服务。
- `Services\DaPlacementService.cs`：按区域实例化、落地、移动、缩放、删除手动放置物。
- `Services\DaCrossSegmentPlacementService.cs`：未来组合来源模板与目标 `SubArea`，生成 positions/rotations，写诊断并处理兼容警告。
- `Services\DaMaterialOverrideService.cs`：实例级材质/颜色覆盖、恢复和导入导出。
- `Services\DaNetworkSpawnService.cs`：未来主机权威同步自定义生成数据，记录模板 ID、子区 ID、positions、rotations、必要 PhotonView ID 和资源缺失降级。
- `UI\DaObjectPalettePanel.cs` 或拆分现有 `DaCustomiserWindow.cs`：区段模板库/放置子区、添加物品、数量/大小/材质编辑 UI。

## 查日志

用户说“日志已更新”时，优先检查诊断目录里的最新文件。结论同步到 `RECENT.md` 或根目录 `TODO.md`。


