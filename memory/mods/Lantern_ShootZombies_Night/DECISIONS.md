# Lantern_ShootZombies_Night Decisions

Last updated: 2026-05-10

## 当前边界变更

- LSN 必须保留自己的燃料/备用池 HUD，这是灯笼耐久可见性的核心功能，不得因删除 PI 展示而停用。
- LSN 必须保留内部燃料同步广播；该广播用于 Photon 主权变化时修正本地 LSN HUD，不等同于 PlayersInfo 的队友圆环显示。
- PlayersInfo 不再消费 LSN 燃料广播，也不再显示灯笼圆环/倒计时；这是 PI 展示层删除，不是 LSN 功能删除。
- 对本地玩家灯笼的燃料判断不能只看 `PhotonView.IsMine`。背包和 BPR 重建场景下，属于本地玩家槽位的灯可能不是本机 Photon owner，必须通过物品 `guid` 反查本地手持、临时槽和背包槽。
- 本地玩家灯笼燃料以本地端为权威：主机/远端广播或 `OnInstanceDataSet` 带来的 fuel 下降不得覆盖本地 tracked fuel。客机和主机都必须先消耗本地备用池，备用池不足时才扣本地灯燃料。
- LSN 内部燃料同步只用于远端显示/队友状态参考；如果收到的 GUID 属于本地玩家灯笼，接收端必须忽略该快照。
- 同一玩家短时间存在多盏本地有权限灯笼时，只允许“主灯笼”参与备用池和燃料消耗；主灯笼优先选择当前点亮灯，否则选择任意本地槽位灯。副本只能恢复/保持自身 tracked fuel，不能消耗全局备用池。
- `DispelFogFieldGuardPatch` 只允许吞掉 BRP 驱雾字段的 `NullReferenceException`，不得扩大到通用异常吞噬；非 NRE 必须继续抛出，避免隐藏真实逻辑错误。
- 多余灯清理排序必须优先考虑“当前端是否有销毁权限”，再考虑临时槽和燃料量；否则客机端会反复选择无权限对象并刷失败日志。

更新时间：2026-05-08

## 版本

- 当前版本固定为 `0.2.1`，发布前不要自动递增。
- `Plugin.ModVersion`、`.csproj <Version>`、发行目录语义应保持一致。

## 前置与兼容

- `Thanks.ShootZombies` 是前置依赖，不包裹原 DLL，不复制其实现。
- 对前置 MOD 的性能修复必须优先检测上游是否已修复；上游已修复时跳过补丁，避免覆盖新逻辑。
- `ShootZombiesPerformanceFix` 只保留旧版兼容回退：新版 SZ 1.3.4+ 跳过替换死亡补丁；旧版才替换 `ZombieDeathPatch.Postfix`。
- 旧版回退销毁僵尸时优先调用 `ZombieSpawner.DestroyZombie`，没有该方法才退回 `RemoveZombie`。
- 如果回退调用的是 `DestroyZombie`，不得再额外调用 `PhotonNetwork.Destroy`，避免双销毁。

## 保持不变

- LSN 自身功能仍以灯笼、夜晚寒冷、回暖、HUD、联机配置同步为主。
- `PlayersInfo` 交互继续通过 `LanternFuelBroadcaster`/事件接收，不直接耦合内部字段。
- BlackPeakRemix 仍通过运行时检测和反射适配。
