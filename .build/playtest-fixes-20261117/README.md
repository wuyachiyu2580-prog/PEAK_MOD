# 实测修复隔离产物

## 2026-11-18 已部署 2.0.a，等待实机验证

按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。

2026-11-18最终验收；开发标识20261117.playtest。未部署到游戏profile。

- WhySoLaggy: whysolaggy-release/WhySoLaggy.dll + WhySoLaggy.pdb；105项测试通过。
- WhereIsThing: whereisthing/bin/WhereIsThing.dll + WhereIsThing.pdb；184+104项逻辑测试通过。
- verification.json为最终源码、二进制、安装保护哈希与验证结果。
- 回放10包、53,651帧，补回329条、无新增旧事件丢失；不等于Unity实机性能结论。
- 备份在backup/；instrument*.py是已执行的一次性编辑脚本，不要重复执行。

[完整实施与实测说明](../../memory/IMPLEMENTATION_PlaytestFixes_2026-11-18.md)
