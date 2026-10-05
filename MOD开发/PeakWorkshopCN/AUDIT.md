# PeakWorkshopCN 缺失项重新审核

日期：2026-10-05。对应已部署 PeakWorkshopCN 0.1.0，原 PeakWorkshop 0.2.0.0。

用户反馈绝大部分仍是英文，并明确要求审核截图之外的缺失项。本轮只审核和记录，没有改插件源码、翻译词表、项目、DLL 或配置，也没有构建部署。

## 结论

当前覆盖不足是全局性问题。现有词表只覆盖约四分之一的相邻固定文字调用位置；许多页面整组缺词。变量文本、直接语言分支、德英数据表、裸写显示文字及缓存也存在未覆盖入口。还有一条已编写的动态规则挂错了方法。

截图与日志证明插件已经正常加载，工坊 IMGUI 的部分汉字能够显示。日志没有出现本插件文字 hook 跳过或初始化失败；这不能证明所有页面、模板和开关均正常。

## 审核方法与计数

遍历全部 474 个反编译 C# 文件，用 Roslyn 语法节点枚举调用、德英字段、直接显示参数及语言分支；全部文件语法解析错误为 0。另用 Mono.Cecil 只读枚举原 DLL 的真实调用指令、所在类型/方法及 IL 位置，没有执行原程序集。

源码和 DLL 的 Loc.Tr 调用计数均为 4,968；固定/非固定两类计数和词表命中数也一致。旧调研中的 4,979 是文本搜索匹配数，本次 4,968 是实际调用节点/指令数，应使用本次口径。

| 范围 | 结果 | 含义 |
|---|---:|---|
| Loc.Tr 全部调用位置 | 4,968 | 不是独立词条数或界面数量 |
| 相邻德英固定字面量位置 | 4,204 | 当前固定调用重写器识别的形态 |
| 固定位置词表命中 | 1,081 | 约 25.7%，不代表实机逐项通过 |
| 固定位置未映射 | 3,122 | 需要逐项审查；含快捷键等应保留内容 |
| 机器语言代码位置 | 1 | Tr("de", "en")，必须保留 |
| 固定英文去重值 | 3,239 | 按原值区分大小写去重 |
| 未映射固定英文去重值 | 2,720 | 排除上述机器语言代码；不是 2,720 条都必须译 |
| 非固定调用 | 764 | 插值 353、其他表达式 330、变量/表达式 81 |
| 非固定调用所在方法有动态规则 | 15 | 仅证明进入候选来源，不能证明每句命中模板 |
| 非固定调用没有现有来源规则 | 749 | 需分清内置模板、外部内容、纯数值等 |
| De/En 后缀赋值候选 | 550 | 包括静态词表、动态拼接及作者/服务器内容 |
| De/En 后缀变量声明候选 | 45 | 与赋值清单独立，不是独立词条数 |
| 直接读取 Loc.En | 30 | 包括已处理分类、未处理显示、数字格式和作者内容 |
| 显式语言 bool 参数方法候选 | 12 | 包括 DescribeIn、CtLabel、CheckIn 等 |
| 未经 Tr 的直接显示候选 | 92 | 包括正文、品牌、富文本、空串、控件 ID，需人工分类 |
| 原配置绑定 | 69 | 现有 69 个名称/67 个非空说明映射；2 条原说明为空 |

运行日志为 1,083 fixed text sites / 51 dynamic call sites / 67 configuration descriptions。补丁计数器使用累加，不能当成唯一词条覆盖率；51 还包含转交动态助手后回退的固定文字。不能用这些计数代替本次逐调用清单。

## 各区域的固定缺失规模

以下按原 DLL 命名空间汇总，包括对应嵌套类型；列中的位置不是页面数量。非固定列不自动认定为缺翻译。

| 区域 | 固定位置 | 词表命中 | 未映射固定位置 | 非固定调用 |
|---|---:|---:|---:|---:|
| Editor 编辑器 | 1,950 | 404 | 1,546 | 347 |
| Airport 工坊/机场 | 1,912 | 577 | 1,335 | 314 |
| Build 构建/物品 | 112 | 28 | 84 | 21 |
| Teams 队伍 | 78 | 22 | 56 | 16 |
| Launch 起飞 | 58 | 39 | 19 | 11 |
| Net 联机 | 45 | 5 | 40 | 18 |
| Online 在线服务 | 25 | 2 | 22 | 15 |
| Generation 加载 | 14 | 2 | 12 | 11 |
| Npc NPC 界面 | 8 | 2 | 6 | 11 |
| Patches 其他入口 | 2 | 0 | 2 | 0 |

Online 剩余 1 个固定位置是机器语言代码；总计命中 1,081、未映射 3,122、机器代码 1。

单独看主类型：WorkshopPanel 有 1,692 个固定位置，命中 487、未映射 1,205；EditorUi 有 1,725 个固定位置，命中 375、未映射 1,350。

## 页面遗漏

本节的路径均相对 `引用参考代码/反编译/BepInEx/plugins/PeakWorkshop/`。完整逐项文本/表达式与行号见 CSV。

### 工坊与机场

| 区域 | 已确认遗漏 | 代表来源 |
|---|---|---|
| 导航与搜索 | Home、MY STUFF、3D ASSETS、Mine & downloaded、各类搜索提示 | WorkshopPanel.cs 的 DrawHeader、SubTabs、DrawSubHeader |
| 地图首页/列表/详情 | 创建地图、热门/新建/更新的说明、查看与下载、路线、内容清单、编辑/发布/单人/多人入口 | DrawMapHome、MapHero、DrawMapBrowse、DrawMapDetail、DrawRemoteDetail |
| 建图与路线向导 | 完整标题、三种地图类型及说明、空白地图提示、路线阶段、下一步、创建并进入编辑器 | WizardKind:28678、KindCards:29476、DrawWizard、WizardDetails、WizardSummary、PlanEditor、AscentEditor |
| 大厅查找/创建/我的大厅 | 查找/创建、空列表、筛选、区域、可见性、邀请码说明、准备/开始、玩家规则 | DrawLobbyBrowse:3320、DrawLobbyFilters:3388、DrawLobbyRules、LobbySettingsEditor、DrawLobbyHostBox |
| 素材与上传 | 首页副标题、详情与来源、纹理/比例、动画选择、导入验证、发布和订阅结果 | AssetHero、DrawAssetDetail、OriginForm、DrawTexturePanel、ContentPublishBox |
| 物品制作与详情 | 整段新手说明、模型/效果/使用设置、伤害/击退、近战/远程/投掷、持握、音效、弹丸/特效 | ItemIntro、EditorViewerBlock、GripPanel、RangedSettings、MeleeSettings、ItemStats |
| NPC 制作与详情 | 整段创建说明、类别名称及说明、属性、动作、掉落、对话编辑、使用方式 | NpcIntro:18363、NpcCatHint:18580、WorkshopNpcs.Display:220、NeStepValues、NeStepTalk、DrawNpcDetail |
| 我的内容/回收站/来源 | 我的素材与物品、订阅说明、删除恢复确认、版权/许可证说明 | DrawMine、DrawTrash、WorkshopTrash、WorkshopDelete、DrawSources、AssetOrigin |
| 报告/更新/在线状态 | 错误外层、更新内容、在线开关/超时/请求状态、评分、举报、审核表单 | NewsPopup、OnlineStatusLine、OnlineRatingBox、DrawNewsForm、DrawModeration、PmfApi、TransferProgress |

部分高遗漏方法：DrawLobbyRules 缺 35 个固定位置；DrawMapDetail 31；GripPanel 24；ContentPublishBox 22；DrawRemoteDetail 21；OriginForm 和 EditorViewerBlock 各 20。不能仅补截图首屏后声称这些流程已完成。

### 编辑器与游玩

| 区域 | 已确认遗漏 | 代表方法与缺少固定位置数 |
|---|---|---|
| 帮助/操作说明 | 相机、视图、选择、地形绘制、拖动、层级等整段说明；快捷键名称需另行分类 | HpContent:5463，116 |
| 地形与散布 | 地形塑造、放置条件、坡度/净空、预设与尺寸描述 | DrawTerrain:5604，43；ScRules:18280，30 |
| NPC 属性 | 类型、朝向、生成设置及属性说明 | DrawNpcProp:11441，36 |
| 物品效果 | 添加/删除效果、效果强度与条件 | InEffects:7054，35 |
| 交互逻辑 | 按 E 的内置长说明、标记、计数、给予/取走物品等 | DrawInteract:7502，30 |
| 液体/流程/传送 | 水与岩浆的整段行为描述、流程条件、传送选项 | DrawLiquid:7733，29；DrawFlow:7947，27；DrawTeleport:6766，25 |
| 危险/天空/外观 | 危险类型/提示、天空、外观属性 | DrawHazard:6609，24；DrawSky:18874，22；DrawLook:9499，17 |
| 地形计划/运动 | 区段计划、运动时钟/路径/摆动及参数摘要 | DrawBiomePlan:9032，18；DrawMotion:9916，18 |
| 队伍/玩法 | 地图或房主决定规则、队伍、胜利条件、时间/阶段 | WoTeams:23405，18；WoStages:19203，17；WoTeamRules、WoTeamWin |
| 音乐/音效 | 音乐选择、触发器和声音预设名称 | MusicPicker:10838，16；WoMusic:10641，14；ItemSounds.Preset.Label |
| 场景树/放置工具 | 数量、层级名的外围标签、选区状态、绘制与工具错误 | EditorTools 缺 87 固定位置及 61 非固定调用；EditorSession 缺 35 固定位置及 21 非固定调用 |
| 山上状态 | Boss 狂暴/击败、波次、剩余数量、队伍进度和局末提示 | NpcHud.DrawBosses/DrawWaves、TeamHud、TeamPanel |

## 接入层缺陷

1. **Boss 击败规则挂错来源。** 当前 DynamicCatalog 配置 `PeakMapForge.Npc.NpcHud.OnGUI`，该方法没有 Loc.Tr 调用；实际句子位于 `NpcHud.DrawBosses:91`。因此之前“已补 Boss 击败提示”的交付表述不成立。测试只验证了给定来源字符串和模板的输出，未验证真实调用者。其余 7 个规则来源确有调用，但也需逐模板验证。
2. **变量词表无法被相邻字面量重写器覆盖。** `ScatterPreset.Label/Hint/Short` 传入字段。ScatterPresets 中已有 Empty、Rocks、Big boulders 及长说明等内置词表，均需单独接入；词典中恰好有对应英文也不能令变量调用自动翻译。
3. **直接语言选择遗漏。** `ItemSounds.Preset.Label:29`、`ForgeVfx.Element.Name/Desc:32/44`、`AssetOrigin.LicenseInfo.Label/Hint:35/47`、EditorUi.WoMusic/OvGameMusic 均从字段直接选择德英。当前五个分类显示后缀并不覆盖这些入口。
4. **运动/放置等辅助方法绕过 Tr。** ForgeMotion.DescribeIn:270 直接拼接旋转速度、摆幅、周期、路径点数、击退和时钟说明；EditorUi.DrawMotion:9916 显示其返回值。PlacementSetup.CtLabel/CheckIn、ScatterPresets.CtBiome、HazardCatalog.Display 等也需显示层适配。部分输出同时用于报告，不能全局改存储/工具字段。
5. **加载和加入的阶段文字来自字段。** LoadOverlay.StageText:1289、LobbyOverlay.Body:196、JoinFlow.ConfirmText:89 等传入阶段/确认变量；当前对固定参数模式的支持不覆盖这些内部生产链。
6. **直接硬编码显示没有入口补丁。** WorkshopPanel.LobbyPlayerRow:12321 的 `"Kick"` 按钮，以及 NpcCategoryTiles:18633 的 `"1 NPC"` / `"{num4} NPCs"` 未经过 Tr。裸写的名称、品牌、控件 ID 则不能一概替换。
7. **泛型方法会被现有扫描跳过。** `WorkshopPanel.Strip<T>` 的 Show all 是泛型方法中的调用；即使补上词表，当前 ContainsGenericParameters 检查仍会跳过它。审核发现原 DLL 的 Tr 调用中有 1 个这样的泛型位置，当前词表也未包含此项。
8. **缓存失效不完整。** MarkerRef.Label:102 只按 bool En 缓存；当前 Plugin.Refresh 只通知 Loc.Bump，没有显式失效这个缓存。中文开关与原 En 值不变时，可能继续保留旧标签，需实机验证。
9. **现有词表不区分上下文。** 查表仅使用英文值且忽略大小写，没有校验德英对或用途。62 个已映射英文分组具有多个德文变体，很多只是大小写/同义词，但也包含不同用途：WorkshopPanel.LobbyModForm:12387 的 Remove 表示踢出玩家，当前统一为“移除”；EditorUi.WoTeams:23449/23457 的 Teams 用作队伍数量设置。需要单独审查译文适用性，词表命中不等于翻译准确。

原 DLL 没有在静态构造函数中直接调用 Loc.Tr 的位置。启动时风险主要来自已经初始化的德英数据表、已缓存标签以及补丁安装顺序，不能把它们笼统称作“漏掉的静态 Tr 调用”。

## 动态文字待审

749 个无现有来源规则的非固定调用并不都是作者正文。已经确认的内置模板遗漏包括：

- 大厅数量、刷新时间、列表过滤结果：LobbyBrowser.StatusText、WorkshopPanel.DrawLobbyBrowse。
- 地图/素材/物品/NPC 总数、作者前缀、订阅与下载结果：WorkshopPanel.DrawSubHeader、AssetHero、MapHero、SubscribeButton、DrawRemoteDetail。
- 在线服务器条目数量：WorkshopPanel.OnlineStatusLine:19506。
- NPC 生命/伤害、援军数量、怒气阈值、对话段落和掉落列表：WorkshopNpcs.DescribeTypes:1171、WorkshopPanel.NpcTraits。
- 物品回复体力、异常持续时间、伤害/射速/数量等摘要：WorkshopItems.VanillaSummary/AfflictionName、ItemDef、ItemStats。
- 地形尺寸、坡度、百分比、预估时间、放置校验、路径/运动/状态说明：EditorUi、EditorTools、ForgeMotion、LoadOverlay、TransferProgress。
- 缺模型/物品、来源冲突、导入失败、下载失败等外围错误：WorkshopAssets.Publish、WorkshopNpcs.Assemble、WorkshopLibrary、JoinFlow。

应保留作者/玩家姓名、地图名称/正文、NPC 对话、聊天、路径和 ID 等参数；翻译内置句子外围。不对整屏做词典替换，也不按长度排除长说明。

## 保留与待确认

机器语言代码、RPC/Bridge/序列化字段、资源 ID、技术单位、品牌 PEAK WORKSHOP、真实按键名以及作者正文，需要按用途保留。快捷键说明中的 Wheel、Click、Drag 等自然语言则应翻译；同样的 Home 在按键帮助中和导航首页中用途不同，不能无条件共用一条译文。

30 处直接 Loc.En 读取中，5 处属于已安装分类显示入口，2 处控制数字格式，5 处控制作者/NPC 内容选择，其余 18 处需继续核对显示或报告调用链。不能全局修改 En/Code，原语言按钮仍显示 EN/DE 属于原回退语言设置；其显示与中文补丁状态的区分也需要评估。

配置映射当前看不到已确认的非空说明缺项；日志确实登记 67 条说明。ModConfig 菜单中的显示名、分区/选项及动态开关后的现有控件仍未完整实测，不应以静态映射存在宣布整页通过。

92 个直接显示候选以及 550/45 个字段/声明候选包含非 UI 和无需翻译内容，不能加到 3,122 或 764 上当成“总缺失词条数”。未对每个候选完成人工语义判定，也未重建所有数据流，因此本报告是全量静态清单及重点人工复核，不是全部 GUI 文本已经穷尽或实机验收完成。

## 后续修订与验收建议

先修正规则来源及泛型/变量/直接显示接入，并建立按页面和方法管理的词条清单。再依次覆盖工坊导航和全部操作流程、编辑器各属性页及帮助、NPC/物品/特效/音乐词表、加载/联机/状态/错误描述。

后续测试应从真实 DLL 的方法/调用位置验证规则可达性，枚举同页遗漏，并验证所有参数保留及作者内容隔离。页面验收使用实际屏幕证据；不得把模板单元测试或累加 hook 数当作可见文字完成率。本轮不实施上述修订。

## 完整清单

目录：`audit/2026-10-05/`。

| 文件 | 内容 |
|---|---|
| summary.json | 全量计数、原 DLL hash、主要模块和错误来源摘要 |
| tr-source-calls.csv | 4,968 个源码调用的路径、行号、方法、德英参数/表达式及状态 |
| tr-il-calls.csv | 4,968 个原 DLL 调用的方法签名、IL 位置、字面量/边界/泛型情况 |
| missing-fixed-source.csv | 3,122 个未映射固定位置，按文件和行号排列 |
| nonliteral-review-source.csv | 764 个非固定调用，包括目前 15 个候选已接入位置 |
| source-method-summary.csv | 790 个含调用的源码方法/属性的缺失汇总 |
| module-summary.csv | 各原 DLL 类型的调用与映射汇总 |
| bilingual-fields.csv / bilingual-declarations.csv | De/En 字段赋值及变量声明候选 |
| language-branches.csv / language-parameter-methods.csv | 直接语言选择与显式语言参数入口 |
| direct-display-candidates.csv | 92 个不经过 Tr 的直接显示候选 |
| dynamic-registration-review.csv | 8 个配置来源的真实调用检查，明确 Boss 来源错误 |
| dictionary-context-review.csv | 62 个具有多个德文变体的已映射英文分组；候选不等同于 62 个错译 |
| configuration-bindings.csv | 69 个配置身份及描述来源 |
| source-parse-errors.csv | 本轮无语法错误，空清单 |

原 DLL SHA-256 仍为 `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`；中文 DLL 仍为 `E2C3AD28DF7995C5F1EC1EC72CC2954AA0570F70FF8C3CD62A39AE15ED70F092`。没有产生新 DLL。
