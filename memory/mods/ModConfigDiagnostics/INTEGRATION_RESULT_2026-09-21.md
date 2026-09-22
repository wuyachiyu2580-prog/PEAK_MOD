# 四 MOD ModConfig 更新结果

日期：2026-09-21。源码、版本链、构建、自动测试及 profile 部署完成；按后续指示备齐四个新版发行目录（无 ZIP、未上传），最终 DLL 的完整实机矩阵待验收。

## 已完成

- PlayersInfo：删除全局 RefreshCache 和 ProcessModEntries 等重注册路径；按游戏当前语言识别简中/繁中，订阅语言变化，仅更新自身配置描述和 UI。
- WhySoLaggy：以静态 Action 字段订阅/退订语言通知；移除以通用分区识别 MOD 的方式，修正继承方法 Harmony 补丁目标。
- 四 MOD：新菜单适配、声明方法去重、按配置文件/section/key 隔离标题和选项、仅修改枚举显示、合并可见 UI 刷新、保护自身配置行 LocalizedText。
- 各项目独立编译 ModConfigUiAdapter，无新增共享 DLL；ModConfig 为 SoftDependency，缺失时跳过接入。
- 保留四 MOD 的业务逻辑与已有未提交修改；两个定位 MOD 的版本和 20000 Canvas 排序保持不变。Lantern 和公共依赖源码未修改。

## 构建与产物

四个工程 Release 均为 0 warnings / 0 errors。沿用项目默认输出，以下 DLL 与各自 obj/Release 构建产物 SHA-256 一致。

| MOD | AssemblyVersion | 字节 | SHA-256 |
|---|---|---|---|
| PlayersInfo | 0.2.5.0 | 103936 | `68D7B4BB25CD0C16F2DAE544BD683E174DE5E800D7F67528945C0A364B905FE0` |
| WhySoLaggy | 1.0.5.0 | 160256 | `946168D48B36DF3D8733C38FE2BDA6C807D7007337167AA5529F816FD2DBD550` |
| WhereIsThing | 0.1.2.0 | 164352 | `CF1C9043F74E843032CB65F972137EA135297D690EA9E397929977AAB889BF63` |
| WhereIsMyAmulet | 1.0.4.0 | 44544 | `70C24B097F1B943C80746DF64C8B6AD28372758238B915FA942A2EFE10860F04` |

输出目录：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/`。

旧版发行目录与 ZIP 原样保留；当前另建 0.2.5 / 1.0.5 / 0.1.2 / 1.0.4 发行目录，详情见下方发行文件记录。

## 测试证据

- WhySoLaggy.Tests：23 项通过，0 失败，0 跳过。已补 Microsoft.NET.Test.Sdk 17.11.1，保证实际执行，而非仅编译。
- 最终部署 DLL 元数据核验：四者均包含 ModConfig SoftDependency（标志 2），无 ModConfig/PEAKLib 程序集硬引用；部署文件 SHA-256 与上述清单一致。
- 直接读取 ModConfig 1.8.2 DLL 元数据确认 ConfigBase getter 为 Assembly 可见性；适配器使用 Public | NonPublic，新增动态生成非公开接口的回归并通过。
- 新回归覆盖：同 section/key 不同配置文件隔离（含文件名后缀碰撞）、显式接口取配置、六次枚举显示切换不改原始选项/值、Action 字段重复订阅/退订保留其他监听、闭合泛型声明方法解析/缺成员降级。
- 更新原分类测试，拒绝 General/UI/Logging 等作为 MOD 身份；程序集版本断言改为 1.0.5；RPC 名单测试使用现有 PEAK 2.4.b 反编译基线且通过。
- 本次 23 项是 WhySoLaggy 测试项目执行结果；其中适配器行为测试覆盖相同实现。另三 MOD 的映射/UI 并未因此获得完整实机认证。
- TRX：`.build/modconfig-update/test-results/modconfig-20260921.trx`；构建清单：`.build/modconfig-update/artifacts-final.json`。

## 实机证据边界

- 用户中途启动游戏，首次构建的四个 DLL 在日志中按 0.2.5 / 1.0.5 / 0.1.2 / 1.0.4 加载；该日志未发现 ModConfig localization 报错或 only patch implemented 警告。副本：`.build/modconfig-update/first-build-game-session.log`。
- 这份日志对应首次构建，不能证明随后非公开接口读取、协程清理/异常防护修订后的最终 DLL 已完成 UI 验收。游戏退出后最终构建才成功覆盖 profile。
- 尚待：中英繁中反复切换、展开下拉切语言、DEFAULT/外部配置更新、搜索/五类筛选切页、多 MOD 互不改写、无 ModConfig 加载、切场景/卸载，以及自身活动标题 LOC: 0 复测。
- 历史旧 ModConfig 类型回退仅为兼容防护，本轮没有旧 DLL 实测。
- 完整 profile 若加载未修的 Lantern，其全局重注册风险仍存在；本轮不能宣称所有来源已消除。

## 后续入口

实施设计与完整验收表：`INTEGRATION_PLAN_2026-09-21.md`。源码修改无需再次授权；四个新版发行目录已按后续指示准备完成；下一步以最终 DLL 的实机反馈为准，不自动生成 ZIP、上传或修改 Lantern。


## 发行文件记录（后续用户指示，2026-09-21）

用户要求编辑四 MOD 的发行文件，并明确不打 ZIP；此前非 ModConfig 的已实现改动也纳入本版说明。此指示覆盖旧 memory 中“暂不更新发行目录”的限制，不代表实机验收或公开发布已完成。

| MOD | 新版发行目录 | 本版说明覆盖 |
|---|---|---|
| PlayersInfo | `MOD开发/PlayersInfo/发行/0.2.5` | ModConfig、语言识别；保留既有队友 HUD 使用说明 |
| WhySoLaggy | `MOD开发/WhySoLaggy/发行/1.0.5` | ModConfig、语言监听/补丁；核对诊断配置与日志说明 |
| WhereIsThing | `MOD开发/WhereIsThing/发行/0.1.2` | 放置物/owner、预设、分类、扫描/标签性能、窗口/排序、ModConfig |
| WhereIsMyAmulet | `MOD开发/WhereIsMyAmulet/发行/1.0.4` | TMP/材质/缓存、排序、投影与字号、ModConfig |

每个目录仅含本 MOD DLL、icon.png、README.md、CHANGELOG.md、manifest.json。DLL 复用上表最终产物；图标沿用上一版。README 和本版 CHANGELOG 已按用户纠正恢复英文，保留上一版章节、表格、图片位置和版本标题风格；各 README 明确加入对应版本 What's new，CHANGELOG 保留全部历史章节，开发目录 CHANGELOG 同步。manifest 沿用上一版格式并更新必要字段。

manifest 版本与插件/工程/程序集版本一致，description 不超过 135 字符，仅保留 BepInExPack 必需依赖；ModConfig 仍为可选。旧目录（含 ZIP）未修改，新目录未生成 ZIP，未上传。验证明细写入 `.build/modconfig-update/release-verification.json`。

英文修订校验通过：各 README 和 CHANGELOG 均含当前版本更新，原 README 章节顺序及历史 CHANGELOG 保留；各目录仍仅 5 个文件，DLL/图标及旧版目录与本轮修订前的 116 个受保护文件逐一哈希一致。未重新编译、未生成 ZIP，完整实机验收状态不变。
