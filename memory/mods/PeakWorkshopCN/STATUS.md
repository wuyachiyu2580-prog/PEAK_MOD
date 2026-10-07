# PeakWorkshopCN 当前状态

**2026-10-07 用户最终确认**：profile中的scanner和CN DLL是用户为暂时游玩其他内容而自行移走，明确要求不要重新复制。保留当前停用/移走状态；扫描器源码、使用说明和候选DLL保存在开发区，50项离线检查通过，实机尚未验证。此前部署记录仅为历史。

**2026-10-07 14:50 更新**：用户改为先做小扫描 MOD 标注漏译，已新增独立 `PeakWorkshopCNScanner 0.1.0` 并部署，50项离线检查通过、实机未验证。见兄弟目录 `PeakWorkshopCNScanner/STATUS.md` 和开发区使用说明。本轮未改中文补丁。当前原MOD只有 `.dll.old`（禁用），未启用；下方“原 DLL 已恢复”等属于上轮历史，不代表当前状态。

日期：2026-10-07。资源显示补丁已继续修正，原 PeakWorkshop DLL 已恢复，等待用户重启进行 Unity 实机复核。下方历史段保留为依据。

当前 DLL：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/PeakWorkshopCN.dll`，2,013,184 字节，SHA-256 `2FC50B1FDBB6AC7F82A9D3EAAA7E70DADCF7EBA8B797031FAEEA1A77033AA523`。51 项离线测试通过，独立构建 0 警告、0 错误。新增构件键和材质名显示映射，并接入 `RockLook.Label`；保存键、作者内容和原 DLL 保持不变。

本次实机前置检查发现 `PeakWorkshop.dll` 曾被改名为 `.old`，导致 BepInEx 报缺少 `com.peakcode.peakworkshop`；已从哈希 `CC1ECDE5…8ADC` 的备份恢复原文件名。重启后必须先确认日志同时出现 `Peak Workshop 0.2.0` 与 `PeakWorkshopCN 0.1.0`，再检查资源列表和材质下拉。

本次 DLL 已保存于 `.build/peakworkshopcn-unified/rebuild4/PeakWorkshopCN.dll` 和 `candidate/PeakWorkshopCN.dll`，并在 `backup/PeakWorkshopCN-2FC50B1FDBB6.dll` 保留回退副本。资源显示、中文搜索、所有页面、语言开关、联机、未知资源和性能仍需实机证据，不能把离线结果当作验收。

已从当前游戏安装包提取866键的官方表：SHORE=海岸（用户看到证据后已确认采用）、TROPICS=雨林、ROOTS=森蕈、ALPINE=雪山、MESA=方山、CALDERA=火山、THE KILN=熔炉、GLOOM=雾沼、THE CITADEL=城塞、PEAK=顶峰、AREA_VOID/NADIR=天底。当前根林/高山/荒漠/热带/虚空及配置说明存在冲突；Campfire对应游戏营火。禁止把官方英文同词直接全局覆盖（PLAY演奏≠游玩，LIGHT点燃≠光源）。Swamp→GLOOM的实际场景对应仍待确认。

性能已定位：字体hook每次遍历EditorUi 314字段/相关59、WorkshopPanel 296/相关4，原初始化早退不阻止postfix；NpcHud字段hook相关0。FieldPostfix重复找字段且字段错配；MarkerRef每次反射强制失效且无中文开关快退。建议启动绑定访问器、创建/重建/语言代次刷新、有限资源索引；程序集扫描属于启动，不能当逐帧成本。优化尚未实施。

此前用户要求严谨复核全MOD，明确“不只是图片里的”，并指出构件 Desert_Rock_1、材质 M_Rock 等名称未翻译；这一完整范围继续保留。

审核未通过完整中文化标准。新日志188确认初始化完成，仍有124条扫描跳过警告、1条Strip显式补丁失败及2条本补丁ModConfig声明警告。不能再把全部英文归为初始化失败。

已重新解析全部474文件（0错误），源码/原DLL均4,968个直接Tr调用：4,204固定位置中2,006有映射、2,197无映射、1语言代码保留；无映射去重英文2,042。非固定764中实际IL来源登记127，其余637无对应来源规则；127也不代表全部命中。源码外层登记148中有21处回调需改接真实方法。生成1,000条规则实为907固定+93动态。

主要缺陷：构件/材质显示入口未接入且搜索只认原名；FieldPostfix按错字段；回调来源错配；LoadOverlay.StageText跳转标签被重写器跳过；MarkerRef把Loc.En属性当字段读取；泛型Strip失败；生成模板会修改参数双大括号；Home键被译为首页；30处本地tr委托调用绕过扫描。详见开发区 AUDIT_RECHECK.md R01–R15 与 audit/2026-10-05-recheck/。

当前安装仍为16:58部署的0.1.0.0、304,128字节，SHA256 `26D877A2BFEDBEE5F6BE7D3BD5772B6DEB6990B74311D3BC854F767EBFA836CC`；原DLL哈希未变。之前构建0/0、41项测试不能覆盖WorkshopPatches/字体/初始化的实机正确性，本轮未重复构建插件。

资源目录为运行时收集：日志本局467个prefab、286个材质是单次重建计数，不是全游戏总量或未翻译数。完整资源映射需实际键/来源清单；中文显示与原键分离，不翻译作者正文或改写地图资源字段。

未发行、打包、上传。下一步按完整复核报告修正确性与接入，再补全词表/资源显示/中文搜索并逐页验收。


