# WhySoLaggy ModConfig 修复实施记录

日期：2026-11-13。用户在下一步调研后明确回复“开始”，授权实施。代码修复、自动回归、隔离构建和单份 profile 部署已完成；实机 UI 验收尚未完成。版本暂保持 1.0.5 / 1.0.5.0，发行/1.0.5 保持原样。

## 实施内容

- ProfilerTargetResolver.cs：将继承目标重新定位到实际声明的闭合类型，保留构造函数、重载、重写和构造泛型参数；按 module MVID/token/声明类型/方法实参建立唯一身份。拒绝开泛型、抽象、无托管方法体等不支持目标并记录原因。
- PatchProfiler.cs：合并目标和外部 owners、应用兼容忽略规则，只挂一次计时。Prefix 移除未用的 __originalMethod，Postfix 在规范化目标上保留原方法参数并查询注册时缓存，不再每次拼接方法签名。
- 专属 Harmony id 为 com.wuyachiyu.WhySoLaggy.PatchProfiler，停用只撤该 id；不会撤 root id 的语言/RPC/其他诊断补丁。安装失败撤回本目标的计时补丁，清理失败保留句柄供再次清理并停止采集。
- 统计使用短锁更新/快照，IO 在锁外执行；启停 session generation 防止旧的在途调用污染重新开启后的统计。普通方法异常仍向调用者传播，不用 finalizer 吞业务异常。
- DeferredProfilerSwitch.cs：配置事件仅排队，在 Unity Update 主线程应用；处理启动延迟期间的变更、重复状态和销毁取消。EnablePatchProfiling 开关在启动初始化之后的下一帧应用，退出时取消订阅并先清理计时。
- IgnorePatchMethods 和采样阈值在下一次开启时重新读取；没有宣称所有配置均实时生效。
- 最后统一更新中英文说明：实时开关只影响耗时采集、忽略列表延后生效、低耗时采样不等于完全不统计。继续沿用现有简中/繁中共用中文文本策略；未新增独立繁体字库或更改配置 section/key。
- 开发 CHANGELOG 增加 Unreleased；没有改发行包或擅自升版。

## 测试证据

目录：.build/whysolaggy-fix-20261113/results/。

- before-fix.trx：真实 PatchProfiler 的新增泛型组合 4 项，2 通过、2 失败；失败为与用户日志一致的 GetMethodFromHandle 泛型声明异常。
- core-fix.trx：同 4 项全部通过。
- final-deploy.trx：最终完整回归 37 通过，0 失败，0 跳过。Release 0 warnings / 0 errors。
- 新增真实补丁回归覆盖：int/string 按键泛型结构、两种挂钩顺序、第三方后续补丁、翻译保留、单次计数、三轮启停、忽略名单、部分安装失败回滚及重试、后台四线程 8000 次调用与并发快照、停用前在途调用丢弃、递归/原异常、泛型方法实际调用和身份解析、配置变更主线程排队。
- 本地化测试覆盖最终说明和多次切换不改变 section/key/value。已有 ModConfig 所有权隔离和 Action 订阅测试一起通过。
- 原 CoreBehaviorTests 的 2.4.b 硬编码路径改为明确 3.1.0 默认基线，也可由 PEAK_DECOMPILED_ASSEMBLY_CSHARP 指定；保留全部 RPC 名称断言，无静默跳过。

边界：测试运行于本机 .NET Framework，不是 Unity/Mono 实机。测试中额外尝试“泛型类内构造泛型方法”的端到端挂钩，底层 MonoMod 在外部测试补丁挂载/卸载时出现 invalid generic instantiation；最终保留其身份/参数解析测试，并用普通类构造泛型方法覆盖实际计时调用。不能宣称任意双重泛型底层 detour 已验收。仅抛出且没有返回分支的测试目标同样触发旧 Harmony IL 编译限制，异常传播回归改用有正常返回分支的真实条件异常场景。中间失败 TRX 保留，不作为最终通过结果。

## 部署

游戏未运行时，于 2026-11-13 23:23 完成部署。

- 当前文件：C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/WhySoLaggy.dll。
- DLL 版本 1.0.5.0，166912 bytes，SHA256 1C04AAF283EE30E9B3CAC923A5C1A431ECE1B46F843AA149B9C4BC2CFA405807。
- PDB 同步到 plugins/WhySoLaggy.pdb。
- 原 plugins/WUYACHIYU-WhySoLaggy/WhySoLaggy.dll 已移出活动加载目录并保存；递归确认活动 WhySoLaggy.dll 只有一份。
- 配置未改，SHA256 9842FF91345A68FB49B04C1B030E26468D0EA6C41688A44845C4683EBBD93695；EnablePatchProfiling 原值仍 true。
- 隔离构建产物：.build/whysolaggy-fix-20261113/tests/WhySoLaggy.dll。
- 部署清单：.build/whysolaggy-fix-20261113/deployment.json。
- 原 DLL/配置备份：.build/whysolaggy-fix-20261113/backup/。原 DLL SHA256 946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550。
- 发行/1.0.5/WhySoLaggy.dll 仍为原哈希，未修改发行文件或 ZIP。同版本号下区分本次修复构建须以哈希为准。

构建命令：dotnet build MOD开发/WhySoLaggy/WhySoLaggy.Tests/WhySoLaggy.Tests.csproj -c Release -p:OutputPath=<workspace>/.build/whysolaggy-fix-20261113/tests/ -p:AppendTargetFrameworkToOutputPath=false --no-restore。
测试命令：dotnet vstest <isolated>/WhySoLaggy.Tests.dll /Logger:trx;LogFileName=final-deploy.trx /ResultsDirectory:<results>。

回滚应在游戏退出后进行：将备份旧 DLL 替换 plugins 根目录当前 DLL，移出本次 PDB，保留单份活动 DLL；不要恢复子目录后又留根目录副本。配置本轮未改，不需要为回滚覆盖用户后续的新配置。

## 实机阻碍与待验

按 computer-use 技能查到当前 r2modman 3.2.21 的 2.0.a 窗口；两次截图捕获分别 FrameArrived/window capture timeout。文本辅助树可读，但游戏 UI 需要可靠画面。管理器文本残留“PEAK is launching via Steam”，实际窗口/进程检查没有 PEAK。未点击启动按钮或重复启动，也未操作其他 MOD 开关、加入房间或改语言/按键。

因此未执行完整 profile 的实机 A/B，不声称 LOC: 0 已在当前游戏画面验证消失。用户下次启动原 2.0.a profile 即会加载本次修复 DLL。

下一次实机重点：

1. 保留 ShootZombies 和现有补丁计时开启值，打开 WhereIsThing、WhereIsMyAmulet、PlayersInfo、WhySoLaggy，确认行和控件完整。
2. 计时关/开三轮，检查翻译/RPC 保留、无重复计时；忽略列表按说明下次开启生效。
3. 中英繁、搜索筛选、按键保存、场景和联机后台线程检查。
4. 查 Player.log 是否再出现泛型句柄异常，并保存 WhySoLaggy/harmony 快照；不能仅看缺少 Unity 日志写入的 BepInEx 日志。

## 接续状态

本条覆盖同日此前“只调研/未实施”的当前状态。已修代码、37 项自动测试通过并部署；未完成实机或准备新发行版。其他项目既有改动保留，未提交 Git。仅同步本地 Markdown memory，不声称同步独立记忆服务。
