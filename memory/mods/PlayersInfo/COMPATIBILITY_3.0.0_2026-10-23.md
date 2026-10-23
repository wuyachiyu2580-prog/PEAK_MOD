# PlayersInfo 3.0.0 专项复核

日期：2026-10-23。当前开发/发行版本仍为 0.2.5。本轮只复核和记录，未修改插件源码、配置、版本、发行或安装 DLL，未启动游戏。

## 结论

- 0.2.5 暂未发现升级引起、必须修改的编译/API 或核心读数断点。不能扩张为“不需要检查”或“已完整兼容 3.0.0”。
- 建议维护项：队友昵称优先读取游戏处理后的 Character.characterName，异步结果就绪前使用合理占位，随后沿用低频刷新。当前优先 Owner.NickName，可能与原版显示不一致。这是既有一致性问题，在 3.0.0 跨平台处理路径下值得修正，不是新发现的必崩断点。
- 安装版本另判：2.0.a profile 的 0.2.5 与发行文件哈希一致；terrain 仍为 0.1.1，已确认旧背包/物品槽成员失效，使用前需更新安装版本。
- 仍需 3.0.0 实机验收：UI 资源层级、主客机、晚加入/重连、死亡/复活/观战、石化/无限体力、背包/腰包/喷气包，以及可选 ModConfig 菜单。

## 补查证据

初审主要对比 2.6.b → 3.0.0。本次补查 PlayersInfo 实际开发基线 2.4.b → 3.0.0 的 35 个相关游戏类（初始 33 个匹配类，加 CharacterSyncer/InventorySyncData），区分反编译语法差别与行为变化。

1. 既有隔离 Release 构建 0 警告/0 错误；当前安装 0.2.5 的 268 个成员引用可解析；GUIManager.Start / StaminaBar.Update 两个静态 Harmony 目标仍在。静态检查不代替 Unity 运行。
2. StaminaBar、BarAffliction、ThornOnMe、BackpackSlot、CharacterSyncer 去掉注释、空白和 base. 语法差别后相同；CharacterSyncData 相同；状态枚举未新增/删项。
3. Character.GetMaxStamina 仍按 1-statusSum；额外体力仍按 1-petrifyAmount*0.01 限制。PlayersInfo 读取对应目标角色，石化上限处理匹配，未查到必须重写体力/石化显示的证据。
4. 0.2.5 背包已使用 IsEmpty、prefab/visuals.slotCount 和背包类型兜底；喷气包读 UseRemainingPercentage，回退 Fuel/100。其他 MOD 的旧 hasBackpack 错误不能套用到此版本。
5. 饥饿步进和自动衰减相关公式未查到升级断点；倒计时仍是估计值，不证明冻结/倒地等所有情形都精确。
6. 3.0.0 复活增加 ForcePushAfflictions 和 ForcePushStatus(2f)。PlayersInfo 读取既有同步数据，没有证据要求主动发 RPC；复活后的刷新延迟应实机观察。
7. CharacterData.userID 从 ulong 改为 string，但当前名册/条目身份使用 Photon ViewID/ActorNumber，并不读取该 userID 字段，不能判为身份绑定必坏。

## 昵称显示建议

- MOD开发/PlayersInfo/PlayersInfo/MonoBehaviours/TeammateBarDriver.cs:833-846 的 SafeGetName 首先返回非空 Owner.NickName，之后才回退 characterName；:241 每 0.5 秒刷新。
- 引用参考代码/反编译/3.0.0/Assembly-CSharp/Character.cs:336-347 先判断是否同平台，再决定直接赋值或执行 FilterProfanity，把结果写入 characterName；原版 GUIManager 观战名读取 characterName。
- 在处理结果与原始昵称不同时，当前队友 HUD 将一直显示原始昵称。2.4.b 也已有 FilterProfanity，不能说此问题始于 3.0.0。本次只记录建议，未改显示逻辑。

## 产物和实机边界

- 发行/0.2.5/PlayersInfo.dll 与 2.0.a/BepInEx/plugins/WUYACHIYU-PlayersInfo/PlayersInfo.dll 的 SHA256 均为 68D7B4BB25CD0C16F2DAE544BD683E174DE5E800D7F67528945C0A364B905FE0。
- terrain 的 0.1.1 SHA256 为 8E3D4BBD9D1EFC3E3A51D3D80C7DBFC4F638166D608CDEA7058BCE6519314135；失效成员见初审 metadata-results.json。
- 现有 2.0.a 日志修改时间 2026-10-18，可见 0.2.5 加载和 GUIManager.Start 初始化；不能证明本次 3.0.0 全功能验收。
- 未解析新游戏 UI prefab 序列化层级，未执行多人/跨平台会话；源码相同不等于资源布局和运行时均无变化。
- 差异证据：.build/compat-3.0.0-20261023/playersinfo-detail/；初审核验见上级 PlayersInfo.build.log、metadata-results.json、dynamic-checks.json。

本报告收窄总审核中“其余工程沿用当前源码”的判断：可暂不因版本号强改源码，但保留昵称维护建议和 3.0.0 验收任务。
