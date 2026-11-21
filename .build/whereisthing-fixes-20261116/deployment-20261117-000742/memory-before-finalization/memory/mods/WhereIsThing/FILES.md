# WhereIsThing Files

## 2026-11-16 WhereIsThing 审查修复已实施，未部署

用户已授权开始修改。共享会话/发布失败重试、目录自动就绪补发、无关属性过滤、扫描计时与菜单生命周期已修；有效选择缓存、标签绑定/分类复用、距离与设置局部更新及确定残留清理已完成。184/184 helper + 97/97 生产流程回归，真实依赖 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.fixes，未部署/实机/发行/提交。

新增 WhereIsThingPlugin.State.cs、Tests/StateFlow 四文件及实施报告。隔离目录含 DLL/PDB、build/regression/stateflow 日志、baseline.json、backup/、final-summary.json。一次性 implement_state/implement_lifecycle/prepare_tests 脚本不可重复应用。

[实施与验收记录](IMPLEMENTATION_Fixes_2026-11-16.md)。仅同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 修复方案已调研，未实施

用户要求先调研如何修。确定共享按 Room 会话/actor/模式去重、目录就绪自动补发、无关/重复属性过滤、内容刷新与计时分离；保留协议 2、旧配置、默认客机限制和三秒房主过渡。隔离候选 47/47 检查通过，不能当作生产修复或实机通过。

新增修复方案报告；实验目录 .build/whereisthing-fix-research-20261116/ 含 prepare_probe.py、CandidateMethods.cs、ResearchScenarios.cs、Harness.cs、Probe.csproj、probe.log、source-before.json、research-summary.json。仅实验副本，不直接复制部署。

[修复方案与验证边界](RESEARCH_Fixes_2026-11-16.md)。只同步本地 memory，无可用外部 update_memory 接口。

## 2026-11-16 WhereIsThing 整体审查完成，问题未修复

确认四类状态流问题：相同 payload 跨房/模式漏发布；目录解析后未重新发布；无关房间属性续时并重建窗口；外部常驻→定时保留无穷截止。现有 184/184 回归仍通过，新增隔离审计使用真实方法体和测试替身，未做实机。生产源码未改，问题尚未修复。

新增 REVIEW_2026-11-16.md；隔离证据位于 .build/whereisthing-audit-20261116/：prepare_audit.py、AuditHarness.cs、ExtractedMethods.cs、audit.log、regression.log、source-before.json、audit-summary.json。未新增插件构建/部署产物。

[完整审查报告](REVIEW_2026-11-16.md)。仅同步本地 memory；本次没有可用的外部 update_memory 接口。

## 2026-11-16 晚安莓统一标注已实施，未部署

用户“开始”已授权实施：食物分类中的单一晚安莓条目覆盖未摘/已摘；未摘锚定果实，隐藏/采摘/更换后旧标签失效，空株不再标记。Ground控制未摘，已摘沿用地面/手持/背包规则；旧itemID或场景token预设兼容，取消勾选清除两者，共享协议不变。184/184逻辑回归、Release 0警告0错误。开发标识0.1.3-dev.20261116.napberry；仅隔离产物，未部署/改cfg/实机/发行。

新增ThingNapberry.cs、WhereIsThingPlugin.Napberry.cs、Tests/NapberryTests.cs。产物/回归/身份记录：.build/whereisthing-napberry-20261116/，DLL 187392 bytes，SHA256 8EEA8EDDC8D808FB0FF8DDE9D3A2D2D17DDA7000C9406659B6DF41ED9061CF68。

[实施与验收记录](IMPLEMENTATION_Napberry_2026-11-16.md)。仅维护本地memory。

## 2026-11-16 晚安莓统一标注可行（仅方案）

用户追问统一标注可行性：可用单一“晚安莓”入口/标签，未摘定位果实，采摘后撤掉植株标签并沿用物品扫描；内部仍分FakeItem/Item两条发现路径，旧物品ID/场景选择应兼容。沿用先调研要求，未改生产代码、配置、构建或部署。此前空株误标缺口仍未修复。

[方案与边界](RESEARCH_Napberry_2026-11-16.md)。

## 2026-11-16 晚安莓植株扫描调研（未改代码）

只选普通晚安莓不会自动扫描植株；需另选自然危险中的“未摘下的晚安莓”。实际扫描NapBerrySpawn植株对象，未检查fakeBerry采摘/激活状态，采摘后若植株仍激活会残留标签。已用3.1源码与sharedassets4资源交叉核查，未改生产代码/配置、未构建部署或实机。该旧缺口未修复，也不在此前143项回归覆盖内。

[调研证据与修复边界](RESEARCH_Napberry_2026-11-16.md)。只维护本地memory。

## 2026-11-16 客机配置与改版整合完成，额外识别仅调研

用户认可的改动已整合到开发源码：客机 RestrictClientPresets 默认 true（本地偏好），本地多预设并集、放置物门控、蘑菇毒/无毒品种及 Clone 识别、地面过滤与标签范围优化。143/143 逻辑回归，当前 PEAK 3.1.0 隔离 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.presets；未部署、未实机、未改发行。

新 helper：ThingPresetSelection.cs、ThingItemTraits.cs、ThingLabelRange.cs；回归 PresetAndTraitTests.cs。证据/构建日志/复核脚本位于 .build/whereisthing-research-20261116/。隔离DLL为 bin/WhereIsThing.dll，185344 bytes，SHA256 D9456F7E5B72CF2EFCA9B7BA190F1358A4FB330021B81BBE1A7B4F52C4CE8B34。构建覆盖 OutputPath，命令见报告。

[实施与额外能力调研](IMPLEMENTATION_PresetsAndTraits_2026-11-16.md)。此条覆盖下方只对比、未授权移植状态；旧记录保留为历史。

## 2026-11-16 反编译对比文件

- 新报告：`COMPARISON_Renlei_0.1.3.1_2026-11-16.md`，含两侧源码行号与行为差异。
- 参考目录：`引用参考代码/反编译/BepInEx/plugins/WhereIsThing-renlei/`；插件/程序集0.1.3.1。
- 开发目录、DLL、配置及发行产物未修改，无本轮构建或部署产物。

## 2026-10-29 23:32 WhereIsThing 0.1.3 发行文件已备齐

新增 MOD开发/WhereIsThing/发行/0.1.3/：WhereIsThing.dll（180224 bytes，程序集 0.1.3.0）、icon.png、README.md、CHANGELOG.md、manifest.json。DLL SHA256：4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与 .build/whereisthing-0.1.3 和 2.0.a/BepInEx/plugins 根目录安装一致。验证报告 .build/whereisthing-release/verification.json；未生成 ZIP。

## 2026-10-29 23:03 部署路径纠正

用户明确要求 DLL 放在 2.0.a/BepInEx/plugins 根目录。此前采用子目录不符合要求；本次检查时 profile 下已没有 WhereIsThing.dll/PDB，不推断移除原因。现已将验证过的 0.1.3.0 DLL 和 PDB 复制到 C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/，csproj 默认 OutputPath 同步恢复根目录。DLL 为 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522，与隔离产物一致；递归复查仅一份 DLL。未改功能或配置、未重新构建/启动游戏。本条覆盖下方当前安装在子目录的历史描述。既有 71 项逻辑回归与实机待验状态不变；仅同步本地 memory。

## 2026-10-29 22:07 WhereIsThing 0.1.3 已实施

当前源码与程序集 0.1.3 / 0.1.3.0。新增 `ThingPickupIdentity.cs`、`WhereIsThingPlugin.Plants.cs`、`WhereIsThingPlugin.Luggage.cs`、`ThingSegmentFilter.cs`、`Tests/WhereIsThing.Regression.csproj`（71 检查）。隔离构建命令：`dotnet build MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj -c Release -p:OutputPath=C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-0.1.3/`。默认 OutputPath/安装为 profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhereIsThing/，DLL 180224 bytes，SHA256 4D99F46134B996BB8788BFEA6F86C4C613F6B5DE8F5CD5AE7D81CFCB16C8B522。备份 `.build/whereisthing-0.1.3/backup-20261029-220734/`。详见 [实施记录](IMPLEMENTATION_0.1.3_2026-10-29.md)。

## 2026-10-29 21:36 反编译路径更新

当前游戏参考：`引用参考代码/反编译/3.0.1/Assembly-CSharp/`；旧参考：`引用参考代码/反编译/作废/3.0.0/Assembly-CSharp/`。本需求涉及的 12 类对照一致，见调研报告顶部补充；仅更新文档，不改变工程引用或输出路径。

## 2026-10-29 调研入口与实际路径

- `RESEARCH_0.1.3_2026-10-29.md`：版本、语言、地形植物、箱子、关卡范围的证据/建议/待验矩阵。
- 参考：`引用参考代码/反编译/BepInEx/plugins/LengSword-BetterItemInfoDisplay-0.1.5/BetterItemInfoDisplay/ItemInfoController.cs:151-181`。
- 实际安装：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/WUYACHIYU-WhereIsThing/WhereIsThing.dll`，164352 bytes，SHA256 `CF1C9043F74E843032CB65F972137EA135297D690EA9E397929977AAB889BF63`；plugins 根目录当前无同名 DLL。csproj 默认仍输出根目录，后续部署必须防止重复安装。
- 下方旧安装路径为历史。当前游戏 DLL 已异于 3.0.0 审核，详见报告基线；本轮无构建/产物更新。

## 2026-09-21 发行文件准备完成

当前版本 `0.1.2` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsThing/发行/0.1.2`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：放置物及 owner 识别、玩家放置预设、分类校正、分帧发现和标签优化、20000 排序、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前产物

- 开发版本 `0.1.2` / 程序集 `0.1.2.0`；新增 `Helpers/ModConfigUiAdapter.cs`（独立反射适配器）。
- profile 输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`，164352 字节，SHA-256 `CF1C9043F74E843032CB65F972137EA135297D690EA9E397929977AAB889BF63`。
- 旧发行 DLL/hash 属于历史，不再与当前 profile 相同；验证入口：`../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。

更新时间：2026-09-06

## 源码

- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.cs`：BepInEx 入口、配置、快捷键、公共状态、标签生命周期和场景清理。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.Discovery.cs`：增量 PhotonView 发现、有限重试、放置目标分类和 owner 缓存。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.SceneDiscovery.cs`：普通物品、行李箱、静态/动态场景目标的分帧发现。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.PlacedSources.cs`：从物品目录建立放置 prefab 正向来源索引，并维护纯客户端投掷证据。
- `MOD开发/WhereIsThing/WhereIsThing/ThingTypes.cs`：物品、行李箱和场景目标定义，动态数据库加载、名称、危险分类，以及 ItemSpawnerEnhanced 审计后加入的精确 prefab 分类覆盖。
- `MOD开发/WhereIsThing/WhereIsThing/ThingLabel.cs`：世界空间目标到屏幕标签的显示。
- `MOD开发/WhereIsThing/WhereIsThing/ThingSelectionWindow.cs`：物品选择窗口、范围复选框、仅显示已选、分类网格、自适应列数和鼠标状态。
- `MOD开发/WhereIsThing/WhereIsThing/ThingPresetPickerWindow.cs`：预设选择、共享和管理窗口。
- `MOD开发/WhereIsThing/WhereIsThing/ThingPresets.cs`：预设数据、内置预设和序列化逻辑。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/FontHelper.cs`：游戏 TMP 字体获取和场景缓存失效。
- `MOD开发/WhereIsThing/WhereIsThing/Helpers/ModConfigLocalization.cs`：ModConfig 分组、配置项、说明和枚举下拉选项的中英文本地化。
- `MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj`：net4.7.2 工程与 PEAK `2.4.b` DLL 引用。
- `memory/mods/WhereIsThing/PLAN.md`：分阶段研究、实机验证、收口和发布计划。

## 构建

在 `MOD开发/WhereIsThing/WhereIsThing` 执行：

```powershell
dotnet restore WhereIsThing.csproj
dotnet build WhereIsThing.csproj --configuration Release --no-restore
```

输出：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`。

当前测试 DLL：`C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\WhereIsThing.dll`，程序集版本 `0.1.2.0`，大小 `158720` 字节，SHA-256 为 `AC1D98407D52BAA61E1B7B4EF33F5AB4B09CEC168E28D65E019587B048DCEFA9`。2026-09-06 Release 已成功覆盖该目录，结果为 `0 warnings / 0 errors`。

`MOD开发/WhereIsThing/发行/1.0.3` 是此前已有的发行目录，本轮 2.4.b 兼容修复没有更新该目录，也没有新建 ZIP；不得把目录内 DLL 当作当前测试构建。更早的 `发行/0.1.1` 及其 `0.1.1.0` DLL/hash 仅为历史产物。

当前依赖 `Assembly-CSharp.dll`、`Zorro.Core.Runtime.dll`、`Sirenix.Serialization.dll`、BepInEx、Harmony、Unity UI、TMP、TextRenderingModule 和 PhotonUnityNetworking。PEAK `2.4.b` 的 `Assembly-CSharp.dll` 位于 `C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed`，SHA-256 为 `21CBF3A6585A72778A2E5FB007E36CA246002DA5AE8209A6D998673633D82759`。ModConfig 是可选运行时集成，未安装时本地化补丁静默跳过。

分类参考源码：`引用参考代码/反编译/BepInEx/plugins/ItemSpawnerEnhanced/`。关键依据为 `VanillaItemCategories.cs`、`ItemCategoryResolver.cs` 和 `VanillaItemVisibility.cs`；该参考插件版本 `1.3.0` 自述基线为 PEAK 2.1.a，因此只作审计证据。
