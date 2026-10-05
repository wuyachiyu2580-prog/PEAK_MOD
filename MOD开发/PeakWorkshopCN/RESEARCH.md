# PeakWorkshopCN 调研

日期：2026-10-04。状态：源码调研完成；未写代码、未构建、未运行游戏验证。

## 1. 需求与基线

用户指定新增 PeakWorkshopCN `0.1.0`，以补丁中文化，先调研再计划。用户随后确认包括菜单按钮、提示、编辑器标签和配置说明。

用户最初写“中文和德文”，但实际原 MOD 只有英文与德文；本轮已在澄清问题中说明，用户确认上述界面范围。

| 项目 | 实际证据 |
|---|---|
| 原 MOD | `PeakMapForge/Plugin.cs:25` 的 BepInPlugin：`com.peakcode.peakworkshop` / `Peak Workshop` / `0.2.0` |
| 程序集 | `PeakWorkshop`；`Properties/AssemblyInfo.cs` 声明 `0.2.0.0`，目标 `netstandard2.1` |
| 内部类型 | `PeakMapForge.*`，不能按产品名猜成 `PeakWorkshop.Loc` |
| 安装包 | profile 的 `PeakCode-PeakWorkshop/manifest.json` 声明版本 `0.2.0` |
| 原 DLL | profile 的 `PeakCode-PeakWorkshop/PeakWorkshop/PeakWorkshop.dll`，4,465,152 字节 |
| 原 DLL SHA-256 | `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC` |
| DLL 元数据核验 | AssemblyName.GetAssemblyName 只读确认 `PeakWorkshop` / `0.2.0.0`；未执行原程序集 |
| 游戏 | 本次以用户指定的 `引用参考代码/反编译/2.6.a` 为源码依据；没有宣称已完成该版本实机兼容验证 |

反编译目录不是可直接复编译的原工程：存在未知 IL、编译器生成初始化等还原痕迹。原 csproj 的 LangVersion 14.0 也不是新补丁必须采用的要求。

## 2. 文本规模与覆盖边界

对原 MOD 的 `.cs` 静态搜索：474 个文件，`Loc.Tr` 4,979 处匹配，分布在 4,157 行、77 个文件。EditorUi 命中 1,675 行，WorkshopPanel 命中 1,723 行。配置 `.Bind<` 命中 69 行，20 个文件。

这些是源码搜索计数，不是去重翻译条目数，不是完整运行时覆盖率。重复调用、静态数组、字符串拼接、插值、变量参数及不经过 Loc.Tr 的文本均存在。配置命中行也不等同于已经枚举完全部配置键。

| 来源 | 代表文件/方法 | 调研结论 |
|---|---|---|
| 固定德英文本 | `PeakMapForge/Loc.cs:170`，`Tr(string de, string en)` | 集中补丁入口，精确词典优先 |
| 动态说明/状态 | `EditorUi.cs`、`WorkshopPanel.cs`、`TeamHud.cs`、`NpcHud.cs` | 参数已插值或拼接才送入 Tr；单纯完整字符串词典无法覆盖 |
| 变量德英对 | `ScatterPreset.Label/Hint`、`JoinFlow.Error/ConfirmText`、`LoadOverlay.StageText` | 部分来自内置表，部分需追溯生产者 |
| 直接语言分支 | `PrefabCategories.DisplayIn`、`HazardCatalog.Display`、`WorkshopLibrary.DisplayCategory/DisplayDifficulty`、`WorkshopPanel.DisplayAssetCategory` | 不经过 Tr，需要专属显示入口 |
| 内置声音/效果名称 | `ItemSounds`、`ForgeVfx`、EditorUi 中直接 `Loc.En ? En : De` | 需要对显示值补充覆盖；资源 ID 保留 |
| 工坊通行证 | `WorkshopPass.EnsureTexts` | 通过 CustomItems 向游戏本地化表写 4 个固定 key |
| 配置描述 | `Plugin.Awake`、`UiPrefs.Bind`、`OnlineConfig.Bind` 等 | 德文/德英硬编码，未经过 Tr |
| 界面显示的诊断 | `BuildReport`、ModelLibrary、ReachValidator 等 | 部分由德文报告、异常和服务器信息汇总；翻译显示副本，避免改诊断原始列表 |
| 玩家/作者内容 | map description、NPC Name/Text/NameEn/TextEn、在线内容 | 不是 MOD 自带词条；保留原文，不自动机器翻译 |

## 3. 原语言实现

`Loc.Bind` 将 `Editor/Language` 绑定为 `auto`，只接受 `auto`、`de`、`en`；`Parse/Set/Toggle` 都只有这两个语言分支。`En` 在 auto 时读取 `LocalizedText.CURRENT_LANGUAGE`，只有枚举值 3（German）走德文，其他游戏语言都走英文。

因此游戏设为中文仍会显示英文；向原配置写 `zh` 或 `zh-CN` 会被当成 auto，不能直接获得第三语言。

`Loc.Code` 不只是界面上的 EN/DE，它进入 `EditorUi.State()` 的 `lang` 和 `langSetting`，供 Bridge 使用。`Loc.En` 还控制 `Numbers`、`UiNumbers`、`EditorUi.UiNum` 和若干显示选择。`ByteText.Culture` 用 `Loc.Tr("de", "en") == "de"` 判断数字文化。

结论：不要将 `Loc.En/Code/Setting` 全局改成中文，也不要翻译 `Tr("de", "en")`。新增中文显示层应保留原语言配置、数字格式和 Bridge 合同。

## 4. 缓存和启动顺序

`Loc.Bump()` 会增加 Version 并调用 Changed。EditorUi 在 OnEnable/OnDisable 订阅/退订；其 OnLanguageChanged 仅设置 `_paletteDirty=true`，不代表所有缓存均刷新。

`EditorSession.MarkerRef.Label` 使用 `_cmLabelEn`（bool）缓存标签。英文与中文显示切换若底层 En 均为 true，此缓存不会自动失效。EditorUi 场景列表还以 `Loc.Version` 判断刷新；LoadOverlay 同样观察该版本。

采用原 MOD HardDependency 时，原 Plugin.Awake 会先执行。中文补丁 Awake 中安装 Harmony 后，不能声称已拦截过原来的配置绑定或启动时静态文本创建。实施时需处理已绑定 ConfigEntry 和已存在的固定文本缓存；后续新实例再通过补丁生成。禁止重跑原 Awake、静态构造函数或全部 Boot。

## 5. 字体有两条独立链

### IMGUI

EditorUi.FindFont 选 Montserrat/NotInter/Inter 等正文和 Daruma/Tetsubin/Londrina 等标题；TeamUi.FindFonts 和 WorkshopPanel.FindFonts 也按固定拉丁字体名字查找。返回的是 `UnityEngine.Font`，被 GUIStyle/GUISkin 使用，不能把 TMP_FontAsset 赋给它。

主要入口：EditorUi.FindFont/Styles、TeamUi.FindFonts/Style、WorkshopPanel.FindFonts/St/_fieldStyle、LaunchPanel.Styles，及使用独立 GUIStyle 的 ForgeHud/招牌提示。TeamUi 样式缓存检查字体是否变化，WorkshopPanel 的样式缓存只检查 key，后者需要额外处理已有样式和影子/输入框缓存。

计划先验证已有游戏动态字体是否覆盖常用汉字，必要时使用系统中文动态 Font；缓存并在场景/语言变化时按目标 UI 更新。本机 Windows/Fonts 已找到 msyh.ttc、simhei.ttf、simsun.ttc；仅证明字体文件存在，不证明 Unity 动态字体已成功创建、中文字形或测量效果已验证。

### TMP

MenuSignRig 创建 TextMeshPro；LoadOverlay 创建 TextMeshProUGUI。LoadOverlay.FontFor 优先 Pangolin，Covers 只检查 `AaZzÄäÖöÜüß0123456789%`，不保证中文覆盖。MenuSignRig 也优先 Daruma SDF。

PEAK 2.6.a 的 FontFallbackSwapper 有 `mainBaseFont`、`simplifiedChineseFont`、`traditionalChineseFont`。LocalizedText.SetLanguage 会切相应 fallback；中文枚举为 SimplifiedChinese=9、TraditionalChinese=10。

优先沿项目 FontHelper 规范复用游戏 UI 中文能力；2.6.a 可额外核验上述明确字体引用。只为 Workshop 自有组件/字体副本补充中文，不改其他 MOD 全局样式，不修改共享字体图集和全局 fallback。

LoadOverlay.Clean 对缺失字形仅替换少数标点；Replacement 默认返回 null，汉字不会因此被删除。此处风险是缺字和 Glyphs 字形缓存，而不是已证实“过滤掉中文”。

## 6. 游戏本地化表与配置

CustomItems.AddText/Inject 将英文铺到所有语言槽，仅将 German=3 覆写德文；ReinjectTexts 在原 ItemTextPatch 的 LoadMainTable postfix 中重新写表。因此若覆盖中文槽，还要应对重新注入和补丁顺序。

WorkshopPass 内置固定 key：`NAME_WORKSHOPPASS`、`WORKSHOPPASS_OPEN`、`WORKSHOPPASS_VIEW`、`WORKSHOPPASS_LOOK`。它们可以白名单中文化。自定义物品 NAME_/DESC_ 可能源于作者数据，不能按前缀批量翻译。

PEAK 2.6.a 的 `LocalizedText.OnLangugageChanged` 是 public static Action 字段（名称原样），不是 event。订阅/退订必须保留其他委托。GetText 存在 string/bool、string/Language 和绑定文本组件的重载，实施时明确签名与白名单。

BepInEx.ConfigDescription.Description 和 ConfigEntryBase.Description 都只有 getter。原配置已绑定后，应按原 ConfigFile 实例 + section/key 翻译其描述，备份并可恢复。只更新这个原 MOD；原 key、value、类型、AcceptableValues 和 Tags 不变。不采用全局 Bind 补丁或 ModConfig RefreshCache 重注册。

原 MOD 源码未发现 ModConfig 专用接入或 BepInDependency；它以普通 BepInEx 配置绑定为主。中文补丁若需要 ModConfig 配置名称本地化，应作为可选集成并按真实配置身份收窄，不能假定原 MOD 已提供显示委托。

## 7. 推荐方向与待确认事实

- 独立 `PeakWorkshopCN.dll`，通过原 GUID HardDependency 明确安装顺序；内部类型用 Harmony AccessTools 解析，避免复编译原 MOD。
- 精确词典 + 动态模板/调用点补丁 + 专属显示方法 + 两套字体适配；动态规则必须识别内置模板并原样保留变量。
- 缺失成员或未知词条保留原文、有限告警，不伪装为全覆盖；原 MOD 更新后重新核对签名及文本来源。
- 内置分类显示与 ID 分离；若中文影响搜索，增加显示侧匹配，不能改变原分类、资源名或写回数据。
- 未遍历并人工核对全部 474 文件；本轮是覆盖架构调研，不是已完成翻译表或完整安全审计。
- 待实施验证：原 DLL 与反编译方法签名逐项一致、实际中文字体、全部文本去重清单、缓存范围、ModConfig 可选接入、多人行为和界面布局。
