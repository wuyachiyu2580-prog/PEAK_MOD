# PeakWorkshopCN 文件与路径

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

2026-10-07新增定位工具：`MOD开发/PeakWorkshopCNScanner/`，独立DLL/配置/报告；参见 `memory/mods/PeakWorkshopCNScanner/FILES.md`。试用说明 `MOD开发/PeakWorkshopCNScanner/使用说明.md`。扫描器本轮部署不更新此中文补丁DLL。

日期：2026-10-07。

## 统一候选产物

- 当前候选：`C:/Users/Administrator/Desktop/MOD/PEAK/.build/peakworkshopcn-unified/candidate/PeakWorkshopCN.dll`，2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`。
- 回退备份：`.build/peakworkshopcn-unified/backup/PeakWorkshopCN-2FC50B1FDBB6.dll`；旧版备份仍保留。
- 复建输出：`.build/peakworkshopcn-unified/rebuild4/PeakWorkshopCN.dll`，与候选哈希一致；主项目 0/0，测试 51/0。
- 指定部署目标：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`，已覆盖；原 `PeakWorkshop.dll` 从 `.old` 备份恢复。

## 当前文档

- TERMINOLOGY_PERFORMANCE_RESEARCH.md：最新官方术语证据、用户确认SHORE海岸、别名/语境边界、反射热点、实施顺序与实机验证标准。
- research/2026-10-05-terms-performance/：8个CSV/JSON，官方区域词表、来源指纹、同英文差异候选（42匹配/22不同，非22确定错误）、字体字段统计/明细、规则桶、12文件源码完整性、进度点提取0匹配结果。
- tools/Research/extract_terms.py、Research.csproj、Program.cs：独立只读研究工具，已运行，不参与生产插件构建。UnityPy1.25.4依赖和完整官方表位于 `.build/peakworkshopcn-research/`。

- 开发目录：`C:/Users/Administrator/Desktop/MOD/PEAK/MOD开发/PeakWorkshopCN/`。
- README.md：开发入口；RESEARCH.md：调研证据；PLAN.md：初始完整计划；IMPLEMENTATION.md：当前试用实现、动态来源、验证及遗漏。
- AUDIT_RECHECK.md：21时最新全量复核及R01–R15；audit/2026-10-05-recheck/：全量源码/IL调用、缺项、动态来源、直接显示候选、运行日志摘录、边界探针及输入指纹；tools/Recheck：只读审核生成器。
- memory：`C:/Users/Administrator/Desktop/MOD/PEAK/memory/mods/PeakWorkshopCN/`，六件套与temp/current.md。

## 参考与原DLL

- 游戏反编译：`C:/Users/Administrator/Desktop/MOD/PEAK/引用参考代码/反编译/2.6.a/`。
- 原MOD反编译：`C:/Users/Administrator/Desktop/MOD/PEAK/引用参考代码/反编译/BepInEx/plugins/PeakWorkshop/`。
- 原安装包：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakCode-PeakWorkshop/`。
- 原DLL：上述安装包下 `PeakWorkshop/PeakWorkshop.dll`，4,465,152字节，版本0.2.0.0。
- 原DLL SHA-256：`CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC`。

## 工程/输出

- 源码/项目：`MOD开发/PeakWorkshopCN/PeakWorkshopCN/PeakWorkshopCN.csproj`，net472，Plugin/TranslationCatalog/DynamicCatalog/LiteralRewriter/WorkshopPatches/FontPatches/ConfigurationLocalization。
- 输出：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`。
- 构建命令：`dotnet build "MOD开发/PeakWorkshopCN/PeakWorkshopCN/PeakWorkshopCN.csproj" -c Release`，主项目 0 warnings / 0 errors，直接部署上述 profile。
- 当前已部署产物：版本 0.1.0.0，304,128 字节，SHA-256 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`。
- `tests/Tests.csproj`：net472，41 项测试通过，执行 `tests/bin/Release/net472/Tests.exe`。
- 新增 CatalogData/RawLiteralRewriter、Data/Translations.Airport.json 与 Data/BuiltinRules.json；tools/Catalog 从 Roslyn 语法树生成模板与未解析表达式清单。
- `tools/Audit/Audit.csproj`：net10 + Cecil，只读原 DLL 并检查字体目标/动态来源/配置身份；报告 `.build/peakworkshopcn/literal-audit.json`。
- 未发行打包或实机验证。
- 当前安装：`.build/PeakWorkshopCN/PeakWorkshopCN.dll` 于2026-10-05 16:58:48 +08:00复制到profile，源/目标哈希一致、原DLL未变。21时新日志已确认初始化完成；本轮全量复核未更新DLL，遗留问题见AUDIT_RECHECK。

## 重新审核产物

- 开发区 `AUDIT.md`：2026-10-05 全量缺失审核，含页面/入口分类、Boss错误来源、覆盖计数及人工审核限制。
- `audit/2026-10-05/`：16 个生成CSV/JSON文件，源码/DLL调用各4,968位置；缺少固定映射3,122位置、非固定待审764位置，另有词表上下文复核候选。
- 重点清单：missing-fixed-source.csv、nonliteral-review-source.csv、source-method-summary.csv、dynamic-registration-review.csv；其余清单索引见AUDIT.md。
- 用户运行日志来自指定profile的BepInEx/LogOutput.log。21时复核取得重启后初始化完成记录及仍含英文的新截图；完整UI/资源目录验收尚未通过，证据摘录见最新recheck目录。

