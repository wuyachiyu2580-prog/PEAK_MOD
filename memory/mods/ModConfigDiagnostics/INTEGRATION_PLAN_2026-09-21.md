# 四个 MOD 的 ModConfig 更新计划

日期：2026-09-21。

状态：源码实现、指定版本同步、构建及 profile 部署已完成；23 项自动测试通过，完整实机验收待完成。实际产物与证据见 `INTEGRATION_RESULT_2026-09-21.md`。后续用户已要求准备四个新版发行目录，五件套已备齐且不生成 ZIP，旧版目录保留。下方保留原实施设计。

## 1. 范围与版本

| MOD | 当前源码版本 | 实施目标 | 程序集目标 |
|---|---|---|---|
| PlayersInfo | 0.2.4 | 0.2.5 | 0.2.5.0 |
| WhySoLaggy | 1.0.4 | 1.0.5 | 1.0.5.0 |
| WhereIsThing | 0.1.2 | 保持 0.1.2 | 0.1.2.0 |
| WhereIsMyAmulet | 1.0.4 | 保持 1.0.4 | 1.0.4.0 |

只实施用户选择的四个 MOD。Lantern&ShootZombies&Night 不在此次范围，其全局重注册风险仍存在，不能宣称修完四个 MOD 就消除了完整 profile 的全部风险。PEAKLib、诊断 MOD 和其他业务 MOD 的源码不作为本轮修改对象。

保留 HUD、扫描、联网、性能采集和配置身份。两个定位 MOD 的 Canvas 排序 20000、WhereIsThing 窗口 Raycaster、WhereIsMyAmulet 的 TMP/投影修复沿用现状。升级开发版本不等于发布，不覆盖历史发行目录、不自动制作 ZIP。

## 2. 调研依据

本机 DLL 版本、当前启动日志和工作区反编译对应：ModConfig 1.8.2、PEAKLib.UI 1.7.2、Core 1.7.2；游戏参考为 PEAK 2.4.b。这是本机当前基线，不代表已联网确认全网最新版。

路径缩写：`MC` 指 `引用参考代码/反编译/BepInEx/plugins/ModConfig/com.github.PEAKModding.PEAKLib.ModConfig/`；`GAME` 指 `引用参考代码/反编译/2.4.b/`。业务源码在 `MOD开发/<MOD>/<MOD>/`。

| 证据 | 确认结论 |
|---|---|
| PlayersInfo `Helpers/ModConfigLocalization.cs:124`、`PlayersInfoPlugin.cs:364`；MC `PEAKLib.ModConfig/ModConfigPlugin.cs:327`；GAME `Assembly-CSharp/SettingsHandler.cs:72` | 启动 8 秒后语言判断变化会清空 EntriesProcessed，再重新包装所有插件配置；AddSetting 直接追加。catch 无法防止这种无异常的重复注册。 |
| 四个 helper 的 ModdedSettingsMenu；MC `PEAKLib.ModConfig.Components/ModSettingsMenu.cs:17` | 实际新类型是 ModSettingsMenu，旧类型查找失败导致菜单补丁静默跳过；配置名称部分生效不等于整个集成正常。 |
| WhySoLaggy `Helpers/ModConfigLocalization.cs:425`；GAME `Assembly-CSharp/LocalizedText.cs:54` | 游戏语言通知是静态 Action 字段，GetEvent 无法订阅。 |
| WhySoLaggy `Helpers/ModConfigLocalization.cs:645` 和现有分类测试 | 以 General/UI/Logging 等通用 section 判断 MOD 归属会误认其他插件；旧测试把该行为当成正确，须一起修正。 |
| 四个 helper 的 GetDisplayName 遍历；MC `PEAKLib.ModConfig.SettingOptions/BepInExInputBindingSetting.cs` | 快捷键方法继承自闭合泛型基类，须规范化到声明方法并去重。当前日志复现 WhySoLaggy HarmonyX 警告，其余三个本次未加载，只能认定同型源码风险。 |
| PlayersInfo `Helpers/LanguageHelper.cs`、`PlayersInfoPlugin.cs` | 仅启动检测及延迟复查，缺后续语言监听；整数只识别简中 9，繁中 10 会被判为英文。 |
| MC `PEAKLib.ModConfig.SettingOptions/BepInExEnum.cs`；GAME `Zorro.Settings.Runtime/Zorro.Settings.UI/EnumSettingUI.cs` | GetUnlocalizedChoices 同时参与 GetValue、SetValue 和默认值映射；不能翻译该返回值，只改 dropdown 显示。 |
| MC `PEAKLib.ModConfig.SettingOptions.SettingUI/BepInExEnum_SettingUI.cs`；GAME `Zorro.Settings.Runtime/Zorro.Settings/SettingInputUICell.cs` | 可由 KeySetting 或 _listeningSetting 找到 setting，再通过 IBepInExProperty.ConfigBase 识别配置，无需靠当前文本猜归属。 |
| MC `PEAKLib.ModConfig.Components/ModSettingsMenu.cs:134`；PEAKLib.UI `PEAKLib.UI.Hooks/PauseMenuHooks.cs:23` | 新版仍克隆共享 SettingsCell 后直接写标题，未见关闭 LocalizedText.autoSet；存在公共覆盖风险，本轮没有活动页面复现。 |

## 3. 统一接入方案

### 可选依赖与异常边界

- 四个插件声明 BepInEx SoftDependency，保证 ModConfig 安装时的加载顺序；不增加硬依赖或共享运行 DLL。
- 各自 helper 使用一致接入约定，本轮不扩张为框架重构。
- 优先解析新 ModSettingsMenu，旧类型仅作受保护回退。按真实签名匹配 OnEnable()、ShowSettings()、SetSection(string)、UpdateSectionTabs(string)，不按名字取首个重载。
- GetDisplayName 解析到真实声明方法，按 MethodBase 去重；保留 KeyCode/string 两个闭合泛型实现，跳过开放泛型、抽象或无方法体目标。
- 记录已成功安装的目标；单个目标失败只降级该项，不中断其他本地化。缺插件安静跳过；不兼容成员只输出一次可定位警告，不将跳过记成成功。
- 第三方类型通过反射适配，失败保留原始英文与可操作配置。销毁时退订自己的语言 handler、停止刷新、移除自身补丁，不影响其他插件。

### 配置与 UI 归属

- 文案以“本 MOD ConfigFile 身份 + 原始 section/key”为键。优先通过 IBepInExProperty.ConfigBase（含显式接口属性）取配置，控件由 KeySetting 或监听 setting 对应到 entry；无法确认归属则跳过。
- 菜单切换完成后读取实际 selectedMod、selectedSection 和控件归属；搜索/筛选可能自动切 MOD，不能只缓存 SetSection 参数。
- 插件 tab 只改本 MOD 对应文本；section tab 仅在当前 MOD 匹配时处理。使用稳定 category/包装 setting 的 GetCategory 识别，不改 GameObject 名称、category、section 或选中状态。
- 仅处理自己配置行的标题和控件，移除整个菜单内按文字替换的做法；不改其他 MOD、搜索/输入框、筛选器和公共按钮。

### 语言与刷新生命周期

- PlayersInfo 优先 LocalizedText.CURRENT_LANGUAGE，简中/繁中走中文，其余英文；初始化未完成时受控兜底，完成后刷新一次。
- PlayersInfo 订阅实际语言委托；WhySoLaggy 用静态 Action 字段订阅/退订自身 handler，保留其无游戏程序集硬引用的方式；两个定位 MOD 沿用已有正确订阅。
- 启动、语言变化、菜单创建/切换合并到下一帧刷新；同一菜单根只排一个任务，执行时再次核对归属与 active 状态。
- 更新自己的 ConfigDescription 和已有 UI，不重新注册配置、不反复调用 Setup、不重复添加 onValueChanged。菜单关闭时只更新数据，下次打开应用。
- 关闭、切场景、销毁后清理失效引用，不每帧全局扫描。

### 枚举显示与标题保护

- 在枚举 UI 的 Setup/RefreshText 后，先验证 setting 归属，再按原始索引修改 TMP_Dropdown.options.text 并更新 caption。
- 保持数量、顺序、索引、枚举名和回调；不翻译 GetUnlocalizedChoices/GetValue/SetValue，不向 BoxedValue/.cfg 写中文。
- 展开的列表切语言后同步显示，只操作归属确定的 dropdown 及其生成列表；不改变选择或触发值变更。默认值、外部配置更新后仍显示正确。
- 对确认属于自己的活动 SettingsUICell，保留 LocalizedText 引用，设置 autoSet=false 后写最终标题。仅保护自身实例，不修改共享模板、不全局 Patch LocalizedText、不伪造语言 ID 0。
- 此保护不等于修复其他 MOD 的 LOC: 0；公共依赖问题仍独立保留。

## 4. 实施顺序与文件

1. **PlayersInfo**：移除 RefreshCache 实现和调用；修正语言来源/监听；接入新菜单及归属受限的配置行、枚举显示。涉及 PlayersInfoPlugin.cs、Helpers/LanguageHelper.cs、Helpers/ModConfigLocalization.cs、PlayersInfo.csproj、Properties/AssemblyInfo.cs。目标 0.2.5。
2. **WhySoLaggy**：修正语言字段订阅、新菜单、声明方法补丁、当前 MOD 识别及 Shutdown；修正旧分类测试和测试执行链。涉及 WhySoLaggyPlugin.cs、Helpers/ModConfigLocalization.cs、WhySoLaggy.csproj、Properties/AssemblyInfo.cs、WhySoLaggy.Tests。目标 1.0.5。
3. **WhereIsThing**：迁移菜单/控件接入、补语言变化后的可见 UI 刷新、限制翻译范围；保留稳定键映射。主要涉及 WhereIsThingPlugin.cs、Helpers/ModConfigLocalization.cs，必要时补工程引用；保持 0.1.2。
4. **WhereIsMyAmulet**：恢复新菜单实例跟踪、隔离 UI 归属、修正下拉显示及刷新清理。主要涉及 WhereIsMyAmuletPlugin.cs、ModConfigLocalization.cs，必要时补工程引用；保持 1.0.4。
5. **版本、文档和联合验收**：核对四个 DLL 版本、输出位置与 SHA-256；分别记录构建、自动测试、实机结果。

四个工程均 GenerateAssemblyInfo=false。PlayersInfo/WhySoLaggy 同步 PluginVersion、csproj Version、AssemblyInfo 的 AssemblyVersion/AssemblyFileVersion；不能只改项目 Version。两个定位 MOD 的版本字段保持原值。

实施时更新开发 changelog 与 memory；没有开发 changelog 的项目可新建项目根 CHANGELOG.md。历史发行 README/manifest/ZIP 保留原版本事实；后续若制作新发行目录，再在新目录同步 README/CHANGELOG/manifest/DLL，不单独给旧包改版本。

## 5. 验证与验收

### 自动检查

- 复核四个 MOD 无全局缓存清空/注册，旧类型仅安全回退，无通用 section 归属判断，语言订阅/退订对称。
- 回归重点：其他 MOD 的 General/Enabled 不被改写；跨 section 同名 key 正确；枚举显示变化不改序列化值/索引；重复初始化和销毁不重复订阅/补丁；缺插件/成员时主功能可继续。
- WhySoLaggy 分类测试加入“其他 MOD 同分区”反例，不继续断言 General 即属于自己。当前测试项目缺 Microsoft.NET.Test.Sdk，实施时补齐执行链，可参考 StateKeeper.Tests 的 17.11.1；记录真实发现/执行数量及 TRX，0 tests 或仅编译成功不算通过。
- 四个项目分别 Release 构建，目标 0 warnings / 0 errors。默认输出仍为 `C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/`；不切临时发布路径。若游戏锁 DLL，报告锁定，不擅自结束游戏进程。

### 实机矩阵

| 场景 | 通过标准 |
|---|---|
| 单 MOD 与四个同时安装 | 各自正确，其他插件配置/文案不变，无本地化异常 |
| 英文→简中→繁中→英文，至少 5 次切换 | 名称/分区/枚举与已有描述数据跟随，包装项数不增长 |
| 主菜单/暂停菜单、搜索、5 类筛选、切 MOD/section、关开菜单 | 自动切换后归属正确，无重复刷新、失效引用、输入被翻译 |
| 选择枚举、DEFAULT、外部配置变化、展开下拉切语言 | 显示与实际值一致，无中文枚举值、错索引或额外回调 |
| 菜单打开前后和多次语言切换后读诊断 | 同配置集合无重复包装身份，自身活动标题不被覆盖为 LOC: 0 |
| 无 ModConfig 加载四个业务 MOD | 主功能正常，可选集成安静跳过 |
| 重启、切场景、销毁恢复 | 无重复监听/补丁，无旧菜单引用；已有 TFA/标签表现不回退 |

计数按 ConfigFile+section+key 与包装身份判断，不能将含原版设置的 TotalSettingCount 和 MOD 配置数直接作差。受控验收先使用不加载旧 Lantern 的测试配置；完整 profile 若加载它后出现重复，再定位触发源，不自动修改它。测试配置调整保留可恢复记录，不删除用户插件。

旧类型回退属于防护设计，没有旧 DLL 实测就不宣称通过旧版本兼容。Unity UI/性能验证不能被自动测试替代；未进行的实机项明确保留待验收。

## 6. 完成定义

- 四个 MOD 修复和用户指定版本链一致，开发文档与 memory 同步。
- 构建及必要的真实自动测试通过，DLL 版本/hash 可核查。
- 实机已测/未测分别记录，不将源码推断或历史报告充当验证。
- 不自动上传 Thunderstore、制作 ZIP 或扩大到 Lantern/公共依赖修复。

当前没有专用 update_memory 工具；本计划与各 MOD TODO/temp、索引、CHANGELOG 是本轮实际写入的接续记录。
