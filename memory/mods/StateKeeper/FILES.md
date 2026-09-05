# StateKeeper Files

更新时间：2026-09-06

## 工程

- `MOD开发/StateKeeper/StateKeeper/StateKeeper.csproj`：主工程，版本 0.1.0，直接输出到 r2modman `2.0.a` profile。
- `MOD开发/StateKeeper/StateKeeper.Tests/StateKeeper.Tests.csproj`：MSTest 测试工程。
- `MOD开发/StateKeeper/README.md`：开发说明；发行目录当前仍是旧草稿，不同步。
- `MOD开发/StateKeeper/CHANGELOG.md`：0.1.0 开发变更。

## 关键源码

- `StateKeeperPlugin.cs`：BepInEx 生命周期、配置、Harmony 初始化、F8 收藏入口。
- `RunCollector.cs`：RunId 绑定、5Hz 采样、2Hz 库存、玩家身份、距离、状态和事件收集。
- `RunStore.cs`：活动恢复、GZip 分块、后台队列、原子写入、收藏和最近 10 局清理。
- `StatsModels.cs`：RunRecord、分块、玩家、体力/状态、库存、事件和索引模型。
- `GamePatches.cs`：正式结束、胜利、死亡/倒地、物品生命周期和体力事件补丁。
- `Properties/AssemblyInfo.cs`：StateKeeper 程序集信息和 0.1.0.0 版本。
- `StateKeeper.Tests/RunStoreTests.cs`：8 人分块恢复、收藏和最近记录联动清理测试。

## 运行路径

- DLL：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/StateKeeper.dll`
- 配置：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/config/com.local.statekeeper.cfg`
- 数据根：`C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/StateKeeper/`
- 旧 DLL/PDB 迁移备份：`MOD开发/StateKeeper/迁移备份/`

## 数据结构

```text
StateKeeper/
  Runs/
  Favorites/
  active-run.json
  index.json
```

局主 JSON 只保存 header、players、statusTypeOrder、chunks 和 activeChunk。高频 `samples`、`inventorySnapshots`、`events` 写入 `RunId.chunk-xxxxx.json.gz`。

## 构建与测试

```powershell
dotnet build "MOD开发\StateKeeper\StateKeeper\StateKeeper.csproj" -warnaserror
dotnet test "MOD开发\StateKeeper\StateKeeper.Tests\StateKeeper.Tests.csproj" -warnaserror --no-restore
```

当前结果：构建 0 warnings / 0 errors，测试 2/2 通过。
