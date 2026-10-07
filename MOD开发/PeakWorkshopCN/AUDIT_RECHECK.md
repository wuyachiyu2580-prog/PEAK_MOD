# PeakWorkshopCN 0.1.0 全量复核（二次）

> 后续状态（2026-10-06）：本报告记录的是实施前缺项基线。统一修复候选已按其清单完成离线实现，候选 DLL 位于 `.build/peakworkshopcn-unified/candidate/`，但尚未覆盖 profile，也未完成 Unity 页面、联机和性能验收；本报告中的“待修”数量不可直接当作候选当前覆盖率。

日期：2026-10-05。审核对象：当前安装的容错修复版，SHA256 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`。

结论：**尚未达到完整中文化要求。** 新日志已证明初始化完成，但仍存在大量缺词、遗漏显示入口，以及有译文却不可达、字段错配、缓存失效错误。用户的四张截图是复现样例，本次扫描覆盖全部 474 个反编译 C# 文件和原 DLL，构件及材质名称也列入显示本地化范围。

本轮只新增审核工具/清单/文档；没有修改插件源码、词表、DLL、配置或地图。下述修复动作是审核建议，尚未实施。旧 `AUDIT.md` 和 `audit/2026-10-05/` 保留为补齐前基线，当前数字以本报告为准。

## 证据与计数

- Roslyn 重新解析 474 文件，0 解析错误；源码和原 DLL 都有 4,968 个直接 `Loc.Tr` 调用。
- 安装版的两个嵌入 JSON 与当前工程数据哈希完全一致；原 PeakWorkshop 0.2.0.0 哈希仍为 `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`。
- 当前 `LogOutput.log:188` 有 `2013 fixed text sites, 216 dynamic call sites, 67 configuration descriptions. Partial test build.`，不能再把所有英文归为初始化没有完成。
- 日志仍有 **124 条扫描跳过警告、1 条显式 Strip 补丁失败**，另有 2 条属于本补丁的 ModConfig 方法声明警告。大量扫描警告涉及匿名泛型，不能等同于 124 处用户文案失败。原 DLL 中含 `Loc.Tr` 的泛型位置只有 1 处：`WorkshopPanel.Strip<T>` 的 `Show all`，它已有词条但实际补丁仍失败。
- 日志 `209` 同时记录 WhySoLaggy 对 Strip 的补丁失败；其相互影响未隔离，不能断言全部由某一个 MOD 引起，也没有为审核停用其他 MOD。

| 统计对象 | 本次结果 | 含义 |
|---|---:|---|
| 固定字面量调用位置 | 4,204 | 并非去重词条数 |
| 有固定映射 | 2,006 | 47.7%，只表示静态查表命中，不是界面完成率 |
| 无固定映射 | 2,197 | 去重英文 2,042 条；含需保留/另判上下文者，不全等于必译文案 |
| 保留语言代码 | 1 | `Tr("de", "en")`，用于原数字格式 |
| 非固定调用位置 | 764 | 变量、拼接、格式化、字段及作者内容混合 |
| 源码外层方法登记规则 | 148 / 764 | 其中 21 处在回调/局部函数，不能据此外推为运行可达 |
| 实际 IL 方法登记规则 | 127 / 764 | 其余 637 处没有对应来源规则；这 127 处也不保证具体模板命中 |
| 生成规则库存 | 1,000 | **907 条固定规则 + 93 条动态模板**，此前“1,000 个参数模板”表述不准确 |
| 直接语言读取 / 显式语言参数助手 | 30 / 12 | 包含数字格式、作者内容及工具报告，需按用途处理 |
| 本地 `tr` 委托调用 | 30 | 队伍名称/队伍校验/玩家校验；不在 4,968 个直接调用内 |
| 直接显示候选 | 1,734 | 启发式收集，含控件 ID、布尔条件等假阳性，不能计作缺词 |

### 全模块缺项分布

按源码命名空间归组；固定映射有无以当前词表判定。最后一列不是已确认缺词数。

| 模块 | 固定有映射 | 固定无映射 | 非固定待逐条核验 |
|---|---:|---:|---:|
| 编辑器 Editor | 480 | 1,470 | 347 |
| 工坊/机场 Airport | 1,405 | 507 | 314 |
| 物品构建 Build.Items | 35 | 60 | 13 |
| 队伍 Teams | 23 | 55 | 16 |
| 联机 Net | 8 | 37 | 18 |
| 在线 Online | 4 | 20 | 15 |
| 起飞 Launch | 40 | 18 | 11 |
| 加载/生成 Generation | 2 | 12 | 11 |
| 构建 Build | 7 | 10 | 6 |
| NPC | 2 | 6 | 11 |
| 游戏补丁 Patches | 0 | 2 | 0 |
| 模型 Build.Models | 0 | 0 | 2 |

另有 Online 的 1 处语言代码保留。没有直接 Tr 的类仍可能提供字段或资源名称，不能从此表推断它们没有本地化工作。

## 已确认的接入及正确性问题

以下 `原/` 指 `引用参考代码/反编译/BepInEx/plugins/PeakWorkshop/`；`补丁/` 指 `MOD开发/PeakWorkshopCN/PeakWorkshopCN/`。原路径下保留全部具体行号的 CSV 见末尾清单。

| 编号/优先级 | 问题与证据 | 影响与修订要求 |
|---|---|---|
| R01 / 高 | 构件默认返回资源键：`原/PeakMapForge.Editor/EditorUi.cs:8311` 的 InPrimLabel 对普通 prefab 返回 key；`:16958` 用于列表/拖拽，`:5970` 用于属性标题，`:12923`、`:12989` 用于场景条目，`:17575` 为散布选择，`:17654` 为提示 | `Desert_Rock_1` 等未接入中文显示。建立官方资源键→中文名称映射，在显示位置使用，放置/保存继续用 key；详见下一节 |
| R02 / 高 | 材质目录直接使用对象名：`原/PeakMapForge.Build/MaterialCatalog.cs:29,46`；`EditorUi.cs:5808,5811,5826,5832` 直接展示/筛选/选择 Material 名称。`RockLook.cs:129` 只去掉 M_、下划线等 | M_Rock 等不经过 Tr。地形列表、当前材质摘要、物件/散布岩石外观都要接；不能替换 MaterialCatalog 的 key |
| R03 / 高 | `补丁/WorkshopPatches.cs:112` 的 FieldPostfix 不看原 getter 名，固定优先 De/En、DescDe/DescEn、Label、Hint、Short | ForgeVfx.Desc 优先取 Name 字段，LicenseInfo.Hint 优先取 Label，ScatterPreset.Hint/Short 优先取 Label；名称有译文时会错误覆盖说明。已确认 Rocks 的长 Hint 会被覆盖为“岩石”，原本为空的 Short 也可能变成名称。必须按属性精确匹配字段 |
| R04 / 高 | 生成器 `tools/Catalog/Program.cs:66` 把 lambda 文案登记在外层方法。当前 IL 中 SubscribeButton/RunVisibility 等文案实际位于 `WorkshopPanel+<>c__DisplayClass...<...>b__...`，这些源不在规则表 | 21 个源码候选发生在回调/局部函数，IL 登记数 148→127。已有动态译文也可能不执行。必须核对编译后的实际方法；不能按外层登记数宣布成功 |
| R05 / 高 | `补丁/LiteralRewriter.cs:37` 跳过带标签/异常边界的调用；原 DLL 共有 9 个这种 Tr 位置。`LoadOverlay.StageText` 的变量调用在 IL 82，带跳转标签，虽有 FieldSources 仍被跳过 | 加载阶段文字没有真正接上；ScatterPreset 三个 getter 同样有边界，只能依赖当前有错的后缀。需要正确转移标签/边界，或改为精确显示 getter，不可直接删保护 |
| R06 / 中 | `补丁/WorkshopPatches.cs:137` 用 GetField("En")，原 `Loc.cs:44` 附近 En 实为属性。于是 LocEn 恒回退 false，`:131` 总把 `_cmLabelEn` 写 true | 原语言为英文时不会按预期失效已有 MarkerRef 标签；德文时可能每次读取都重算。需按中文开关/语言代次刷新缓存，开关前后均验证 |
| R07 / 中 | 日志185：`Direct text hook skipped ... WorkshopPanel.Strip: IL Compile Error`；此处 Show all 已有映射 | 泛型接入失败是独立于缺词的问题。只删除跳过条件再包 try/catch 没有解决它；需要封闭泛型/调用点方案并验证实际 patch 注册 |
| R08 / 高 | `补丁/CatalogData.cs:90` 插入捕获值之后再执行 `.Replace("{{","{").Replace("}}","}")` | 会修改参数中的作者原文。对 93 个生成动态模板注入相同含双大括号的边界参数，均能复现变化；这是匹配器边界探针，不是 93 条实机案例。应只解码模板常量，原样插入变量 |
| R09 / 中 | `原/EditorUi.cs:5471` 的 Home 是键盘 Home 键，现有词表与首页共用翻译为“首页” | 已确认的上下文错译，不能把有映射等同于正确。需要德英对/调用用途限定；帮助中的按键保持可识别 |
| R10 / 高 | `原/EditorUi.cs:24582,24709,24834` 的 TeamStandardName/TeamIssues/PlayerIssues 用传入 `tr` 委托，实际 UI 在 `:23399` 等传 Loc.Tr | 30 个委托调用绕过现有扫描。相同助手也被 TeamsBridge 使用显式语言选择，修复要限定 UI 调用，不能污染 Bridge 返回合同 |
| R11 / 高 | `原/ScatterPresets.cs:605,610` BiomeLabel/CtBiome 直接按 En 返回生物群系名，英文字段返回原 key；游戏音乐用 `EditorUi.cs:10709,11115` 的 `En ? ... : ...` | Shore/Tropics/Roots/Alpine/Mesa/Volcano/Swamp 等，以及音乐名，不靠增加固定 Tr 词条就能解决。Kiln/Citadel 及路线搭配显示还要检查直接 PartnerOf 输出 |
| R12 / 中 | 最新日志186–187对本补丁的 ModConfig GetDisplayName 给出声明方法警告；补丁只处理名称/说明 getter | 67 条配置说明登记成功不代表分区、选项、已打开菜单刷新都正确。可选兼容需按实际声明方法处理，分区与选项单独审核，不改配置值 |
| R13 / 中 | `WorkshopPatches.cs:63` 等显式安装与全程序集扫描重叠；`:143` 用累加方式计数，Harmony 重生成也可累计 | 日志2013不是静态2006的“额外7处已完成”。后续报告应按真实方法+IL位置去重，分别记候选、安装成功、跳过及原因 |
| R14 / 中 | 生成规则有 6 条动态规则 Same 为空；生成器对 De/En 使用笛卡尔组合、名称字符串替换和单参数兜底 | 此项是参数语义风险，不能把 6 条全部判为错误：错误信息德英内容可能本来不同。需要逐条验证对应生产者，不再笼统声称所有生成模板都有德英参数一致保护 |
| R15 / 中 | `Plugin.cs:51` 在输出初始化异常前调用 UnpatchSelf 和字体清理；清理未独立容错。当前测试工程没有编译 WorkshopPatches/RawLiteralRewriter/FontPatches/Plugin | 初始化失败可能被清理异常遮住，现有41项测试也覆盖不到上述接入和缓存错误。需要真实目标注册、字段 getter、回调、跳转边界和开关恢复验证 |

R03 是代码可证明的字段错配，未将其冒充用户截图中已经观察到的现象；R06/R08 同理。R07 有当前运行日志证据，R01/R02 有用户反馈与源码证据。

## 构件和材质应怎样纳入

此前“内部 ID 保留”不能当作“不翻译玩家看到的名称”的理由。这里需要区分同一字符串的两种用途：

| 原键 | 中文显示示例 | 仍使用原键的地方 |
|---|---|---|
| Desert_Rock_1 | 沙漠岩石 1 | PrefabCatalog.TryGet、拖拽 payload.Name、Place、PropSpec.Prefab、散布 Prefabs |
| M_Rock | 岩石（材质） | MaterialCatalog.Find、TerrainSpec.Material、PropSpec.Material、ScatterSpec.Material |

示例中文名是后续词表方案，不表示已经部署。

必须一起处理：构件列表/网格、场景树和当前选区名称、属性标题、拖拽/放置提示、散布预设选择、材质选择及当前摘要、岩石外观名称、资源搜索、语言开关后的缓存。原名建议在悬停提示中保留，并让搜索同时接受中文和原名。

搜索有单独缺口：`PrefabCategories.Grouped:296` 最终只对原 name 做 IndexOf；地形材质 `EditorUi.DrawTerrain:5826` 也只搜索原名。只改显示会出现“看得到中文却搜不到中文”。

目录来源是运行时数据：PrefabCatalog 从生成器、物品库、场景模板及自定义资产收集；MaterialCatalog 从当前场景 Renderer 的 shared materials 收集。当前日志228–229记录这次目录重建有 **467 个 prefab 条目、286 个材质条目**。这不是完整游戏资源数，也不是全部未翻译数；后续 RegisterAssets 还会追加/替换条目。反编译源码没有完整列出它们，`Desert_Rock_1` 就不是当前源码中的固定文案。

完整资源命名验收仍需采集 2.6.a 的实际目录键、来源和类型，并覆盖各群系/变体加载后的增量。不得把旧版本导出数当本版总量；不能只靠 `M_`/下划线就认定是官方资源；玩家导入模型、自定义物品名称及作者正文保持原样。未知官方键应纳入遗漏清单继续补词，不能声称已覆盖后静默遗漏。

## 按功能的复核范围及缺项

固定位置逐项见 `missing-fixed.csv`，非固定表达式见 `nonliteral-review.csv`；表中是人工核验的代表入口，不是只修这些入口。

| 功能范围 | 本次仍有问题的入口/内容 |
|---|---|
| 创建地图完整向导 | WizardKind、KindCards、PlanEditor、ModeCards、BiomeBlurb、WizardDetails/Summary、AscentEditor、End/Goal选项、默认文案；截图中的类型/生态描述都不是作者内容 |
| 工坊地图列表/详情/路线编辑 | DrawMapDetail、DrawRemoteDetail、ContentPublishBox、缩略图/来源/订阅/下载/失败提示 |
| 多人大厅全流程 | 筛选/创建/房间设置/准备状态/加入确认/删除发布/可见性回调；部分已中文，不能因此略过剩余动态分支 |
| 素材/物品/NPC制作 | OriginForm、DrawSources、DrawTexturePanel、残余音效与特效属性；GameItem/订阅/导入/删除回调 |
| 我的内容/回收站/在线维护 | DrawMine、AskPurgeTrash、News/Moderation、PmfApi错误外围文字；在线用户正文另作保留 |
| 编辑器帮助与操作提示 | HpContent仍有114个固定位置无映射，EditorTools另有86个；Home按键错译独立修正 |
| 地形与散布 | DrawTerrain缺42、ScRules缺30；地形模式、尺寸/坡度/净空、预设Label/Hint/Short、构件及材质名 |
| 物品/交互/效果 | InEffects缺33、DrawInteract缺30；拾取、燃烧、可摇晃、发射器、模型外观及运行提示 |
| 传送/液体/流程/危险 | DrawLiquid缺29、DrawFlow缺27、DrawTeleport缺25、DrawHazard缺24；变量摘要和辅助显示 |
| NPC/波次 | DrawNpcProp缺28、DrawWaves缺14；敌我/掉落/对话外围说明、Boss/波次HUD。作者对话不翻译 |
| 世界/区段/出生/标记 | WoSegments缺10、WoSpawns缺8、WoMarkers缺3，加动态名称；地图目标、篝火、雾墙、区域/场景源名、原生别名链 |
| 音乐/天空/运动 | DrawSky缺22、DrawMotion缺15、MusicPicker缺15、WoMusic缺14；GameMusic/ForgeMotion显式语言助手 |
| 队伍/规则/胜利 | WoTeams缺16、WoTeamRules缺14，Teams模块另有55个固定缺映射；30处委托调用及默认队伍名称 |
| 加载/起飞/联机异常 | LoadOverlay、LaunchPanel、JoinFlow、LoadSync、ResolverNotice；字段生产者→显示者链、数值/列表参数保留 |
| 配置/字体/开关 | 69个配置身份、67条非空说明已离线核对；实际选项/分区/恢复仍需验收。截图说明部分汉字可显示，不能证明所有页字体、换行、缩放正确 |

## 修复与验收顺序

1. **先修翻译接入的正确性**：字段按属性对应、Marker缓存、回调真实来源、跳转边界、泛型、参数原样保留、委托UI适配。每项用实际原DLL方法/字段/IL作为验证对象，不只给模板传一个手写来源字符串。
2. **补全固定文案和独立显示助手**：以全量逐项清单为准，每页同时核对固定/动态/数据字段/直接语言选择，优先编辑器与建图流程；长说明不因长度跳过。
3. **资源名称与中文搜索**：对完整运行目录建立映射及漏项报告，显示名与原键分离；覆盖上节的全部显示入口；验证选择/拖拽/保存/重载仍引用同一资源。
4. **逐页实机验收**：机场→完整建图→各编辑器页→游玩HUD→加载/大厅→配置。检查EN/DE回退、中文开关多次切换、既有缓存、字体和长文本布局；用含双大括号/富文本字符的作者名字做隔离测试。
5. **证据收口**：记录安装成功目标、跳过原因和资源未知键。验收时每个遗留项必须归入“应翻已完成 / 作者内容保留 / 机器键保留 / 尚未完成”，不能用总hook数替代页面验收。

## 产物与复现

目录 `audit/2026-10-05-recheck/`：

- `summary.json`、`module-summary.json`、`method-summary.csv`：本次最新计数与全部含直接Tr的方法汇总。
- `tr-source-calls.csv`、`tr-il-calls.csv`：全量源码/IL证据；`missing-fixed.csv`、`nonliteral-review.csv` 为待分类清单。
- `callback-source-mismatches.csv`：21个来源登记需改接实际回调的候选；`il-special-sites.csv`：9个边界调用与1个泛型调用。
- `display-candidates.csv`、`language-branches.csv`、`language-helpers.csv`、`local-bilingual-calls.csv`、`bilingual-fields.csv`：直接显示/字段/委托路径。
- `generated-rule-probes.json`：参数双大括号边界探针与6条未做捕获相等校验的规则；不是UI验收测试。
- `source-files.json`、`plugin-source-hashes.csv`、`resource-verification.json`：审核输入指纹及嵌入数据一致性。
- `runtime-evidence.csv`、`runtime-summary.json`：当前日志相关行、时间与哈希，未复制无关日志内容。
- `source-parse-errors.json`：空数组，表示本次语法解析无错误。

审核工具：`tools/Recheck/Recheck.csproj`，只读源文件/程序集，输出清单。运行方式：

```text
dotnet run --project MOD开发/PeakWorkshopCN/tools/Recheck/Recheck.csproj -- <原MOD反编译目录> <原PeakWorkshop.dll> <已安装PeakWorkshopCN.dll> <报告目录>
```

另使用原有 `tools/Audit` 对当前 DLL 做固定位置与12个字体hook签名、69个配置身份的只读检查。

限制：静态扫描不能穷尽运行时收集的资源实例，也不等同于逐页游戏操作。1,734个显示候选仅按常见显示API/表达式收集，含假阳性并可能漏掉间接数据流；30处Loc.En也不是所有语言选择的总数（显式参数/委托另列）。本报告给出全量直接调用清单和已核实问题，不声称所有候选已经逐一语义验收或所有资源都已列齐。
