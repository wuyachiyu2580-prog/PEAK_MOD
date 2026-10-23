# Lantern_ShootZombies_Night Temporary Memory - 2026-05-19

## 2026-10-23 PEAK 3.0.0 兼容性审核

必须适配：BackpackSlot.hasBackpack 导致8个字段错误及4个连带变量错误；Lantern.lit已是属性，LightLanternRPC已不存在，但源码仍反射字段、打补丁及发送旧RPC。必须按当前authority/OnPhotonSerializeView保留燃料同步语义；2.6.b已存在这些断点。0.2.1发行目录目前无DLL，本轮未修代码或部署。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

## 临时记忆规则初始化

- 本目录用于记录 Lantern_ShootZombies_Night 任务中的阶段性思考结果，避免上下文压缩或换 AI 后丢失刚形成的判断。
- 每形成 3 次明确的阶段性判断、排查结论或方案取舍，就追加一次摘要。
- 摘要只写当前判断、证据、风险和下一步，不写原始脑内推理链。
- 压缩恢复后，先读本目录最新日期文件，再读 `RECENT.md`、`DECISIONS.md` 和根 `TODO.md`。
