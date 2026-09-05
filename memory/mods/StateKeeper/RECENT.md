# StateKeeper Recent

更新时间：2026-09-06

## 2026-09-06 当前停点

- 项目从未发布的 `PeakRunAnalytics` 完整改名为 `StateKeeper`：程序集、DLL、命名空间、插件类、GUID、BepInEx 配置、开发工程、测试工程和数据目录均已同步。
- 版本保持 `0.1.0`；profile 中只加载 `StateKeeper.dll`，旧 DLL/PDB 已移到 `MOD开发/StateKeeper/迁移备份/`。
- 本机旧数据已从 `LocalLow/LandCrab/PEAK/PeakRunAnalytics` 一次性移动到 `LocalLow/LandCrab/PEAK/StateKeeper`；旧目录不存在。用户确认正式发布不会改名，因此源码中的旧路径兼容迁移代码已删除。
- 旧 BepInEx 配置已改名为 `com.local.statekeeper.cfg`，保留 `Enabled`、`FavoriteHotkey`、`DebugLogging` 和 `StaminaEventThreshold`。
- `StaminaEventThreshold` 默认 `0.01`，绝对变化量达到阈值即记录；加入 `0.000001` 浮点比较容差，阈值为 0 时也不记录零变化调用。5Hz 体力采样不受影响。

## 性能优化

- 高频数据约 30 秒一个 GZip JSON 分块，主 JSON 只存索引和元数据。
- JSON 序列化、压缩和写盘在串行后台队列完成；正式结束也不再同步压缩阻塞 Harmony 主线程，正常退出仍会 flush。
- 慢盘情况下跳过过时的未封存检查点；已经封存的分块写入不会被跳过。
- 玩家身份在高频数据中使用整数索引，异常状态为固定顺序 `float[]`，两两距离为索引数组与距离数组。
- 角色对象列表每 0.5 秒刷新，位置仍每 0.2 秒读取；距离直接使用同一帧已采集位置，避免重复 `Center` 访问和字典查找。
- 库存轮询从 5Hz 降为 2Hz，先比较值类型指纹，变化时才创建完整快照和事件；关键物品动作仍由即时补丁记录。
- 槽位名称改为静态字符串表，采样取整改为轻量浮点运算。

## 实机数据调研

- 已解析迁移后的 128 个 GZip 分块：序号 0-127 连续、0 个解析错误、压缩 4.80 MiB、解压 88.69 MiB、压缩率约 18.47:1。
- 约 74 分 51 秒中有 21,367 个采样，平均 4.79Hz；20,013 个间隔落在 0.18-0.22 秒，556 个间隔大于 0.3 秒。
- 事件 87,678 条，其中体力事件 65,150、状态变化 20,927；这批数据来自阈值优化前，不能代表当前构建。
- 一局累计见过 14 个身份，单次采样为 4-10 个角色；后半段长期稳定为 6 个。需要新局确认 9-10 个角色是实际加入/离开还是离场角色对象暂存。
- 发现 11 次采样时间倒退，最大约 1.353 秒；当前尚未增加单调时间保护。
- 分块实际多为 152-172 个样本，平均约 166.93，因为只在 5 秒检查点发现达到 150 样本；数据完整，但不是严格 150 样本封块。

## 验证结果

- `dotnet build MOD开发/StateKeeper/StateKeeper/StateKeeper.csproj -warnaserror`：0 warnings / 0 errors。
- `dotnet test MOD开发/StateKeeper/StateKeeper.Tests/StateKeeper.Tests.csproj -warnaserror --no-restore`：2/2 通过。
- 当前 profile DLL：`StateKeeper.dll`，文件/产品版本 `0.1.0.0`。
