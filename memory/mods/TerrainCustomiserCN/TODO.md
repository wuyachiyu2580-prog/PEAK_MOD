# TerrainCustomiserCN 待办

## 2026-10-23 PEAK 3.0.0 兼容性审核

必须适配：UBImGui/PhotonCustomPropsUtils引用缺失，主/Collector构建失败；游戏引用仍为2026-05-22旧publicized DLL。0.1.2发布DLL有23个失效成员，涉及生成和光照；LevelGenStep.Execute现返回IEnumerator，PropGrouper.RunAll返回Awaitable。需独立兼容迁移，本轮未改地图/网络/源码或部署。

详细结论与原始证据：[COMPATIBILITY_3.0.0_2026-10-23.md](../../COMPATIBILITY_3.0.0_2026-10-23.md)。这是审核结果，不是实施完成或实机验收。下方旧版本结论仅适用于原记录时间。

- 补漏翻、重建 Release 并检查发布包内容。
