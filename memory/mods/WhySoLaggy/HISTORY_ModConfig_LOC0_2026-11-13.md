# LOC: 0 历史触发条件调查

日期：2026-11-13。用户追问：之前游戏是否也会有，为什么此次发生。本轮只调查，未改插件源码、配置或安装 DLL，未启动或回退游戏。诊断程序只在 .build 独立进程运行。

## 结论

旧游戏在相同插件组合和计时条件下也存在这条故障路径，不能认定是 PEAK 3.1.0 新引入。主要变化在 ModConfig 的泛型设置实现和 MOD 的补丁组合；已有 Harmony/WhySoLaggy 机制把问题放大为调用时异常。

本次更具体的组合是：ModConfig 1.8.2 的泛型按键名称方法，ShootZombies 1.4.0 经非泛型子类解析并挂本地化补丁，再由启用了 EnablePatchProfiling 的 WhySoLaggy 对已补丁目标加计时，要求注入 __originalMethod，触发错误的句柄解析。设置列表因此中止。

不能确定用户第一次出现此问题的日期，也不能确认旧会话计时配置值；没有旧版游戏的受控实机回放。以下区分静态证据、隔离复现与用户实机证据。

## 游戏版本对比

对比本地完整反编译的 PEAK 2.3.a、2.4.b、2.6.b、3.0.0、3.0.1、3.1.0：去除 Token/RVA 注释和空白后，六版 mscorlib/System.Reflection/MethodBase.cs 内容完全相同。单参数 GetMethodFromHandle 遇到泛型声明类型时主动抛出同一异常，早已存在。

3.0.1 → 3.1.0 的 Assembly-CSharp/SettingsUICell.cs 仅新增 m_image、获取子 Image 并设置 SettingsColors.BoxColor，以及相应 using；没有改变上述反射句柄逻辑。LocalizedText.cs 在 3.0.0、3.0.1、3.1.0 的归一化内容相同。没有发现把本次异常归因于游戏新 UI 或本地化 API 的证据。

证据：.build/whysolaggy-modconfig-3.1.0-20261113/history-probe/game-source-comparison.json。这里只覆盖故障相关类，不是六版游戏的全量兼容认证。

## ModConfig 历史：关键分界为 1.7.0

从 r2modman 本地缓存读取实际 DLL，用 Mono.Cecil 查看类型与方法声明；没有安装或替换这些 DLL。

| 缓存版本 | 按键名称实现 | 本次特定风险 |
| --- | --- | --- |
| 1.6.0、1.6.1 | 非泛型 BepInExKeyCode 自己声明 GetDisplayName | 这条按键泛型继承路径不存在，不代表整个旧版没有其他问题 |
| 1.7.0 | 改为继承 BepInExInputBindingSetting<KeyCode>，名称方法在泛型父类 | 已具备本次触发结构 |
| 1.8.0、1.8.1、1.8.2 | 同样的泛型父类结构 | 风险继续存在，需与计时/补丁目标组合判断 |

这不是说 ModConfig 1.7.0 单独就会报错：它的泛型设计需要被补丁方正确处理。保存的 2026-09-21 日志已加载 ModConfig 1.8.2，说明本次并非首次换到 1.8.2。

缓存根：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/cache/PEAKModding-ModConfig/。
详细 DLL 路径、结构和 SHA256：history-probe/modconfig-history.json。

## WhySoLaggy 与 Harmony 也不是此次新增

- 检查发行 1.0.3、1.0.4、1.0.5 的真实 DLL：均枚举 Harmony.GetAllPatchedMethods，TimingPrefix/TimingPostfix 均要求 MethodBase __originalMethod。此机制早于 3.1.0。
- 当前安装 WhySoLaggy DLL 与 2026-09-21 最终发行记录哈希一致：946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550。
- 2026-10-23 PEAK 3.0.0 审核保留的 0Harmony.dll、测试环境副本、当前安装副本 SHA256 都是 1A21CC03424FC82C3DD1346905D16494536B9595AE4162228D99FB7C285C1031，程序集版本 2.9.0.0。
- 1.0.3 起发行说明已注明补丁计时默认关闭，当前源码也默认 false；本次实际配置 true。未保存旧配置，不能断言用户此前一定是 false 或刚刚开启。
- WhySoLaggyPlugin.cs:415 的 Profiling active! 是无条件启动末尾日志，不能仅靠这句话证明 EnablePatchProfiling 开启。

## 为何本次会暴露：比之前更明确的证据

本次 BepInEx-reproduced.log：

- 第 81 行加载 ShootZombies 1.4.0。
- 第 85–86 行，Harmony 明确警告 ShootZombies 正在补丁按键泛型父类的继承方法，应改为实际声明方法。
- 第 164–165 行，WhySoLaggy 对相同 KeyCode/string 路径发出同类警告。
- WhySoLaggy-reproduced.log 确认计时扫描启用，646 个方法挂钩；Player-reproduced.log 有 8 次实际调用失败。

ShootZombies 反编译 Plugin.cs:14959–14975 的 PatchModConfigSettingOptionMethods 遍历具体类型，直接 AccessTools.Method(type, GetDisplayName) 后 harmony.Patch，没有像四个自有 MOD 的新适配器那样向上寻找实际声明方法。四个自有 MOD 在 2026-09-21 已修正自己的目标解析，但 WhySoLaggy 的全局计时扫描仍会遇到其他来源留下的继承目标。

2026-09-21 保存的 first-build-game-session.log 加载 WhySoLaggy 1.0.5 / ModConfig 1.8.2，但没有任何 ShootZombies 加载记录。它还以 Unable to start Unity log writer 开头，因此旧 BepInEx 日志没有异常不能证明旧 UI 完全正常；也没有旧配置和对应完整 Player.log 用于受控对照。

修正先前结论：最初报告把 ShootZombies 的告警统一归为独立问题，这不够准确。ShowSettings(string) 缺失仍是另一兼容问题，但 GetDisplayName 的继承方法告警与这次根因链直接相关。其作用是提供不安全目标；WhySoLaggy 的计时注入把该目标变成调用时异常，不能简单归咎于中文翻译本身。

## 隔离复现：不需要加载任何游戏程序集

程序：.build/whysolaggy-modconfig-3.1.0-20261113/history-probe/GenericPatchProbe.cs。
使用本机 .NET Framework 编译器及上述同哈希 Harmony/MonoMod；模拟非泛型子类继承泛型父类 GetDisplayName，本地化 Postfix 和 WhySoLaggy 同签名计时回调。每个组合使用新进程，避免残留补丁。

| 组合 | int 与 string 两种泛型实参 |
| --- | --- |
| 正确的父类声明目标 + 计时 | 均成功，计时回调实际执行 |
| 子类反射得到的继承目标 + 计时 | 均抛同类 GetMethodFromHandle 泛型异常 |
| 先继承目标、再声明目标，再遍历全部目标加计时 | 均失败 |
| 先声明目标、再继承目标，再遍历全部目标加计时 | 均失败 |
| 继承目标只挂本地化，不加计时 | 均成功 |
| 单个继承目标存在，但计时改用父类声明目标 | 均成功 |

12 个组合调用中，6 个预期成功，6 个预期失败；结果是机制验证，非生产测试全部通过的说法。完整输出 results.txt。混合场景在 .NET Framework 中观察到两个 MethodInfo 记录；不能据此假设 Unity/Mono 内部字典行为完全相同，也不能把单目标归一化成功当作复杂实机修复已完成。

该实验能确认：错误依赖泛型继承目标与计时注入组合，不依赖 PEAK 3.1.0。结合历史源码/依赖可推断旧游戏同样可能触发，但没有宣称实际启动旧游戏复现。

## 后续建议与边界

修复仍优先落在 WhySoLaggy：保护观测对象、规范化和去重计时目标、避免不安全的 __originalMethod 注入，保留共享翻译。可以另外修正 ShootZombies 的继承目标解析，但只修一个外部来源无法保证以后没有其他 MOD 触发。

暂不需要因本调查退回游戏、降级 ModConfig 或删除语言功能。最终还需当前完整 profile 的受控 A/B 和实机多语言/按键设置回归。此次只增加诊断文件与 memory；未修改源码、配置、安装、发行或版本。
