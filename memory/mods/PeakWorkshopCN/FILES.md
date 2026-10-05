# PeakWorkshopCN 文件与路径

日期：2026-10-05。

## 当前文档

- 开发目录：`C:/Users/Administrator/Desktop/MOD/PEAK/MOD开发/PeakWorkshopCN/`。
- README.md：开发入口；RESEARCH.md：调研证据；PLAN.md：初始完整计划；IMPLEMENTATION.md：当前试用实现、动态来源、验证及遗漏。
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
- 当前复核修复版：`.build/PeakWorkshopCN/PeakWorkshopCN.dll`；2026-10-05 16:58:48 +08:00 确认游戏退出后已复制到 profile，源/目标哈希一致、原 DLL 哈希未变；下一步以重启后的日志确认初始化完成并检查多人页。

## 重新审核产物

- 开发区 `AUDIT.md`：2026-10-05 全量缺失审核，含页面/入口分类、Boss错误来源、覆盖计数及人工审核限制。
- `audit/2026-10-05/`：16 个生成CSV/JSON文件，源码/DLL调用各4,968位置；缺少固定映射3,122位置、非固定待审764位置，另有词表上下文复核候选。
- 重点清单：missing-fixed-source.csv、nonliteral-review-source.csv、source-method-summary.csv、dynamic-registration-review.csv；其余清单索引见AUDIT.md。
- 用户运行日志来自指定 profile 的 BepInEx/LogOutput.log；早期截图只证明初始工坊页已有部分中文。最新容错修复版已部署，尚无重启后的实机结果。
