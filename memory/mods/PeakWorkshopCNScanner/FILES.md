# 文件与路径

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

14:56更新：下方“已部署”路径当前文件已消失；候选路径和哈希完整，用户已确认不重复制。实际状态见STATUS。

- 源码：`MOD开发/PeakWorkshopCNScanner/PeakWorkshopCNScanner/`，补丁在 `Patches/`。
- 构建/测试命令：`MOD开发/PeakWorkshopCNScanner/README.md`；使用说明与验证：同目录 `使用说明.md`、`VALIDATION.md`。
- 证据生成：`tools/Catalog`；原 DLL → `Data/SourceCatalog.json.gz`，包括 SHA256、17个入口和6,385原文证据。
- 离线测试：`tests/`；结果 `.build/peakworkshopcn-scanner/test-results.txt`，合成导出 `test-output/synthetic-session`。
- 独立构建：`dotnet build 'MOD开发/PeakWorkshopCNScanner/PeakWorkshopCNScanner/PeakWorkshopCNScanner.csproj' -c Release -p:OutputPath='C:/Users/Administrator/Desktop/MOD/PEAK/.build/peakworkshopcn-scanner/candidate/'`。
- 候选：`.build/peakworkshopcn-scanner/candidate/PeakWorkshopCNScanner.dll`；部署记录 `.build/peakworkshopcn-scanner/deployment.json`。
- 已部署：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCNScanner.dll`。未同步到工作区 `测试环境/`；此目录只作为引用来源。
- 实机报告：同 profile 的 `BepInEx/PeakWorkshopCNScanner/<session>/report.json`、`report.html`，`latest-session.txt`定位最近会话，首次成功运行才创建。
- 配置：`BepInEx/config/com.wuyachiyu.peakworkshopcnscanner.cfg`（首次加载生成）。
