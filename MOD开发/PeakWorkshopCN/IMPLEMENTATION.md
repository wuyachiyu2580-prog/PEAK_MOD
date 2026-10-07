# PeakWorkshopCN 0.1.0 试用记录

## 2026-10-07 资源显示修订

用户实机复核发现编辑器构件网格仍显示 `CliffPillar`、`Basalt Cluster` 等原键，材质下拉仍显示 `BlueRock`、`DesertRock`、`Rock Volcano` 等原名。原因是构件的 `InPrimLabel` 对未知键回退原文，材质目录和 `RockLook.Label` 绕过原有固定文本翻译。

`ResourcePatches` 现在在显示边界加入官方构件/材质映射，覆盖列表、网格、属性、散布岩石外观和中文/原名搜索；显示转换不改 `PrefabCatalog`、`MaterialCatalog`、地图保存或拖拽 payload 中的键。作者 `model:`、`asset:` 资源保持原名，并通过运行时资源分类排除自定义项。

本次 Release 构建 0 警告、0 错误，51 项测试通过。部署 DLL：`PeakWorkshopCN.dll`，2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`。原 PeakWorkshop DLL 曾被改名为 `.old`，已恢复为 `PeakWorkshop.dll`，原哈希 `CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC` 未变。下一步必须重启后确认两个插件均加载，再做 Unity 页面和卡死复核。

日期：2026-10-05。用户在调研/计划后授权“先执行，我看看效果”，本记录为当前实现状态。

## 2026-10-06 统一修复候选

已按统一计划完成一轮源码、词表和离线验证：全量复核清单中的 2,197 个固定缺映射位置、764 个非固定调用及直接显示入口已重新分类；生成 4,246 条中文嵌入译文，加入官方术语白名单（海岸、雨林、森蕈、雪山、方山、火山、熔炉、雾沼、城塞、顶峰、天底、营火）。构件/材质显示层和中英文搜索已接入，`Desert_Rock_1` 显示为“方山岩石 1”，`M_Rock` 显示为“岩石材质”；作者 `model:`、`asset:` 键保持原样。

已修正字段 getter 绑定、真实动态回调来源、跳转标签边界、泛型 Strip、模板参数保护、Home 语境、MarkerRef 代次缓存、字体创建/重建路径和配置访问器，并加入启动绑定与目录增量索引。新增 `ResourcePatches.cs`、`OfficialTerms.cs`、`BoundAccess.cs` 及统一数据文件。候选 DLL 已独立构建：0 警告、0 错误；45 项离线测试通过。候选路径为 `.build/peakworkshopcn-unified/candidate/PeakWorkshopCN.dll`，2,009,088 字节，SHA-256 `C69B494AD6612A05570F975672B6455C4E51EDCB70B57B8205DEE0019C9E87AE`。

PEAK 进程当前仍在运行（PID 23532），因此候选尚未覆盖指定 profile；旧版备份在 `.build/peakworkshopcn-unified/backup/PeakWorkshopCN-26D877.dll`。上述源码和离线结果不能代替 Unity 页面、联机和性能实测，资源未知项也要在实机目录增量检查后才能宣称全覆盖。

**最新术语/性能研究：** 见 [TERMINOLOGY_PERFORMANCE_RESEARCH.md](TERMINOLOGY_PERFORMANCE_RESEARCH.md)。官方表已提取，SHORE采用海岸获用户确认；字体高频遍历和字段/MarkerRef反射问题已定位，方案未实施。本轮源码/词表/DLL未变，未测实机FPS；下方旧实现不代表已完成本轮修复或优化。

**21时全量复核更新：** 新日志188已出现初始化完成信息。当前仍有2,197个固定缺映射位置，且已确认字段错配、Marker缓存、回调真实来源与跳转边界等问题；构件/材质显示名称尚未接入。完整证据与修复要求见 [AUDIT_RECHECK.md](AUDIT_RECHECK.md)，以下“已实现”仅表示已有代码，不代表正确性或完整验收。本轮未改插件、词表或DLL，安装版仍为下方26D877...836CC。

第一轮补齐后曾出现整页英文，旧日志缺少初始化完成信息；未取得完整致命异常栈，不能确定旧中断位置。2026-10-05 16:58:48 +08:00 已部署逐入口容错修复版。最新日志确认初始化完成，但仍有124条扫描跳过警告及1条Strip显式补丁失败，不能认为泛型入口已修复。

## 已实现

- 独立 net472 BepInEx/Harmony DLL，硬依赖 PeakWorkshop 0.2.0，默认中文、独立开关。版本不匹配则停止启用中文。
- 固定文本只改原 DLL 中相邻德英字面量的 Loc.Tr 调用，保留原调用以维持语言/数字格式行为；变量形式的作者内容不会因为恰好名为 Save 而被字典翻译。
- 词表目前包含原有固定词条及 742 条内置文字；原 DLL 中静态可命中的固定调用位置为 2,006/4,204。另有 1,000 条生成规则（包含固定文字与动态模板），不能等同于 1,000 条动态模板；这些数字是静态审计，不是 Unity 运行时成功补丁数。
- 手写动态规则限定到8个来源方法、12个模板，德英捕获参数须一致；生成库存另有907条固定规则和93条动态模板，其中6条动态规则未设捕获相等检查。生成模板还存在插入参数后处理双大括号的问题，详见最新复核；不能把手写规则的保证推广到所有生成规则。
- 五个分类显示入口和四个通行证 key；保持内部 ID 与地图、NPC、聊天等作者内容。
- 69 个原配置身份有中文显示名，67 条非空说明有译文；MinZ/MaxZ 原描述为空，继续为空。通过目标 ConfigDescription 实例的 getter 补丁显示中文，不改 Value、Tags、范围或原 cfg 注释；可选 ModConfig 名称适配按 ConfigFile + section/key 限定。
- IMGUI 使用经汉字样本检查的系统动态 Font；TMP 使用游戏中文字体及 fallback。记录原字体/材质，加载界面的字形过滤字体与显示字体同步；开关刷新已有 TMP 组件，卸载时销毁本插件创建的 IMGUI 字体。

## 长动态文字的依据

长度不是省略翻译的条件。动态文字需要保留变量，因此单独建立内置模板规则。以下已经补充；文件均相对于 `引用参考代码/反编译/BepInEx/plugins/PeakWorkshop/`。

| 来源与行号 | 内置文字内容 | 原样保留参数 |
|---|---|---|
| PeakMapForge.Launch/LaunchPanel.cs:607 | 本机已有地图，实际构建房主地图；或缺少地图、起飞将退出并需向房主获取 .peakmap | 地图名、修订号 |
| PeakMapForge.Launch/LaunchPanel.cs:653 | 未准备好 MOD 或地图的玩家名单，缺地图和缺 MOD 的不同结果 | 玩家名单 |
| PeakMapForge.Launch/LaunchPanel.cs:528、DrawTeamsLine、DrawPlayers | 地图文件夹计数、就绪人数、队伍数、持有地图人数 | 数量 |
| PeakMapForge.Teams/TeamHud.cs:463、468 | 队伍或玩家获胜 | 原名称 |
| PeakMapForge.Npc/NpcHud.cs:91 | Boss 被击败：已改接真实 DrawBosses 来源 | 原名称 |
| PeakMapForge.Editor/EditorUi.cs:13651、14977、18328 | 地形宽深高、长度及高度、净空要求 | 含德式小数逗号的原数值 |

姓名、路径或数值从变量进入内置句子时，翻译句子而保留参数。玩家自己填写的完整地图介绍、NPC 对话等则保持原文，因为它们属于作者内容。尚未覆盖的动态来源需继续逐项核对，不能以“长描述”笼统排除。

## 验证与产物

- 修复版主项目 Release：0 warnings / 0 errors，独立输出到 `.build/PeakWorkshopCN/`；已在游戏退出后复制到指定 profile。
- 测试：41 passed / 0 failed，包括实际 Harmony 固定调用、动态调用、配置 getter 补丁、扩展词表和 Boss 模板，关闭回退、参数不一致回退、作者词条碰撞隔离和配置值/身份保留。
- 最新 Mono.Cecil 只读审计：原版本 0.2.0.0；12 个字体 hook 签名、69 个配置身份符合预期；71 个动态来源方法存在，含 687 个原 Tr 调用（包含固定或回退分支，不能当作已翻译动态模板数）。
- 复核静态审计报告：`.build/audit-review.json`；基线缺失清单仍见 `AUDIT.md` 与 `audit/2026-10-05/`。41 项测试未覆盖 Unity 中 WorkshopPatches 的实际初始化。
- 当前已安装 DLL：版本 `0.1.0.0`，304,128 字节，SHA-256 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`。
- 2026-10-05 16:58:48 +08:00，已将 `.build/PeakWorkshopCN/PeakWorkshopCN.dll` 复制到 `C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`，源与目标哈希一致。
- 原 PeakWorkshop DLL SHA-256：`CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`，与实施前一致。

审计命令：`dotnet run --project MOD开发/PeakWorkshopCN/tools/Audit/Audit.csproj -- <原PeakWorkshop.dll> .build/peakworkshopcn/literal-audit.json --verify <PeakWorkshopCN.dll>`。

测试命令：`dotnet build MOD开发/PeakWorkshopCN/tests/Tests.csproj -c Release`，随后运行 `tests/bin/Release/net472/Tests.exe`。

## 待验证与遗漏

未启动游戏，未验证真实 UI 字形、布局、开关多轮切换、ModConfig 可见菜单或联机流程。字体字形检查在运行时执行，离线签名检查不证明汉字已经渲染成功。

仍有大量未映射固定词条、直接 Loc.En 分支和 108 条表达式待审；本轮已接入 MarkerRef.Label 缓存失效、ScatterPreset/音效/特效/许可证字段及指定裸写入口，但音乐和运动/放置辅助方法仍需逐项实测。禁用/启用后的现有界面文字刷新及样式恢复需要实机检查。

没有生成发行包、ZIP 或上传内容。重启 profile 后优先检查工坊主菜单、地图详情、编辑器属性长说明、加载状态和多人房间提示。

