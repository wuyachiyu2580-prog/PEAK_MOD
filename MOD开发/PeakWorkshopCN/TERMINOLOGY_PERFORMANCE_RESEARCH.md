# PeakWorkshopCN 术语一致性与性能调研

日期：2026-10-05。目标：PeakWorkshopCN 0.1.0、PeakWorkshop 0.2.0、PEAK 2.6.a。接续 [AUDIT_RECHECK.md](AUDIT_RECHECK.md)；本轮只做研究与离线取证，未修改插件源码、翻译数据、DLL、配置或地图，未测游戏帧率。

> 状态更新（2026-10-06）：以上是研究时点的事实边界。统一修复已在后续候选中实施，包含术语、全量文案、资源显示/搜索和反射路径；候选已通过 0/0 构建与 45 项离线测试，但尚未部署或完成 Unity 实机性能采样。研究中的“Swamp→GLOOM 待确认”、旧 DLL 路径和“只研究未改码”只适用于本报告生成时点；当前候选依据 2.6.a 进度点提取证据确认该对应关系。

## 结论

1. 术语必须绑定实际游戏概念和本地化键，不能只按英文单词翻译。已从当前游戏安装包提取官方简体中文表，确认“雨林、森蕈、雪山、方山、火山、熔炉、雾沼、城塞、顶峰、天底”。`SHORE` 实际是“海岸”，用户看到提取结果后已明确选择跟随官方“海岸”。“海滨”“根须”“根林”等不能用作这些区域的显示名。
2. 中文补丁确有重复反射和重复字体赋值；最优先处理字体路径、字段 getter 和 MarkerRef 缓存。程序集扫描主要发生在启动，不能据此直接断言持续掉帧。
3. 优化与补漏需要共同设计：建立官方术语/资源显示表，初始化绑定精确访问器，稳定界面只查表和命中缓存；在对象创建、样式重建、语言/开关/场景变化时刷新。单纯缓存 `FieldInfo` 或简单“每帧执行一次”不足以解决问题。

## 官方术语证据

只读来源：`C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/data.unity3d`，UnityFS，1,504,358,738 字节，SHA256 `42460E26ACE228498A00F987322CF0D5E55610AB8EE5A7770BA4279C5A5F135B`。

`resources.assets` 的 TextAsset `SerializedTermsData`（path_id=1422）包含 866 个键。原始 JSON 449,420 字节，SHA256 `13A7A854A997E6445B0B0C2D702748ED65E5F8EDCC3E6CC317BFA1D860F0D342`。游戏 `LocalizedText.cs` 定义英文索引0、简体中文索引9；`LoadMainTable` 从 `Resources.Load("Localization/SerializedTermsData")` 读取，`MountainProgressHandler.ProgressPoint.localizedTitle:25` 用 title 调用 `LocalizedText.GetText`。

| 游戏本地化键 | 实际英文 | 官方简体中文 | 对 MOD 的处理 |
|---|---|---|---|
| SHORE | SHORE | 海岸 | Shore 区域采用海岸；用户已确认。Beach 作为该区域/区段别名时跟随，普通“海滩”名词不强改 |
| TROPICS | TROPICS | 雨林 | 修当前“热带”；Jungle 资源前缀需核实来源后关联 |
| ROOTS | ROOTS | 森蕈 | 修当前“根林”；作为普通树根/根节点的 roots 不套区域名 |
| ALPINE | ALPINE | 雪山 | 修当前“高山”；Snow 作为区域别名时关联 |
| MESA | MESA | 方山 | 修当前“荒漠”；Desert 资源前缀需按来源关联，不能全局替换沙漠名词 |
| CALDERA | CALDERA | 火山 | MOD 第4区段 Volcano 对应此区；不是熔炉 |
| THE KILN | THE KILN | 熔炉 | MOD Kiln / TheKiln 为后续区段 |
| GLOOM | GLOOM | 雾沼 | 游戏官方值已证实；MOD Swamp 候选对应，仍需场景进度点/实际区段核对 |
| THE CITADEL | THE CITADEL | 城塞 | MOD Citadel 的区域显示，避免自拟堡垒/城堡 |
| PEAK | PEAK | 顶峰 | 仅游戏终点区域名；一般 summit/山顶说明按句意处理 |
| AREA_VOID | NADIR | 天底 | 当前 Nadir=虚空需修；必须使用 AREA_VOID，表中没有 NADIR 键 |

用户最初提醒“海滩、雨林、森蕈、雪山、方山、熔炉等”；本次取证后对 SHORE 的明确答复为“按游戏表用‘海岸’”。这次确认优先于此前“海滩”的暂定记录。

证据明细：[official-area-terms.csv](research/2026-10-05-terms-performance/official-area-terms.csv)、[localization-evidence.json](research/2026-10-05-terms-performance/localization-evidence.json)。原始完整表只保存在 `.build/peakworkshopcn-research/SerializedTermsData.json`，不嵌入生产插件。

### 别名与显示边界

- `ScatterPresets.cs:605–627` 的 `BiomeLabel → CtBiome`：英文模式直接返回内部 biome，德文分支只有7项。这条路径不经固定 Tr 词条；只改 TranslationCatalog 的 Mesa 不会覆盖它。应在明确的区域显示入口关联官方键，保留 `Loc.En/Code/Setting`。
- `WorkshopPanel.cs:29276` 的 `BiomeBlurb` 明确 Volcano 为 Caldera、后接 Kiln；`BiomePlan.cs:35` 的 `PartnerOf` 配对 Volcano↔Kiln、Swamp↔Citadel。禁止把 Volcano 和 Kiln 合并成“熔炉”。`PartnerOf` 同时用于数据逻辑，不能直接把其返回值改成中文；应在显示使用处调用显示名函数。
- `WorkshopPanel.cs:29946` 拼接“区域 → PartnerOf(c)”时后半段仍是内部字符串，即使修了 BiomeLabel 也会漏翻。`Stages:28935`、`BiomePlan.RowLabel:88`、`SegmentFocus.LabelCore:60` 还有独立标签/编号/括号区段名需要接入。
- `SegmentFocus.LabelCore` 读取 `MapHandler.segments[index].biome`，第5槽另映射 Citadel/Kiln；用户填写的 `Base.SegmentNames` 优先返回，必须保留作者名称。槽位名/枚举名/实际群系不能用同一个全局字典无条件替换。
- `Nadir` 在 `WorkshopPanel.cs:28811,29410` 和 `EditorUi.cs:9180` 为直接显示字符串，除修错译还需补入口；技术性“落入虚空”报错仍是普通描述，不改成“落入天底”。
- 官方表未找到 BEACH/JUNGLE/SNOW/DESERT/VOLCANO/SWAMP/NADIR 同名键。没有同名键不等于没有官方译名，必须先找真实键/区段关系。游戏 Biome 枚举还包含其他名称，不能因其存在就认定当前有可见区域。
- 本次按现有类型树尝试读取资产内 `progressPoints`，匹配0个组件；这不证明游戏没有相应组件，也不构成 Swamp→GLOOM 的资产级确认。该别名保留明确验证项，不能把推断升级为完整实机证据。

### 当前需要修正和复核的范围

当前 `TranslationCatalog.cs:148–149` 把 Tropics/Roots/Alpine/Mesa/Nadir 分别译为热带/根林/高山/荒漠/虚空，应修相应游戏概念。Shore=海岸、Volcano=火山与本次证据相符。Beach、Swamp 要按实际区域别名语境处理。

`ConfigurationLocalization.cs:86–89` 的 Generation/Slot1–4 也必须同步：Slot2“热带/根林”、Slot3“高山/荒漠”明确冲突；Slot1“海岸”正确；Slot4 的 Swamp 待别名确认后统一，值键仍是原英文。还需复核完整长句、阶段标签、散布预设提示、统计、搜索提示和图标说明，不能只改孤立词条。

额外比较官方表英文与现有词表，得到42个可匹配键，其中22个中文不同。这是语境审核候选，不是22个确定错误，更不是全量覆盖统计。详见 [official-catalog-candidates.csv](research/2026-10-05-terms-performance/official-catalog-candidates.csv)。

- `CAMPFIRE=营火`：原 MOD `EditorUi.cs:25281` 和 `EditorTools.cs:3428` 的 Campfire 指游戏营火；当前“篝火”应随游戏统一，相关复数/分类/长描述一起核对。
- `BLOCK=屏蔽` 对工坊构件 Block、`LIGHT=点燃` 对光源 Light、`PLAY=演奏` 对游玩 Play、`WAVE=看这边` 对敌人波次 Wave 都是同词异义，禁止自动覆盖。
- NAME、START/START2、DEAD、HOLD、CLIMB 等须审核具体对象/动作。菜单通用词的合法表述差异也不能全部称为游戏术语冲突。

### 后续实现方式

建立“已确认概念 → 官方键”白名单，而非把官方整表按英文转成全局替换表。使用公开的 `LocalizedText.GetText(key, LocalizedText.Language.SimplifiedChinese)` 读取并缓存，不需要反射或更改全局当前语言。此重载缺键返回空字符串，且没有另一重载的完整平台/fallback/自定义值处理；只对审核过的静态名词使用，空值回退本次核实的中文并记录一次诊断。不要把 API 读取放在每个 Label 绘制中。

中文开关、游戏语言事件、场景和目录重建决定缓存代次；仅在表尚未就绪或确有重载时重新解析。内置句子的译文使用同一术语来源，动态参数保持原样。关闭中文应回退原MOD显示，不改原语言/数字格式。

构件/材质继续遵守上一轮全量审核范围：显示名独立于资源键。`Desert_Rock_1` 只有确认来源于 Mesa 资源后才可采用“方山岩石 1”这类显示名；`M_Rock` 可提供“岩石材质”并保留原名识别。数字、变体、来源、同名材质区分要保留。建立有限目录缓存与原名/中文搜索索引，目录新增时增量处理，不在绘制/每次按键时反射、拆词或重建全库；覆盖列表、网格、场景树、属性、拖拽、散布、提示和当前材质摘要。未知项保留原名并进入待审清单，不能冒充全部已翻。

## 性能证据与优先级

以下是源码和原 DLL 静态证据。IMGUI 一帧可能多次 OnGUI；表中“每次”指 hook 调用，不是固定每帧次数。字段数是遍历规模，不等于同数量 GetValue/SetValue，也不能换算成毫秒/FPS。

### P1：字体路径的重复工作

`FontPatches.StyleFieldsPostfix:90` 已缓存 Type→FieldInfo[]，但中文开启且字体可用时，每次调用仍枚举全部声明字段，筛选类型，读取样式、建立 Tuple、写字体。原程序集 Cecil 实测：

| 目标类型 / hook | 声明字段数 | hook 实际相关字段 | Font / GUIStyle / GUISkin | 触发依据 |
|---|---:|---:|---|---|
| EditorUi.Styles | 314 | 59 | 2 / 56 / 1 | EditorUi.OnGUI:1619；原 Styles 的 `_stylesReady` 不阻止 postfix |
| WorkshopPanel.FindFonts | 296 | 4 | 3 / 1 / 0 | WorkshopPanel.OnGUI:5386；原 `_fontsSearched` 不阻止 postfix |
| TeamUi.FindFonts | 6 | 2 | 2 / 0 / 0 | TeamUi.Style:474 每个样式请求先调用，随后才查样式缓存 |
| LaunchPanel.Styles | 48 | 10 | 0 / 10 / 0 | OnGUI:464/474 的显示路径 |
| ForgeHud.OnGUI | 9 | 2 | 0 / 2 / 0 | 每次 OnGUI 结束后，即使原方法提前返回 |
| NpcHud.OnGUI | 5 | 0 | 0 / 0 / 0 | 该字段 hook 当前没有可处理字体字段；实际样式来自 TeamUi |

详见 [font-field-counts.csv](research/2026-10-05-terms-performance/font-field-counts.csv) 与 [font-field-details.csv](research/2026-10-05-terms-performance/font-field-details.csv)。这些数量针对声明字段，不含嵌套类型、继承字段或样式字典中的条目；GUISkin 又会枚举其内置/自定义样式。

建议：

1. 启动预筛选精确目标，绑定字段访问委托；去掉 NpcHud 无作用的字段遍历 hook，保留它依赖的 TeamUi 字体路径。
2. 样式/字体/skin 真正创建或重建时注册并应用，记录对象、原字体和代次；稳定调用快退。样式字体已是目标引用时不重复 setter。
3. EditorUi 除主 Styles 还有 HpStyles、PaHeadStyle/PaTileStyle 等懒创建样式。仅给 Styles 加“一次完成”标记会漏新对象；应在这些实际创建点注册，或生成精确 setter/返回值 hook。不能靠所有创建都只发生在启动的假设。
4. `StylePostfix:130` 每次使用 `object[] __args`，存在参数数组/值类型装箱成本；按具体方法和参数位置绑定，避免通用参数数组及枚举 ToString。样式原字体仍需按 Title/Hand/Body 正确恢复。
5. `StyleFonts/FieldFonts` 当前长期持有样式/owner，直到 Dispose 清理；场景卸载/对象销毁时清理失效对象与旧代次，避免重开窗口持续累积。恢复前确认对象和绑定仍有效，不能用陈旧原字体覆盖新场景资源。
6. 普通字体 `ChineseFont` 已缓存成功结果，失败重试有300帧间隔，应保留，不能误称每帧扫描系统字体。TMP `ChineseTmpFont:145` 缺字体时没有独立重试节流，会重复查场景/测字形；增加失败节流与场景重试，找到后缓存。字体发现/字形检查只在 Unity 主线程执行。
7. `TmpFontPostfix:173` 反复 SetValue 可改为绑定 setter 并仅变化时写，但 `LoadOverlay.FontFor` 当前是创建加载 UI 时调用（LoadOverlay.cs:1501），不是已证实逐帧热点，优先级低于 IMGUI。`Restore/RefreshTracked` 属语言/场景事件路径，可保留必要恢复操作。

### P1：字段文字与 MarkerRef

- `WorkshopPatches.FieldPostfix:112` 每次 getter 调用都 GetType/GetField/GetValue，并分配前缀数组；同时不区分 Label/Hint/Short/Desc，已有错译。应按具体 getter 绑定精确字段和原 fallback 规则，优先对不可变内置预设预计算；可变对象按字段值/修订失效。可使用启动创建的访问委托或 Harmony 明确字段注入，禁止热路径猜字段名。
- `InvalidateMarkerLabel:128` 每次读取都反射写 `_cmLabelEn`，还通过 `TypeByName` 找类型，并把 `Loc.En` 属性误作字段。没有中文开关快退。这既不正确也破坏原缓存收益。
- MarkerRef 改为开关/语言/标签数据变更时失效，或以“实例 + 代次 + 内容修订”缓存；首次访问刷新一次。`Loc.En` getter 如需使用，启动绑定委托。不要为了强制刷新每次翻转 `_cmLabelEn`，也不能只靠 Loc.Bump 假定所有缓存更新。

### P2：配置与动态匹配

- `ConfigurationLocalization.DisplayNamePostfix:56` 每次扫描所有接口、找 ConfigBase 属性、反射取值，读到 entry 后才判断是否本MOD。按具体 wrapper 类型缓存访问器，按 ConfigEntryBase 缓存目标身份与显示名；其他MOD尽快回退。若 wrapper 可重新绑定 entry，不能永久把“某wrapper实例→entry”当不变关系。先修正确声明方法解析，再谈缓存，避免缓存原来的错误目标。
- `DescriptionPostfix` 已按目标 ConfigDescription 实例查字典，保持；不全局改其他MOD配置，也不改原cfg值。
- `CatalogData.TryPair:35` 是按 source 分桶后线性尝试，不是每次遍历全部1,000规则。当前最大桶24条，`@fields`和各方法的实际分布见 [rule-buckets.csv](research/2026-10-05-terms-performance/rule-buckets.csv)。扩充词表时仍需把907固定对转为(source,de,en)字典，93动态按真实调用点/前缀进一步分组，保留德英参数一致性。
- Regex 对象已有缓存，但 Match、捕获和最终输出仍有成本；手写规则还重复 GetGroupNames，组名可启动计算。10ms是每个正则的超时，不是一条文本或整帧的总预算。固定模板优先生成有边界的匹配器，必要正则应限制候选数及输入长度；保持失败原文回退。
- 上轮确认的21个回调来源、6个Same为空模板、参数双大括号损坏需先修正确性。缓存键不得只用最终英文，否则不同来源/作者内容可能误命中。避免无限缓存时间/计数/作者动态字符串；只缓存有限内置表或有界结果。

### P3：启动与低频反射

- `WorkshopPatches.Install:20` 的 GetTypes/GetMethods/GetConstructors/ReadMethodBody 和配置程序集扫描只在 Awake 安装期间运行。可离线生成精确类型/签名/调用位置清单并绑定原DLL指纹；启动验证再安装，避免全程序集扫描和无关泛型噪音。新版本不匹配时明确诊断并回退，不靠方法名猜测套用。
- `TranslateCalls` 中的 AccessTools.Method 在 Harmony 生成替换方法时执行，不在每条文本显示时执行。`_original` 已用 Delegate.CreateDelegate；固定 TranslateLiteral 热路径没有反射，不应为了“消灭反射”重写它。
- `NotifyLanguageChange` 的 `_bump.Invoke` 属事件，改为 Action 委托容易但收益优先级低。嵌入 JSON 资源枚举/反序列化属于静态初始化，不能当作每帧成本。

## 推荐实施顺序与验收

1. 固化本报告术语白名单和语境，补 Swamp→GLOOM 场景/区段证据；在所有区域显示入口接同一映射，统一配置/长句和内置资源名用词。先保全作者参数、资源键、数字格式与原语言回退。
2. 修 FieldPostfix 精确字段、MarkerRef 代次缓存；修回调/边界/泛型等已确认缺陷，再扩展缺项。这样中文显示与缓存正确性共同验证。
3. 改字体为创建/重建注册与代次刷新，过滤字段、缓存访问器、清理无效对象；设置TMP失败节流。验证懒创建样式、同一帧新窗口、中文开关和跨场景恢复。
4. 接完整构件/材质目录、中文/原名搜索索引，按来源区分同名；优化配置访问与固定/动态规则索引。继续上一轮全MOD缺项清单，不能止于图片或六个区域名。
5. 最后收紧启动补丁清单、去重安装计数、保留异常原始栈。安装日志分别报告“成功目标/失败目标/规则数”，不把计数称为覆盖率。

结构验收目标：界面稳定、缓存已就绪且数据未变时，本补丁高频路径无类型/字段/接口发现、无全字段扫描、无重复字体赋值；固定文字和稳定内置标签新增分配为0是目标，需实机确认。动态输出字符串、首次字形补入、窗口创建和事件恢复可有必要分配。未启用中文时高频补丁快退，MarkerRef也适用。

实机比较同一profile、同一地图/种子、相同界面和帧率上限，区分首次初始化与热身后的稳定阶段：

- 中文关闭、当前补丁开启、优化后开启三组；覆盖机场工坊列表/详情、编辑器构件网格和列表、材质选择/搜索、散布长说明、多人/队伍/NPC HUD、配置页及加载UI。
- 每组热身后固定时段重复采样，记录每帧 OnGUI/hook 次数、插件累计与p50/p95耗时、GC字节、反射查找/字段扫描/字体写入/目录重建次数；启动安装耗时单独比较。说明采样是否包含 profiler 自身开销，不用一次FPS截图归因。
- 反复切换中文/原语言、窗口开关、场景、分辨率/缩放，验证新建与懒创建样式；缺中文字体/对象销毁后重建验证节流、恢复和清理。
- 资源目录检查所有群系/变体增量，搜索中文与原名都返回同一原始键，保存重载仍能定位；作者地图名/介绍/标签与含双大括号等参数逐字不变。

本轮没有运行这些实机采样，也没有声称已经改善FPS。现有41项离线测试不覆盖字体/WorkshopPatches/初始化，后续必须补真正验证这些行为的检查。

## 本轮产物与完整性

- `tools/Research/extract_terms.py`：UnityPy 1.25.4 只读提取本地化资源；依赖安装在 `.build/peakworkshopcn-research/deps`，未加入生产项目或游戏目录。
- `tools/Research/Research.csproj`、`Program.cs`：独立 net10 Cecil 调研工具，只读原DLL，输出字体字段和术语候选/规则桶统计；已成功运行。不是 Unity 基准，也没有构建/部署生产插件。
- `research/2026-10-05-terms-performance/`：官方术语、资源指纹、字段数量/明细、候选差异、规则桶、源码完整性及空的进度点提取结果。源码12项哈希与前轮一致，见 [source-integrity.csv](research/2026-10-05-terms-performance/source-integrity.csv)。
- profile CN DLL 仍为304,128字节，SHA256 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`；原DLL仍为 `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`。本次只生成研究工具/文档/明细和更新本地memory。
