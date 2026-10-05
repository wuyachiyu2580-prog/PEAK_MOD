# PeakWorkshopCN 当前摘要

日期：2026-10-05。版本 `0.1.0`。阶段：第一版试用源码、工程及 DLL 已实现、构建、部署；完整覆盖及 Unity 实机待验收。

## 最新恢复入口：容错修复版已部署

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
