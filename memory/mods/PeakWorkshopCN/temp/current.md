# PeakWorkshopCN 当前摘要

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

## 最新恢复入口：2026-10-07 14:50 扫描器已部署

用户改方向要求做小扫描MOD标注漏译。本轮新增独立PeakWorkshopCNScanner 0.1.0，50项离线检查通过，DLL1847E5EA…EE76已部署；原CN DLL仍2FC50B1F…AA523未改。当前原MOD再次只有.dll.old（禁用），未替用户启用，不要沿用下方历史“已恢复”判断当前状态。下一步管理器启用原MOD，打开页面采集HTML/JSON，按实际证据修复。扫描器完整恢复入口在兄弟目录`PeakWorkshopCNScanner/temp/current.md`；Unity/性能仍待验证。以下为此前开发历史。

日期：2026-10-07。版本 `0.1.0`。阶段：资源显示修订版已构建并部署，等待重启后的 Unity 实机复核。

当前：`PeakWorkshopCN.dll` 2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`；51 项测试通过。备份：`.build/peakworkshopcn-unified/backup/PeakWorkshopCN-2FC50B1FDBB6.dll`。原 PeakWorkshop.dll 已从 `.old` 备份恢复，原哈希仍为 `CC1ECDE5…8ADC`。

本轮新修复：构件 `Basalt Cluster`、`CliffPillar`、`LavaPillar` 等和材质 `BlueRock`、`DesertRock`、`Rock Volcano` 等在显示边界中文化；`RockLook.Label`、列表、网格、属性和中文/原名搜索共用显示转换。保存键、作者名称和原 DLL 不变。

下一步：重启同一 profile，确认日志出现原工坊与中文补丁两条加载记录，检查工坊首页、构件网格、材质下拉、搜索和卡死日志；随后继续完整页面、联机与性能验收。

## 统一计划实施中（2026-10-05 21:56开始，尚未部署）

- 用户已明确IMPLEMENT统一计划：全量漏翻、术语、资源中文/搜索与反射优化一起实施；保持0.1.0。不要停在研究或误把旧部署当新完成。
- 已备份现有DLL至 `.build/peakworkshopcn-unified/backup/PeakWorkshopCN-26D877.dll`，源码zip同目录。开发中候选输出 `.build/peakworkshopcn-unified/candidate/`，指定profile的旧DLL未覆盖。
- 已改CatalogData固定对索引/动态分桶、只解码模板常量后插入参数，SourceAliases支持真实回调；LiteralRewriter移动入口标签/开始边界并保留结束边界；Home按键上下文修订。
- 新BoundAccess提供启动编译字段访问器；WorkshopPatches改为原DLLhash绑定的token清单，精确getter（ScatterPreset改由原getter内Tr转译），MarkerRef弱引用代次缓存，闭合Strip4种类型，计数按方法去重。尚需真实安装/回归验证，不能宣称成功。
- 字体已重写为限定原MOD方法中的GUIStyle构造/set_font/get_font转译（缓存比较读取原字体，实际样式中文），弱引用跟踪恢复，事件处理已有样式，TMP失败300帧节流。取消旧全字段绘制扫描。需要测试Unity注册、继承样式/字体恢复和初始化顺序。
- 配置名称访问器预绑定、目标entry字典；插件先记录异常再分项清理。第一次候选构建成功0错误/5DTO字段警告，字段已补默认值尚未复建；没有运行实机或部署。
- 新tools/Generate通过Cecil产生PatchManifest928个目标、SourceAliases3271、GenericManifest4。当前别名包含无关回调，可收窄。tools/Catalog已扩展30个tr委托和历史固定核心词表导出，最近生成5010模板、1868映射、112未解析表达式、1381条数据词条；全量翻译仍在进行。
- 已安装TypeTreeGeneratorAPI到临时deps，`tools/Research/extract_progress.py`成功只读提取场景progressPoints，证据覆盖多个level：GLOOM biome8、THE CITADEL biome8；CALDERA/THE KILN biome3。Swamp→GLOOM确认，OfficialTerms及词表/配置已改雾沼，旧研究报告和正式memory的“待确认”需在收尾更新。
- 接下来：完成Display/Resource补丁及全部词表、实际方法测试、完整审核分类、统一PLAN和memory、构建与部署。资源目录、UI/联机与性能采样仍未验收。不得提前结束为部分成果。

## 最新恢复入口：术语/性能研究完成，仍未改插件

- 用户最新要求接续调研：游戏已有中文优先，优化反射。官方SerializedTermsData已从当前data.unity3d提取（resources.assets path1422，866键，简中索引9）；详细证据/原文件hash在开发区TERMINOLOGY_PERFORMANCE_RESEARCH.md及research/2026-10-05-terms-performance/。
- SHORE=海岸（用户已明确同意官方值，覆盖之前海滩暂定），TROPICS=雨林，ROOTS=森蕈，ALPINE=雪山，MESA=方山，CALDERA=火山，THE KILN=熔炉，GLOOM=雾沼，THE CITADEL=城塞，PEAK=顶峰，AREA_VOID/NADIR=天底；游戏营火也应统一。未发现NADIR同名键，不要据此判断没有译名。Swamp→GLOOM仍需场景确认，类型树进度点提取0匹配不构成反证。
- 别名处理要在显示端：BiomeLabel/CtBiome直接返回内部名；PartnerOf、RowLabel、SegmentFocus、阶段长说明还有独立漏口。PartnerOf参与数据逻辑不得改中文；用户SegmentNames/作者内容保留。Desert资源前缀需来源核验再关联方山，普通名词不全局替换。
- 字体FieldInfo数组已缓存但每次仍遍历：EditorUi314/相关59、WorkshopPanel296/相关4、TeamUi6/2、LaunchPanel48/10、ForgeHud9/2、NpcHud5/0。TeamUi每个Style请求先FindFonts；postfix不受原方法提前返回保护。优先改创建/重建注册、代次刷新、精确访问器、重复setter快退和清理；保全懒创建样式与字体恢复。普通字体已有300帧失败节流，TMP缺少独立节流。
- FieldPostfix每次找错字段、MarkerRef每次反射强制失效且把En属性当字段/无中文guard，正确性与性能同修。配置GetDisplayName缓存精确访问器；固定对索引/动态调用点分桶。现有最大规则桶24，非每次扫1000；初始化扫描和_bump.Invoke事件反射优先级较低。
- 研究工具独立构建运行成功；生产12源码/数据hash及原/CN DLL未变（CN26D877...836CC）。未测实机FPS、未实施/部署本轮方案。下一步按报告顺序修复与验证，完整缺项仍沿用下方全量复核；当前仅调研范围不能冒充已完成优化。

## 前一阶段：全量复核完成，修复尚未实施

- 用户实测称“有进步，但仍有问题”，要求严谨复核，并强调“不只是图片里的”；构件 Desert_Rock_1、材质 M_Rock 等玩家可见名称明确在范围内。本轮只审核，未改插件源码/词表/DLL/config/地图。
- 21时复核：最新日志188有初始化完成信息，仍有124条扫描跳过警告、1条Strip显式失败及2条本补丁ModConfig声明警告。旧“等待确认初始化”状态已过时。
- 全474文件重新解析0错误；源码/IL各4,968直接Tr。固定4,204=2,006有映射+2,197无映射+1语言代码。缺映射去重2,042。764非固定中源码外层登记148、实际IL登记127，差异21处回调/局部函数；登记不等于模板全部命中。
- 生成库存实际907固定规则+93动态模板；双大括号参数探针93条均复现插入后被错误解码（不是93条实机案例），6条Same为空需逐条核验。
- 已确认：字段getter错配、Loc.En属性误当字段导致Marker缓存失效错误、LoadOverlay变量调用带跳转标签被跳过、泛型Strip失败、Home键→首页错译，以及额外30处本地tr委托调用。构件/材质显示与搜索均需单独接入，不能修改保存资源键；完整运行目录键仍待采集。
- 报告：MOD开发/PeakWorkshopCN/AUDIT_RECHECK.md（R01–R15及完整功能矩阵）；清单audit/2026-10-05-recheck/，工具tools/Recheck。STATUS/TODO以此为当前事实，以下旧记录仅作历史。
- 当前安装仍为0.1.0.0、304,128字节、26D877A2...836CC，原DLL哈希仍CC1ECDE5...8ADC。嵌入JSON与工程一致。之前41项测试未覆盖WorkshopPatches/字体/初始化，不能视作本轮问题已修复。

## 16:58部署历史

- 2026-10-05 16:58:48 +08:00：用户说“已退出”，确认没有 PEAK 进程后，将已测试的 `.build/PeakWorkshopCN/PeakWorkshopCN.dll` 覆盖到指定 `2.0.a` profile。版本 0.1.0.0、304,128 字节，源/目标 SHA256 均为 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`；原 DLL hash 仍为 `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`。
- 本轮仅部署既有构建并同步文档，没有重新改码/构建。修复版主项目 0/0、已有 41 项测试通过；测试未覆盖 Unity 中 WorkshopPatches 初始化。下一步由同一 profile 重启，核对初始化完成日志与多人页，尚不能声称 UI 已修复。
- 证据边界：旧日志有 124 条入口跳过警告（多涉及开放泛型）且无初始化完成信息，没有完整致命异常栈。逐入口容错用于避免单个可选入口失败终止后续安装，不能证明开放泛型、变量字段及缓存语义已正确。

## 审核与首轮补齐历史（当前部署以上文为准）

- 用户反馈截图及其他页面绝大部分英文，最新要求“先重新审核缺失项”。当前停止补码；本轮只生成AUDIT.md和audit/2026-10-05/明细、同步memory，源码/DLL/config未变化。
- 2026-10-05 用户随后说“开始”，授权第一轮补齐并部署：Boss 来源改为 DrawBosses；词表新增数据共 742 条、原 DLL 静态命中 2,006/4,204；1,000 条生成规则包含固定文字及动态模板，108 条表达式仍待审。41 项测试通过，主项目 0/0。该轮旧 DLL 为 303,616 字节、SHA256 A92CD62B54EA77A1DEFC1B0E795EA0E6B7DF115E8CD35FD2D250F808EFC97DC6，现已由顶部修复版替换。
- 本轮新增 CatalogData、RawLiteralRewriter、Data/Translations.Airport.json、Data/BuiltinRules.json 与 tools/Catalog 生成工具；接入 ScatterPreset、音效/特效/许可证字段、MarkerRef 缓存失效，裸写 Kick/NPC 数量及泛型 Show all。仍不是完整覆盖，实机复测、音乐/运动/放置辅助方法与大量剩余词条待继续。
- 新截图复核后增加了方法体读取、显式补丁和 getter 的逐入口容错；准备构建时游戏锁定旧 DLL，用户退出后现已完成覆盖。具体中断位置仍待运行日志确认。
- 全474文件Roslyn解析无错误；真实源码Tr调用4,968，Cecil原DLL同4,968。审核基线固定4,204位置：1,081命中、3,122未映射、1机器代码；非固定764：15位于旧规则来源、749无旧规则来源。当前固定命中已为2,006，不能沿用基线缺失数量作为当前统计。
- 审核发现 NpcHud.OnGUI 没有 Tr，Boss 文字位于 DrawBosses:91；首轮补齐已纠正来源，实机仍待验证。最新审计确认 71 个动态来源方法存在，含 687 个原 Tr 调用（含固定/回退，不等于翻译模板数）。
- 遗漏不止词表：ScatterPreset变量表、ItemSounds/ForgeVfx/GameMusic/许可证直接语言分支、ForgeMotion/PlacementSetup、加载阶段/JoinFlow、裸Kick和NPC数量、泛型Strip<T>、MarkerRef缓存等。
- 已有用户实机截图/日志：首版加载成功，无本插件初始化失败/文字hook跳过，部分工坊IMGUI中文可见；不能据此认定新版完整字体/布局/开关/联机通过。新版 hash 为 A92CD62B...97DC6，原DLLhash仍CC1ECDE5...8ADC。
- 明细为16个CSV/JSON，包含完整源码路径/行号/方法/表达式、IL偏移、字段/显示候选、动态来源检查和62组词表上下文候选。当前词表仅英文匹配且忽略大小写，如Remove用于踢人等场景需复核；不能把所有候选都称为必译UI或已确认错误。

## 首版实现背景

- 用户最初要求调研不写代码，之后“先执行，我看看效果”已授权实施；菜单、提示、编辑器标签和配置说明均在范围内。最新追问长动态描述为何不补，已经澄清长度不是省略依据并补充常见动态模板。
- 原 MOD 实际为英文/德文，PeakWorkshop 0.2.0，GUID `com.peakcode.peakworkshop`，内部命名空间仍 PeakMapForge；游戏使用用户指定 2.6.a。
- 474 个 CS 文件；Loc.Tr 4,979 处搜索匹配并非去重词条。集中补丁还需补动态文本、直接 Loc.En 分支和配置说明。
- 不修改 Loc.En/Code/Setting 等语言逻辑：它们进入数字格式与 Bridge；ByteText.Culture 依赖 Tr("de", "en")。
- IMGUI 要 UnityEngine.Font，TMP 要 TMP_FontAsset，分开适配。不能仅换中文字符串就声称已完成。
- HardDependency 后原 Awake 已执行，要刷新原已绑定描述和有限固定标签缓存；MarkerRef.Label 只按 bool En 缓存，单调 Loc.Bump 不保证所有缓存刷新。
- WorkshopPass 4 个内置 key 可白名单翻译；自定义地图/NPC/物品/在线内容保留作者原文。原本地化表会重注入，要处理覆盖顺序。
- 输出固定 profile plugins/PeakWorkshopCN.dll，已部署；当前产物及 hash 以上方最新恢复入口为准；不打包上传。
- 详细证据：`MOD开发/PeakWorkshopCN/RESEARCH.md`；计划：同目录 `PLAN.md`；正式memory六件套已建立并同步入口/索引/履历。
- 已实现默认中文独立开关、相邻字面量 Tr 调用点、5 个分类显示入口和4 个通行证 key；保留原 auto/en/de 及 Bridge。变量作者内容保持原文。
- 固定审计命中 2,006/4,204；1,000 条生成规则包含固定文字及动态模板，仍需复核生成规则的参数保护。手写动态规则有德英参数一致检查、8,192 字符及 10ms 正则上限回退。69 配置名称/67 非空说明按目标实例 getter 显示，原值/键不变；可选 ModConfig。
- 41 项测试通过，主/测试 Release 0/0；Cecil 检查 12 字体 hook、69 配置身份。字体/材质恢复及加载字形同步已有实现，MarkerRef.Label 缓存已增加失效入口，但仍未实机验证。
- 当前详细状态见开发区 IMPLEMENTATION.md。下一步游戏内工坊、编辑器长说明、加载页和开关/联机验收，继续补遗漏；不把测试或编译称为实际 UI 成功。无外部 update_memory 工具，本地 Markdown 为已落盘记忆。


