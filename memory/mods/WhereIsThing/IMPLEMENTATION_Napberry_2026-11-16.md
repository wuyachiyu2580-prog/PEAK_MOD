# 晚安莓统一标注实施记录

日期：2026-11-16，Get-Date 已确认。用户“开始”授权实施已讨论方案，覆盖前一阶段只调研限制。

## 当前结果

- 开发源码已实现；筛选目录中只有一个“晚安莓”，归入食物。菜单和未摘/已摘标签使用同一物品本地化名称。
- 未摘时标签绑定 FakeNapBerry 果实的位置，而不是留在场景中的 NapBerrySpawn 根对象。
- 果实隐藏、pickedUp、被替换、缺失或植株失活时，旧标签失效；拾取被游戏拒绝后恢复可见，原标签仍在则恢复有效，已移除则下一轮发现重建。
- 未摘果实遵守 Ground；已摘果实沿用物品扫描，按 Ground/Held/Backpack 范围显示。没有新增库存类型或改变游戏物品状态。
- 旧 itemID-only 与旧 NapberryHypnoOrb-only 预设都识别为一个勾选。有效选择层展开为两条发现路径；取消选中同时清掉物品ID和旧场景token。原本未编辑的保存/共享payload不因展开被重写；旧枚举值和共享协议保持。
- 原有多预设并集和默认开启的客机限制仍在；未擅自加入毒浆果、蘑莓效果、烹饪等候选功能。

## 代码位置

- ThingTypes.cs：显式按Napberry真实物品身份合并条目，不靠“Napberry”和“Unpicked Napberry”名字匹配；统一名称/食物分类。
- ThingNapberry.cs：身份、未摘果实可见性、有效选择展开。
- WhereIsThingPlugin.Napberry.cs：专用植物发现及标签生命周期；以fruit instance ID去重。
- WhereIsThingPlugin.SceneDiscovery.cs：原第10步改调用专用扫描，维持分帧扫描方式。
- WhereIsThingPlugin.cs：RefreshEffectivePresetState在合并预设后展开晚安莓选择，旧本地/共享预设走相同逻辑。
- Helpers/ModConfigLocalization.cs：旧token显示名统一晚安莓，稳定键不变。
- Tests/NapberryTests.cs：新增41项回归，GameStubs与csproj链接生产实现。

## 验证

- 184/184逻辑回归通过（此前143 + 本轮41）。覆盖目录去重/官方语言/旧选择/共享codec/取消勾选/目标切换/果实锚点/采摘与恢复/空株/更换引用/范围。
- Release构建0警告0错误，引用当前PEAK 3.1.0游戏程序集。
- git diff --check通过；LF/CRLF提示只是既有Git行尾策略。
- 输出：.build/whereisthing-napberry-20261116/bin/WhereIsThing.dll，187392 bytes。
- SHA256：8EEA8EDDC8D808FB0FF8DDE9D3A2D2D17DDA7000C9406659B6DF41ED9061CF68。
- 插件/程序集仍0.1.3/0.1.3.0，开发标识0.1.3-dev.20261116.napberry。
- 证据：该.build目录regression.log、build-identity.json及final-verification.json。
- 构建命令：dotnet build MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj -c Release -p:OutputPath=C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-napberry-20261116/bin/ --nologo。
- 测试命令：dotnet run --project MOD开发/WhereIsThing/Tests/WhereIsThing.Regression.csproj -c Release --no-launch-profile。

## 交付边界

本轮只输出隔离产物：**未复制到profile、未修改用户cfg、未更新发行包、未启动游戏**，未提交/上传。默认csproj仍输出2.0.a/plugins根目录，验证时须覆盖OutputPath。此前反编译整合也在这份产物内。

逻辑检查不代替真实Unity/Photon。后续实机核对：自己/他人采摘、拾取拒绝恢复、晚加入、持有/丢弃/背包切换、当前段/距离、主客机共享预设及中文布局。标签按各自发现周期更新，不承诺零帧切换或跟踪每颗果实的唯一跨形态身份。

本次把目标语义统一为可取得的晚安莓，不按催眠组件enabled过滤果实；催眠关闭但果实仍可取时仍应显示。没有改变游戏危险规则。

历史依据：[调研](RESEARCH_Napberry_2026-11-16.md)、[此前整合](IMPLEMENTATION_PresetsAndTraits_2026-11-16.md)。本记录覆盖旧“未实施/空株缺口未修复”状态。仅同步本地memory，没有外部update_memory接口。
