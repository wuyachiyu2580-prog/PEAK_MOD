# Lantern_ShootZombies_Night 待办

## 2026-10-23 PEAK 3.0.0 兼容性审核

必须适配：BackpackSlot.hasBackpack 导致8个字段错误及4个连带变量错误；Lantern.lit已是属性，LightLanternRPC已不存在，但源码仍反射字段、打补丁及发送旧RPC。必须按当前authority/OnPhotonSerializeView保留燃料同步语义；2.6.b已存在这些断点。0.2.1发行目录目前无DLL，本轮未修代码或部署。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

- 完成客机燃料权威、重复灯和大队伍同步验收。
