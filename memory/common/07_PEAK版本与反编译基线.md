# PEAK 版本与反编译基线

Last updated: 2026-08-14

## 当前基线

- 当前游戏基线为 PEAK `2.1.a`。
- 反编译参考根目录：`引用参考代码\反编译\2.1.a`。
- 旧基线：`引用参考代码\反编译\作废\2.0.a`。
- 已安装游戏程序集：`C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed\Assembly-CSharp.dll`，大小 `1,562,112` bytes，时间戳 `2026-08-14 17:56:17`。
- 2.1.a 与 2.0.a 的反编译文件名集合未增删；原始差异大量是 Token/RVA/编译器生成方法编号变化。去除这些噪声后，`Assembly-CSharp` 的业务级变化集中在 16 个类，其余程序集未发现需要作为 MOD API 记录的业务变化。

## 影响 MOD 的关键变化

### 角色、状态和复活

- `Character.WillDoCheckpoint(out CheckpointFlag)`、`Character.TryCheckpoint()`、`Character.SpawnStatue(bool)` 现在是独立入口。`TryCheckpoint()` 复活时会清除 `passedOut`、`fullyPassedOut`、`dead`，恢复状态/荆棘，调用 `PushAll()`，然后再回收物品并传送。
- 仪式匕首的石化流程会优先判断 checkpoint；有 checkpoint 时生成石像并走 `TryCheckpoint()`，不再直接走普通 `RPC_PetrifyInstantly`。
- `CharacterData.currentStamina` 在无限体力模式下只拒绝低于当前值的写入；`Character.infiniteStam` setter 改为 public。`Peak.Afflictions.Affliction_InfiniteStamina` 现在直接开关该属性，结束时关闭，不再只用 `AddStamina(1f)` 充满一次。
- `CharacterData` 新增公开字段 `lastFroggedTime`。`FrogTongue` 用它和 `globalFrogCooldown`（默认 `10f`）限制同一角色被青蛙攻击的全局间隔。
- 骨骼角色现在允许 `FlyTrap` / `Web` 状态；僵尸允许 `Web`。加荆棘时骨骼限制改为只拦截普通 `type == 0` 路径。

### 运行时行为

- `Glider` 的开启消耗和持续消耗调用 `UseStamina(..., ignoreAscents: false)`，即不再忽略 ascent 体力倍率。涉及滑翔体力的补丁不要把第三参数语义当作旧版本行为。
- `GameBooter` 收到邀请时不再检查本地 quicksave、比较 lobby `RunId` 或弹出 `SAVE_DESTROY_ON_JOIN` 确认页，而是连接就绪后直接 `ConsumePendingJoin(true)`。需要拦截邀请或保护存档的 MOD 必须以 2.1.a 逻辑为准。
- `Quicksave.FinalizeRunSetup()` 完成加载后会清空 `ShouldUseSaveData`、`_loadedData` 和 `_hasLoadedData`，避免已消费的存档继续被重复使用。
- `MountainProgressHandler.JumpToSegment()` 将 `JoinedInSegment` 设为 `segment - 1`；`CheckProgress()` 不再以 `Application.isEditor` 或 `Debug.isDebugBuild` 绕过“最远进度点”判断。
- `CharacterVoiceHandler.IsMuffled` 只有在说话角色或本地角色仍存活且处于 struggling 状态时才判定静音；死亡角色不再触发该静音条件。
- `FakeItem` 拾取带 `ItemTags.Mystical` 的物品时会触发 `ACHIEVEMENTTYPE.EsotericaBadge`。

### 物品、地图和机关

- `Peak.RitualDaggerFeedBehavior` 改为继承 `ItemComponent`，喂食后通过 `item.ConsumeDelayed(false)` 消耗仪式匕首，并新增空的 `OnInstanceDataSet()`。涉及该组件的补丁应按 `ItemComponent.item` / 生命周期处理。
- `AntiSphere` 会清理空、死亡、被携带或离开球体范围的角色，并结束本地反重力 UI；新增公开 `SphereCollider coll`。2.1.a 的 `ACTIVE_ITEMS` 循环在发现空物品时直接从 `foreach` 集合移除，存在运行时枚举修改异常风险，待实机日志确认。
- `GhostBallSpawner` 新增 `spawnMaxDepthTransform`，生成条件增加 Z 轴深度上限。
- `MovingSawBlade` 新增 `timeout` 窗口内的命中音效节流，公开音效入口为 `hitSound`。
- `SpineCheck` 新增公开 `Transform spine`，从每次查找改为缓存后复用。

## 对现有 MOD 的结论

- `PlayersInfo` 依赖的 `StaminaBar`、`BarAffliction`、`CharacterData.petrifyAmount`、`Character.GetMaxStamina()`、`Character.SetExtraStamina()`、`CharacterSyncer` 和 `Player.SyncInventoryRPC` 在 2.1.a 中仍兼容；Release 基线构建为 `0 warnings / 0 errors`，本轮无需新增兼容代码。
- 2.0.a 已有的 PlayersInfo 兼容规则继续有效：石化条读 `CharacterData.petrifyAmount`，额外体力使用游戏已按石化限制后的值，背包用 `BackpackSlot.IsEmpty()` 和 `backpackSlot.data.itemSlots`。
- 其他 MOD 暂未因 2.1.a 发现必须修改的源码入口。后续新增补丁、重新构建或出现实机异常时，优先以 `反编译\2.1.a` 和当前安装 DLL 复核，不要引用 `作废\2.0.a` 的旧实现细节。

## 证据与限制

- 本次变化判断以 `2.0.a` / `2.1.a` 反编译源码的归一化差异为主，并用当前安装的 `Assembly-CSharp.dll` 做版本尺寸/时间确认。
- 反编译源码不是原始工程源码；Token、RVA、局部变量编号和部分格式变化不能直接视为行为变化。
- 2.1.a 的游戏内邀请覆盖、checkpoint、青蛙冷却、AntiSphere 清理和仪式匕首消耗仍需实机验证；未验证前不要把这些结论扩展成 MOD 功能承诺。
