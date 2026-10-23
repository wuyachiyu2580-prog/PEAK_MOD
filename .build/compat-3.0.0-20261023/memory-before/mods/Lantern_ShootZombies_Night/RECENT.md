# Lantern_ShootZombies_Night Recent

Last updated: 2026-05-13

## 2026-05-13 PEAK 1.62.a compatibility check

- 用户更新 PEAK 1.62.a 反编译后复核 LSN。`Assembly-CSharp`、`pworld`、`PhotonUnityNetworking`、`PhotonRealtime`、`Photon3Unity3D` 等关键程序集与 1.61.b 反编译源码哈希一致。
- LSN 相关原版类/方法所在文件未变化：`Lantern`、`Campfire`、`CharacterAfflictions`、`CharacterHeatEmission`、`StatusField`、`MushroomZombie`、`Tornado`、`BugleSFX`、`Action_RaycastDart` 等。
- Release 构建通过：`dotnet build ... Lantern&ShootZombies&Night.csproj -c Release`，0 warnings，0 errors。暂不需要代码更新，客机备用池/灯燃料验证待办不变。

## 2026-05-10 客机备用池与灯燃料双扣修复

- 根据 `主客机日志\s.log` / `m.txt`，客机/主机都出现过 `FuelSync broadcast 2 owned lantern(s)`、`scene total=3`、同一玩家灯笼反复生成/销毁，说明 BPR/拾取流程中会短时间保留多盏本地有权限灯笼。
- 根因：LSN 的备用池是全局本地池；多个本地有权限灯笼同时跑 `Lantern.UpdateFuel` 时，一个会消耗备用池，另一个或副本会继续消耗自身灯燃料，HUD/同步也可能读到副本低燃料，表现为“备用池和灯燃料一起掉”。
- 新增 `LanternHelper.IsPrimaryLocalLantern/TryGetPrimaryLocalLanternGuid`，以当前本地玩家优先点亮灯、否则任意本地灯作为唯一主灯笼。
- `LanternFuelOverridePatch` 仅允许主灯笼执行备用池消耗、自定义燃料扣减、GameDefault 篝火/倍率补偿和备用池日志；非主本地副本会恢复到自身追踪燃料，不再参与消耗。
- `LanternFuelBroadcaster` 仅广播主灯笼，避免副本燃料快照污染 HUD/远端显示。
- 进一步按用户判断收紧同步权威：本地玩家灯笼不再接受主机/远端广播或 `OnInstanceDataSet` 带来的燃料下降覆盖；客机与主机一样，以本地备用池优先结算后的燃料作为本地权威，远端燃料快照只用于显示其他玩家。
- 当前设计结论：客机本地灯亮着时先按 `burn = Time.deltaTime * drain` 消耗本地备用池，备用池能覆盖时灯燃料不下降；只有备用池不足或用完后，剩余 `burn` 才扣本地 tracked fuel。
- 如果主机端认为该客机灯燃料在下降并广播更低 fuel，客机不采用该下降值；但如果后续实测主机通过 `LightLanternRPC(false)`、`SnuffLantern` 或对象销毁强制熄灯/销毁，则需要单独拦截远端熄灭路径，在本地仍有备用池或燃料时拒绝熄灭或立即重燃。
- 构建通过：`dotnet build ... Lantern&ShootZombies&Night.csproj -c Release`，0 warnings，0 errors，DLL 已输出到测试环境。

## 2026-05-09 LSN/BRP/PI 日志修复

- 版本保持 `0.2.1`，未改 `Plugin.ModVersion` 或项目版本语义。
- 根据 `主客机日志\m.txt` / `s.log`，主机大量报错集中在 `DispelFogField.Update/OnDisable`，与 BRP 驱雾字段在灯光状态销毁后继续 Update 有关；新增 `Patches\DispelFogFieldGuardPatch.cs`，仅吞掉该类的 `NullReferenceException`，并 10 秒节流记录一次。
- 备用池/燃料消耗不再只依赖 `PhotonView.IsMine`；新增 `LanternHelper.IsLocalPlayerLantern(Item)`，把本地玩家手持、临时槽、背包里的灯也算作本地燃料权限，修复“只有手持才先消耗备用池”的方向。
- 加燃料后如果灯从 0 恢复到正数，会通过 `TryRelightAfterFuelGain` 设置 `FlareActive` 并广播 `LightLanternRPC(true)`，用于降低客机端点燃失败概率。
- `ExtraLanternPurger` 排序加入“可销毁权限优先”，避免客户端一直选择无权限的背包灯导致重复失败；主机仍可清理所有有权限对象。
- 已恢复 LSN 自己的燃料/备用池 HUD：`Plugin.Update` 继续 `SafeTick(LanternHud.Tick, nameof(LanternHud))`。
- 已恢复并重做 LSN 内部燃料同步：`LanternFuelBroadcaster` 定时广播拥有者侧 `guid/fuel/max/lit/viewId`，`LanternFuelSync` 在接收端缓存，HUD 在本地槽位不是 Photon owner 或数据滞后时用缓存显示耐久。
- 当前删除范围仅限 PlayersInfo 的灯笼圆环/倒计时展示；不得再把它理解为删除 LSN 自己 HUD 或内部燃料同步。
- 构建通过：`dotnet build ... Lantern&ShootZombies&Night.csproj -c Release`，0 warnings，0 errors，DLL 已输出到测试环境。

更新时间：2026-05-08

## 当前状态

- 当前开发版本固定为 `0.2.1`，不要自动提升版本号。
- 插件常量 `Plugin.ModVersion` 为 `0.2.1`。
- 已将项目文件 `<Version>` 从 `0.2.0` 纠正为 `0.2.1`，保持与插件常量一致。
- 构建输出路径：`C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\Lantern_ShootZombies_Night.dll`。

## 本次检查：Thanks.ShootZombies 更新

- 参考代码 `Thanks.ShootZombies` 已更新到 `1.3.4`。
- 新版 `ShootZombies.ZombieDeathPatch` 已经自行优化：缓存 `Character`，直接读取 `character.data.dead`，并通过 `ZombieSpawner.DestroyZombie()` 统一销毁。
- LSN 原来的 `ShootZombiesPerformanceFix` 不应再无条件替换新版死亡补丁，否则会覆盖上游新逻辑。
- 已调整 `ShootZombiesPerformanceFix`：检测到 `ZombieSpawner.DestroyZombie` + `ZombieDeathPatch.ClearCaches` 时跳过替换，仅保留旧版回退路径。
- 旧版回退路径现在优先反射调用 `DestroyZombie`，找不到时再回退到 `RemoveZombie`。
- 回退路径会识别 `DestroyZombie` 是否已经完成销毁，避免随后再次执行 `PhotonNetwork.Destroy`。

## 验证

- `dotnet build` Release 通过。
- 构建结果：`0 warnings, 0 errors`。
- 修改后已重新构建到测试环境 DLL。

## 待确认

- 用新版 `Thanks.ShootZombies.dll` 实机运行一次，确认日志出现 `SZ.ZombieDeathPatch already optimized upstream; skip replacement`。
- 如果测试环境仍放的是旧版 `Thanks.ShootZombies.dll`，则会走旧版回退替换路径，这是预期兼容行为。
