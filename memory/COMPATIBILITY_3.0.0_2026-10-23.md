# PEAK 3.0.0 开发区兼容性审核

## 2026-10-23 PlayersInfo 复核补充

[PlayersInfo 专项报告](mods/PlayersInfo/COMPATIBILITY_3.0.0_2026-10-23.md)：补查 2.4.b → 3.0.0，0.2.5 暂无确定的强制 API/核心读数适配项；新增昵称读取顺序维护建议，并保留 3.0.0 实机验收。terrain 中 0.1.1 仍需更新。仅更新审核记录，源码及部署未改。

审核时间：2026-10-23（Asia/Shanghai）。本轮范围为查看是否需要更新；未改插件源码、测试源码、csproj、配置、版本或发行文件，未部署 DLL、未启动游戏。

## 结论

- **需要功能适配**：ItemInfoCN、Lantern_ShootZombies_Night、TerrainCustomiserCN（含依赖它的 Collector）。前两者的问题在 2.6.b 已存在，不能全部归因于 3.0.0。
- **需要测试维护**：WhySoLaggy 的 RPC 测试把反编译目录写死为 2.4.b；插件本身构建通过，现有 63 个 watched RPC 均仍存在于 3.0.0。
- **暂未发现必须修改的编译/API 断点**：PlayersInfo、WhereIsThing、WhereIsMyAmulet、StateKeeper、WhySoLaggy、PeakMapBrowser、ModConfigDiagnostics、UprightCockroach。此结论不是 Unity 实机、跨平台或第三方依赖完整验收。
- **安装状态另有旧版本风险**：terrain profile 仍有 PlayersInfo 0.1.1 和 ItemInfoCN 1.0.0，其旧 DLL 已发现失效成员。开发区新代码通过不等于所有 profile 已更新。

## 基线与方法

- 当前参考：引用参考代码/反编译/3.0.0/；对照：引用参考代码/反编译/作废/2.6.b/。
- 编译实际引用 Steam 安装的 C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/Managed/Assembly-CSharp.dll：1,648,128 bytes；SHA256 E9A8585ABFA566AFA7A3EF3E3699BC27369039EEEFCDB3CAA38D624D56F59E5C；MVID b5521262-0ce1-4a6c-a2bb-f18e027e413e。
- Assembly-CSharp 反编译 C# 文件 1149 → 1155；去掉整行注释与空白后，75 个已有文件变化、6 个新增、0 个删除。文件变化数不等于 API 破坏数。
- 11 个活动 MOD、12 个插件工程（TerrainCustomiserCN 含 Collector），另运行 4 个现成测试工程。PeakMapBrowserPrototype 是 HTML/CSS/JS 原型，不作为游戏 DLL 编译。
- DreamyAscent、OldPC、PeakWorkshopCN、PeakWorkshopCNScanner 位于作废区，未构建、部署或恢复维护。
- 全部审核输出到独立 .build/compat-3.0.0-20261023/，没有使用项目默认的 profile 输出目录。
- 8 个构建成功 DLL 的 1,370 个游戏/Unity/Photon 等当前安装程序集成员引用均可解析；23 个静态 Harmony 目标均找到。另核对源代码显式目标、关键反射字段及 WhySoLaggy RPC 白名单。动态类型、第三方 MOD 和完整补丁执行仍需实机。

## 构建与项目判断

| 项目 | 版本 | 隔离 Release 构建 | 判断 |
|---|---|---|---|
| ItemInfoCN | 1.0.0 | 失败：3 个 CS1061 | 必须修曼德拉草读取，发行 DLL 还需更新旧 StatusField 引用 |
| Lantern_ShootZombies_Night | 0.2.1 | 失败：12 个诊断（8 个旧字段引用、4 个连带变量错误） | 必须适配背包、点灯补丁及联网同步 |
| TerrainCustomiserCN | 0.1.2 | 失败：缺 UBImGui / PhotonCustomPropsUtils | 另有确定的旧生成/光照 API 断点，不能只补依赖 |
| TerrainCustomiserCNCollector | 0.1.1 | 主项目依赖构建失败 | 随主项目适配；未单独证明 Collector 运行通过 |
| PlayersInfo | 0.2.5 | 0 警告 / 0 错误 | 暂无确定强制断点；昵称显示建议维护，3.0.0 实机待验；旧 terrain 0.1.1 需更新 |
| WhereIsThing | 0.1.2 | 0 警告 / 0 错误 | 绳索/投掷者/生成池反射入口仍在；多人和晚加入需实机 |
| WhereIsMyAmulet | 1.0.4 | 0 警告 / 0 错误 | ScoutStatue.hasAmulets 仍在；显示/字体需实机 |
| StateKeeper | 0.1.0 | 0 警告 / 0 错误 | 核心采集/复盘 API 无断点，跨平台身份范围见下 |
| WhySoLaggy | 1.0.5 | 0 警告 / 0 错误 | 插件暂不需 API 修复；测试路径要更新 |
| PeakMapBrowser | 0.1.1（csproj） | 0 警告 / 0 错误 | 游戏 API 无断点；账户/API 线上联调未测，程序集版本仍 0.0.0.0 |
| ModConfigDiagnostics | 0.1.0 | 0 警告 / 0 错误 | Unity 引用无断点；第三方 ModConfig 运行环境需实机 |
| UprightCockroach | 0.1.0 | 0 警告 / 0 错误 | Character.Start 和渲染 API 仍在；骨骼、材质、视角/生命周期需实机 |

## 确定需要适配的项目

### ItemInfoCN

- Plugin.Processing.cs:140-142 仍访问 Mandrake.aoe，当前无此字段，构建失败。2.6.b 的 Mandrake 也已经没有 aoe，属于遗留兼容问题。
- 发行/terrain 已安装的 1.0.0 DLL 另外引用旧 StatusField.additionalStatuses 的 List 类型、StatusFieldStatus.statusAmountPerSecond/statusType；当前程序集无法解析这些旧签名。修源码后继续使用旧发布 DLL不能解决。
- 建议按当前 Mandrake 的 TimeEvent/实际效果对象读取范围与状态，避免用 nearPlayersDistance（玩家接近检测）直接当效果半径。同步核对 StatusField 与描述解析后再重新构建发布。

### Lantern_ShootZombies_Night

- Helpers/ItemSpawnHelper.cs:109,130,140、Helpers/ExtraLanternPurger.cs:193、Helpers/LanternHelper.cs:188,515,602、Patches/BugleUltimatePatch.cs:100 访问已删除的 BackpackSlot.hasBackpack。
- 当前背包用 backpackType 和 IsEmpty()；需要区分普通背包、腰包、喷气包/火箭包，再读取有效容器数据，不能把“有背部装备”等同于“有背包物品格”。
- Helpers/ReflectionCache.cs:121,140 把 Lantern.lit 当字段；当前是私有属性，FieldRef 与 GetField 两条路径均失效，读取会回退 false，写入可能不生效。
- Patches/LanternFuelOverridePatch.cs:134 仍补丁 LightLanternRPC，Helpers/LanternHelper.cs:205 仍向其发送 RPC；当前已无此方法。实际入口为 LightLantern(bool)，点亮状态经 OnPhotonSerializeView 同步。
- **不能只把 RPC 名改成 LightLantern**：新入口不是旧 PunRPC，必须按当前物品 authority/序列化机制处理，并保留本地燃料权威、备用池、补燃和双端行为。
- 这些 Lantern / Backpack API 在 2.6.b 已是当前形态，属于旧欠账。3.0.0 Campfire 的载入同步/skipMoraleBoost 又有行为变化；适配后要补存档载入、篝火补燃和主客机复测。
- 发行/0.2.1 当前只有 README、CHANGELOG、manifest、icon，未发现 DLL；没有将旧 0.2.0 当作 0.2.1 最终产物。

### TerrainCustomiserCN / Collector

- 两项目引用 terrain profile 的 UBImGui / PhotonCustomPropsUtils，当前路径均缺失，构建被阻断。
- 主工程游戏引用为本地 refs/Assembly-CSharp-publicized.dll：1,417,216 bytes、文件时间 2026-05-22，与 3.0.0 游戏 DLL不同。补回依赖后编译成功也不能视为兼容。
- 0.1.2 发布 DLL 有 **23 个无法解析成员引用**，集中于 LightVolume、BakedVolumeLight、LevelGenStep.Execute、PropGrouper.RunAll、PropSpawner_Sphere 和 PropSpawner.SpawnDecor。
- GenerationPatches.cs:189,206、PropGrouperHelpers.cs:55,72 把 Execute 当同步 void 调用；当前 LevelGenStep.Execute() 返回 IEnumerator，并有独立 ExecuteImmediately()。忽略 IEnumerator 不会执行生成体。
- 当前 PropGrouper.RunAll(bool,bool) 返回 Awaitable；旧代码仍按 void RunAll(bool) 和旧 deferred 顺序接管。需要按新生成生命周期适配，不能只改返回类型凑编译。
- 需先建立当前版本 publicized 引用与依赖基线，再适配生成/光照路径；保持既有地图序列化类型、Photon 属性键和中文版本边界。

## 测试与运行风险

- **WhySoLaggy：22 通过、1 失败、0 跳过。** WatchedRpcNames_AllExistInPeak24bPunRpcSet 在 CoreBehaviorTests.cs:363，目录查找函数在 :410 起，写死反编译/2.4.b，目录已归档。独立检查找到 3.0.0 全部 **63/63** watched RPC；PhotonNetwork.ExecuteRpc(Hashtable,Player) 和 ZombieManager.zombies 仍在。未改测试源码，原始失败保留。
- **StateKeeper：78 通过、0 失败、1 跳过，共 79。** 跳过 LatestRecordingReadOnlyReportRegression，因硬编码的可选本地录像不存在；不宣称 79 项全通过。20 个静态补丁目标仍存在。
- **UprightCockroach：14 项配置检查 + 49 项模型/配置检查通过。** 当前资源 24 网格、59,440 顶点；仅验证离线数据和配置，不代替游戏外观验收。
- PlayersInfo 的 StaminaBar.Update / GUIManager.Start、反射字段 isInvincible/popOutTime；WhereIsMyAmulet 的 hasAmulets；WhereIsThing 的 attachedToAnchor/lastThrownCharacter/GetSpawnPool 均仍在。
- **StateKeeper 跨平台限制（条件风险）**：采集使用 Photon owner.UserId，跨局比较只接受 ValidSteamId 的 17 位 Steam ID。3.0.0 增强跨平台认证与哈希 ID 处理，非 Steam/哈希身份会被当前比较器排除。若需要跨平台跨局比较，应单独设计带平台命名空间的稳定身份。Steam 认证源码仍设置 Steam ID，不能宣称所有 Steam 用户已变成哈希或采集全面失效。
- 3.0.0 新认证、房间码/Mod 状态提示、复活后延迟状态推送、暂停菜单和 Campfire 行为需要联合实机验证。本轮未启动 PEAK，未确认远端认证实际返回值。

## 安装侧附加发现

- terrain/BepInEx/plugins/WUYACHIYU-PlayersInfo/PlayersInfo.dll 为 0.1.1，含失效 BackpackSlot.hasBackpack、ItemSlot 的旧 data/prefab 成员签名；使用该 profile 前应同步已经适配的版本。
- terrain 的 ItemInfoCN 1.0.0 与发行 DLL有同类失效字段。
- 2.0.a profile 名称只是目录名，不代表游戏版本。当前检查到其中 PlayersInfo 0.2.5、StateKeeper 0.1.0、WhereIsThing 0.1.2、WhySoLaggy 1.0.5 的直接游戏成员引用可解析。
- 未自动恢复缺失插件、启用作废项目、复制候选 DLL或覆盖发行目录。

## 建议顺序

1. 若在使用灯笼整合 MOD，优先修背包、点灯反射、点灯同步三个断点，并做主客机补燃验证。
2. 修 ItemInfoCN 的 Mandrake / StatusField 读取，再验证实际物品提示。
3. WhySoLaggy 更新测试基线，避免把路径失败当运行时 RPC 断裂。
4. TerrainCustomiserCN 独立做兼容迁移，先补依赖/当前引用，再验证生成与存档。
5. 构建通过的 8 个工程暂不因版本号变化强行重写，仍需各自行为复核和 3.0.0 单人/多人回归；PlayersInfo 昵称显示有维护建议，不能笼统认定免更新/免验收。

## 原始证据

全部位于 .build/compat-3.0.0-20261023/：

- build-results.json 与每项目 *.build.log：12 次构建。
- source-diff.json、各类 *.diff.txt：2.6.b → 3.0.0 差异。
- metadata-results.json、game-identity.json、game-api.json：DLL 身份和成员/静态补丁审核。
- dynamic-checks.json、source-patches.json：68 个显式源补丁目标、关键反射和 RPC 名检查；1 个缺失 LightLanternRPC、1 个外部依赖目标待验，其余名字存在，不等于全部动态补丁实装验证。
- StateKeeper.test.log、WhySoLaggy.test.log、RoachConfig.test.log、RoachModel.test.log、tests/results/*.trx：现成测试结果。
- before-hashes.json / preservation.json：240 个既有源码、工程及匹配到的部署 DLL 的哈希前后无变化。审核脚本和产物为本轮新增。

本轮生成审核产物并更新本地 Markdown 记忆；无专用外部 update_memory 工具，不宣称写入外部记忆服务。
