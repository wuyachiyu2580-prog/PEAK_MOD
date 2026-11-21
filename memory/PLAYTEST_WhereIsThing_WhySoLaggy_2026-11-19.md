# 2026-11-19 2.0.a 实机复核

用户在 2026-11-18 部署后运行了 2.0.a。本次只审阅日志、现有诊断包和配置，没有改代码、安装文件或配置。

## 加载与配置

- BepInEx 启动日志确认 WhereIsThing 0.1.3 和 WhySoLaggy 1.0.5 均已加载，对应 20261117.playtest 构建；安装 DLL 哈希仍与部署清单一致。
- WhereIsThing 配置 `Diagnostics/PhaseTiming=false`，本轮没有生成其阶段报告。其余实测设置保留原值，包括 `MaxDistance=0`、`RestrictClientPresets=false`、范围包含 Statue。未改动配置。
- WhySoLaggy 最新导出 ZIP 对应同一 SessionId 和 `1.0.5-dev.20261117.playtest`，包含 `loss-summary.txt`。10 个诊断 ZIP 的帧丢失和关键事件丢失均为 0；其中 2 个报告明细丢失，共 503 条，LossStatistics 为 Exact。最新 ZIP 保留 7,482 帧，约 63 秒，三类丢失均为 0。

## 性能与异常

- WhySoLaggy 持续 CSV 当前保留段有 11,258 条 FrameSample，P95 AvgFrameMs 为 12.19 ms，28 条达到 50 ms 或更高。最大两次约 1,322.5 ms 和 1,189.7 ms，分别落在 Gameplay 与 Airport 状态。CSV 滚动窗口覆盖运行末段，不能将这些数值外推到整局。
- WhySoLaggy 的近期 PluginProfiler 报告中，WhereIsThing Update 观测均值为 0.015 ms/frame、1,428 次采样、0 错误；LateUpdate 报告为 0.000 ms/frame。该结果说明被测常规回调开销低，但没有覆盖 WhereIsThing 各阶段耗时，不能解释周期性整体尖峰。
- WhereIsThing 阶段计时关闭，因此本轮无法判断场景发现、网络清理或标签刷新中哪个子步骤造成尖峰。整体帧采样仍有明显长帧，尚不能判断来源。
- WhySoLaggy 的 JSONL 记录到其他插件 Compass 的 `CompassTapeUI.Update` 持续抛出 `NullReferenceException`；一个捕获窗口累计计数达到 4,572，属于独立插件异常，不能归因于本次两个 MOD。Player.log 还持续出现多人网络 RPC、Instantiate/Destroy 洪泛告警，属于环境负载证据，不直接指向两个 MOD。

## 结论

本轮确认两个目标版本正常加载，WhySoLaggy 新版导出丢失统计可用。WhereIsThing 的普通 Update 回调开销很低，但其约 5 秒尖峰的具体来源仍未定位。下一次若要继续定位，在实际扫描场景短时开启 WhereIsThing 阶段计时，保留当前选择与场景条件，再按报告中的峰值步骤判断；无需据本轮证据立即改动实现。
