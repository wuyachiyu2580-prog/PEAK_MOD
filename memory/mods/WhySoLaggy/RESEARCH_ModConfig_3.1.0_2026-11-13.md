# WhySoLaggy / ModConfig LOC: 0 调研

## 历史追查补充（同日后续）

[旧版本触发条件调查](HISTORY_ModConfig_LOC0_2026-11-13.md) 补充六版游戏/ModConfig 缓存和真正执行 Harmony 包装的实验。游戏版本不是必要条件；本次 ShootZombies 的 GetDisplayName 继承目标告警与链路相关。仍未改生产代码或实机验证修复。

日期：2026-11-13。用户要求摸排设置报错、卡顿及影响其他 MOD，并提供启用 WhySoLaggy 后的截图。本轮只读排查、保存证据、更新 memory；未修改插件源码、配置、版本、安装 DLL 或发行包，未启动游戏。以下修复未实施。

## 结论

直接故障是 ModConfig 按键设置 BepInExInputBindingSetting<KeyCode>.GetDisplayName() 抛出 ArgumentException，设置列表构建中断。截图中的 LOC: 0 是未赋予正确标题的 UI 模板文字，不代表配置值变成 0。

WhySoLaggy 的 PatchProfiler 向共享方法加入计时补丁，要求 Harmony 注入 MethodBase __originalMethod。当前 Harmony 使用 ReflectedType.IsGenericType 选择 GetMethodFromHandle 重载，存在遗漏实际 DeclaringType 为泛型的继承方法的风险。日志正是在单参数重载处失败，安装 DLL 的 IL 与参考源码吻合。这是优先修复的根因链。

边界：未捕获游戏进程内目标 MethodInfo 的 ReflectedType，未运行修改后的 DLL，也没有关闭 EnablePatchProfiling 的受控实机 A/B。本轮确认异常、补丁归属和句柄机制；不得写成已修复或已实机验收。

## 实际基线

- 游戏 PEAK 3.1.0；反编译：引用参考代码/反编译/3.1.0/，3.0.1 已入作废。
- Assembly-CSharp.dll：1,652,224 bytes，SHA256 A3643F11DC444D86491270B224C0A0CAF9126A10B26731FEB97FC85AD1D82038。
- WhySoLaggy 1.0.5.0：160,256 bytes，SHA256 946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550。
- ModConfig 1.8.2.0 / PEAKLib.UI 1.7.2.0。
- 0Harmony.dll 程序集 2.9.0.0，SHA256 1A21CC03424FC82C3DD1346905D16494536B9595AE4162228D99FB7C285C1031。
- EnablePatchProfiling=true，EnablePluginProfiling=true，IgnorePatchMethods 为空，LogVerbosity=Normal。

Profile：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/。

本次活动 DLL 在 plugins/WUYACHIYU-WhySoLaggy/WhySoLaggy.dll，覆盖旧 memory 中 plugins 根目录的历史描述。复现前曾停用，后来由用户启用；本轮没有代为启停。

## 已保存证据

目录：.build/whysolaggy-modconfig-3.1.0-20261113/。源日志不变。

- Player-reproduced.log：8 次同类泛型句柄异常，首次第 259 行（按文本逻辑行计数；rg 对原始 CR 字节的计数略不同）。
- Player-previous-disabled.log：无同类异常；不是受控 A/B，也不代表没有其他错误。
- BepInEx-reproduced.log：首行 Unable to start Unity log writer，因此只查 BepInEx 日志会漏掉 UI 异常。
- WhySoLaggy-reproduced.log：22:17:57 启动，22:18:09 扫描 661 个已补丁方法，计时 646 个，self-only 跳过 15 个，Failed=0；PluginProfiler 计时 15 个方法。以本次文件为准，不沿用此前会话计数。
- harmony-patches-reproduced.csv：约第 2333–2344 行，KeyCode/string 闭合泛型 GetDisplayName 的补丁。

关键异常原文：

    ArgumentException: Cannot resolve method System.String GetDisplayName()
    because the declaring type of the method handle ...BepInExInputBindingSetting<TValue>
    is generic. Explicitly provide the declaring type to GetMethodFromHandle.
      at System.Reflection.MethodBase.GetMethodFromHandle(RuntimeMethodHandle handle)
      at ...BepInExInputBindingSetting<KeyCode>...GetDisplayName(...)
      at ...ModSettingsMenu...ShowSettings(...)
      at ...ModSettingsMenu...SetSection(...)

CSV 同一目标有 WhySoLaggy 的 Prefix 800、Postfix 400、Postfix 0；800/0 对应计时，400 对应本地化。PlayersInfo、WhereIsMyAmulet、WhereIsThing 本地化也使用这一共享方法。

## 故障链与源码锚点

1. MOD开发/WhySoLaggy/WhySoLaggy/Helpers/PatchProfiler.cs:67 遍历所有已补丁方法，106–110 对存在外部 owner 的目标注入计时，没有泛型保护。
2. 同文件 131/136 行 TimingPrefix/TimingPostfix 都请求 __originalMethod。Prefix 未使用该参数仍触发注入。Postfix 的空值判断不能处理进入回调之前的参数生成异常。
3. 引用参考代码/反编译/BepInEx/core/0Harmony/HarmonyLib/Public/Patching/HarmonyManipulator.cs:636 的 EmitOriginalBaseMethod，在 652–661 行使用 ReflectedType 决定句柄重载。Mono.Cecil 只读检查当前安装 0Harmony.dll 确认实际 IL 一致。
4. 非泛型 BepInExKeyCode/BepInExKeyPath 继承泛型 BepInExInputBindingSetting<KeyCode/string>，GetDisplayName 实现在父类。经非泛型子类取继承 MethodInfo，可能 ReflectedType 非泛型而 DeclaringType 泛型。
5. 引用参考代码/反编译/BepInEx/plugins/ModConfig/com.github.PEAKModding.PEAKLib.ModConfig/PEAKLib.ModConfig.Components/ModSettingsMenu.cs:134 先创建行，136 读名称，138 设 alpha=0，139 创建控件，142 启动淡入。读名称失败后当前行无正确标题/控件，前面建好的行可能仍 alpha=0，淡入也未执行。因此只剩一行 LOC: 0，不能断言配置排序的第一项就是按键。
6. 引用参考代码/反编译/3.1.0/Assembly-CSharp/LocalizedText.cs:414 缺失文字返回 LOC: 加 id，与截图残留相符。

WhySoLaggy 自身配置没有 KeyCode，绕开此按键路径。WhereIsThing 在 General 有 ScanKey/WindowKey（WhereIsThingPlugin.cs:181–182）。多个 MOD 共用设置类型，影响范围不局限于单个 MOD；不能概括成所有中文 MOD 必然失败。

## 已做的辅助验证

- 只读检视安装 WhySoLaggy DLL：计时方法参数及 IL 与源码一致。
- 独立 PowerShell 进程动态编译 Derived : Base<int>，继承 MethodInfo 的 ReflectedType=Derived、DeclaringType=Base<int>；单参数 GetMethodFromHandle 抛同类异常，传 DeclaringType.TypeHandle 的双参数重载成功。
- 这只是 .NET 反射机制验证，不是 Unity/Mono 完整 Harmony 回归。
- 既有 ModConfigIntegrationTests.cs:137–146 只测反射选到闭合泛型方法，没有实际叠加计时补丁后调用。旧测试写死 2.4.b 路径的问题与此 UI 异常无关。

## 待实施方案

优先修复 WhySoLaggy 计时注入，保留国际化和配置身份。

- 将继承目标解析回实际声明的闭合类型，验证进入 Harmony 的目标 MethodInfo；覆盖 ReflectedType 与 DeclaringType 不同的情况。
- 考虑注册时缓存方法标识，生成或绑定不依赖 __originalMethod 参数注入的计时回调，减少每次反射/字符串处理；实现须验证 Harmony 回调支持、卸载与泛型共享。
- 无法安全观测的目标明确跳过并记录原因，保护业务方法。只隔离计时补丁，不按共用 owner 整体移除 WhySoLaggy 本地化/RPC 补丁。
- 不删翻译、不强行替换 LOC: 0、不清 ModConfig 全局缓存或重新注册。
- 回归必须真正执行包装后的方法：泛型父类/非泛型子类、值类型与引用类型、多 owner、关闭计时的恢复。
- 实机检查 WhySoLaggy、WhereIsThing、WhereIsMyAmulet、PlayersInfo 全部行、搜索筛选、中英繁切换及保存；检查 Player.log 无同类异常。

## 临时绕过及其他发现

建议退出游戏，在 profile/config/com.wuyachiyu.WhySoLaggy.cfg 的 General 中设 EnablePatchProfiling=false 后重启，停止补丁耗时采集。此建议本轮未执行或实机 A/B；其他独立模块可保留。不能仅在当前游戏里关开关就假定补丁已经撤销。

配置热更新是另一问题：Awake/Start 把设置复制进模块，计时延迟 5 秒初始化，没有 SettingChanged 热更新/撤销链。“保存但行为没变”需另行明确实时生效与重启语义。

本次计时初始化后记录 1714ms spike；广泛同步注入及每次生成方法 key 有额外开销，但不能凭此量化全部菜单卡顿。共享 ModdedTABSButton.Update 计时含其他 owner，不能把总耗时直接归给一个 MOD。ShootZombies 的 ShowSettings(string) 缺失是独立风险；后续追查确认 GetDisplayName 继承目标告警与本次故障链相关，见顶部补充。

后续构建必须显式隔离 OutputPath，项目默认路径会写入 profile/plugins，避免调研时意外部署或重复 DLL。WhereIsThing 0.1.3 与历史发行不因本次调研改变。

仅同步本地 Markdown memory；工具未提供 update_memory 实体接口，未声称同步独立记忆服务。既有其他项目改动保留。
