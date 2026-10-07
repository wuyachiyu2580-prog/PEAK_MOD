# PeakWorkshopCN 近期记录

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

## 2026-10-07

- 14:50：用户改用小扫描MOD定位漏译，新增独立PeakWorkshopCNScanner 0.1.0，50项离线检查通过后部署。现有CN DLL未改；原MOD检查仅有.dll.old，本轮保留禁用状态。须启用后采集实际界面报告，Unity/性能尚未验证，详见扫描器独立memory。

- 用户实机确认基础页面已中文化，但编辑器构件网格仍显示 `CliffPillar`、`Basalt Cluster` 等原名，材质下拉仍显示 `BlueRock`、`DesertRock`、`Rock Volcano` 等原名。复核调用链确认 `InPrimLabel` 的未知资源回退和 `MaterialCatalog.Names` 的直接列表显示是两个独立缺口。
- `ResourcePatches` 新增官方构件/材质显示名、岩石外观 `RockLook.Label` 后缀及中文/原名搜索所需的显示转换；内部资源键和作者 `model:`/`asset:` 内容保持原样。
- 主项目 Release 构建 0 警告/0 错误，测试从45项增至51项并全部通过。当前 DLL 2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`，已复制到指定 profile，并保留 `.build/peakworkshopcn-unified/rebuild4/` 与 `backup/PeakWorkshopCN-2FC50B1FDBB6.dll`。
- 启动前检查发现原 PeakWorkshop.dll 被改名为 `.old`，导致 BepInEx 报 `com.peakcode.peakworkshop` 缺失；已从原 DLL 哈希 `CC1ECDE5…8ADC` 的备份恢复。下一步必须重启并确认两个插件的加载日志，再复核卡死和资源名称。

## 2026-10-06

- 按统一计划完成候选实现：全量缺项、官方术语、构件/材质显示与中英文搜索、字段/回调/泛型/模板/MarkerRef/字体/配置路径已合并；生成 4,246 条嵌入译文。
- 候选独立构建 0 警告、0 错误，45 项离线测试通过。候选 DLL 2,009,088 字节，SHA-256 `C69B494AD6612A05570F975672B6455C4E51EDCB70B57B8205DEE0019C9E87AE`；复建输出哈希一致。
- 旧版 DLL 已备份到 `.build/peakworkshopcn-unified/backup/PeakWorkshopCN-26D877.dll`。PEAK PID 23532 仍运行，尚未覆盖指定 profile；实机页面、联机、未知资源和三组性能采样待游戏退出/重启后完成。

## 2026-10-05

- 接续调研官方术语与反射性能：从游戏UnityFS提取866键SerializedTermsData，确认方山/森蕈/雨林/雪山/火山/熔炉/雾沼/城塞/顶峰/天底；SHORE实际海岸，用户看过证据后确认采用。22个同英文差异候选按语境审核，不能全局覆盖。Cecil确认字体hook每次遍历EditorUi314字段/相关59、WorkshopPanel296/相关4、NpcHud相关0；梳理字段getter/MarkerRef/配置/动态规则与启动扫描生命周期。新增TERMINOLOGY_PERFORMANCE_RESEARCH.md、独立tools/Research和8份明细，同步入口/六件套/temp/索引。12源码哈希及原/CN DLL均未变；未实施优化、未测实机FPS，无外部update_memory工具，本地MD已落盘。

- 21时用户要求重新严谨复核全部MOD并强调构件/材质名。新增AUDIT_RECHECK.md、tools/Recheck与audit/2026-10-05-recheck/：474文件0解析错误，源码/IL均4,968Tr；固定2,006有映射/2,197无映射/1保留，非固定764。确认初始化完成但Strip仍失败；发现字段错配、回调来源、跳转边界、缓存、参数大括号及Home键错译等R01–R15。资源名称显示/中文搜索缺口和运行目录采集要求已记录。本轮未改插件/数据/DLL/config/地图，memory与开发入口同步。

- 用户确认“已退出”后检查无 PEAK 进程，于 16:58:48 +08:00 将既有容错修复版复制到指定 profile。版本 0.1.0.0、304,128 字节，源/目标 SHA256 均为 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`，原 DLL hash 未变。修复版构建 0/0、已有 41 项测试通过；本轮没有改码或重复构建，尚待重启实测。
- 复核证据修正：旧日志有 124 条文字入口跳过警告且没有初始化完成记录；缺少完整致命栈，不能断言具体失败位置。逐入口容错已实现，但不代表泛型 Show all 或各属性显示已正确。

- 用户重新审核后说“开始”，实施第一轮补齐并部署 0.1.0：新增 742 条词表及源码生成模板，修正 Boss 来源、变量字段、泛型/裸写显示和 MarkerRef 缓存；固定命中 2,006/4,204，41 项测试通过，主项目 0/0。新 DLL 303,616 字节、hash A92CD62B...97DC6，原DLL未变；完整覆盖和实机复测待完成。

- 用户提供五张实机截图，确认大片英文；随后明确“除了截图，其他地方也缺，先重新审核”。本轮停止补码，仅重新审核并写报告。
- Roslyn 全474文件解析无错误，实际Tr调用4,968；原DLL Cecil同为4,968。固定4,204位置：1,081命中、3,122未映射、1机器语言代码；非固定764位置中749没有现有来源规则。
- 新审计覆盖 De/En 字段、直接语言分支、语言参数方法、裸写显示及词表上下文候选，生成16个CSV/JSON明细，汇总见开发区 AUDIT.md。确认Boss模板来源误挂OnGUI，实际DrawBosses未接入；泛型Strip的Show all及多个变量表/直接显示链未覆盖。
- 本轮源码/词表/工程/DLL/config没有变化；中文DLL hash仍E2C3AD28...70F092，原DLL hash仍CC1ECDE5...8ADC。日志无本插件初始化失败/文字hook跳过；实机只证明加载和部分IMGUI汉字，不代表完整覆盖。

- 用户调研后授权“先执行，我看看效果”；已建立 net472 独立补丁并部署 0.1.0。原 DLL hash 未变。
- 固定文字仅重写明确字面量调用点，分类/通行证白名单补丁；动态补充地图持有/缺失/玩家名单、计数、胜利、Boss、尺寸与净空说明，参数原样保留。
- 配置按目标实例和 section/key 显示中文：69 个名称、67 条非空说明；可选 ModConfig 失败不影响主界面。两套字体备份恢复，加载字形检查与 TMP 显示同步。
- 39 项测试通过；主项目 0/0；Cecil 确认 12 个字体 hook 签名、8 个动态来源、69 个配置身份。固定词表匹配 518 种文字及 1,081/4,204 位置，仍为部分覆盖。
- 最新产物 hash/大小见 FILES；完整记录见开发区 IMPLEMENTATION.md。未启动游戏、未打包上传，真实视觉/缓存/联机待验收。

## 2026-10-04

- 用户指定新 MOD PeakWorkshopCN 0.1.0，补丁中文化，先调研再计划，不写代码；后续确认界面按钮/提示/标签/配置说明全包含。
- 只读核验原程序集 PeakWorkshop 0.2.0.0、插件GUID和安装包；实际语言为英文/德文，内部命名空间 PeakMapForge。
- 阅读 Loc、EditorUi、WorkshopPanel、TeamUi、游戏2.6.a本地化/字体链、配置及多类文本来源，记录规模、数字/Bridge边界和启动缓存风险。
- 建立开发区 README/RESEARCH/PLAN 和本项目独立memory；未改原DLL和配置，未创建源码、工程、产物或运行测试。

原 DLL证据和后续路径见 FILES。研究结论不代表Unity实机已验证。


