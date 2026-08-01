# Mods Memory

更新时间：2026-07-23

每个 MOD 必须独立成目录。不要再把某个 MOD 的上下文堆到根目录或 `common/`。

## 标准结构

- `README.md`：项目概览和接手入口。
- `RECENT.md`：近期完成内容、验证结论、当前状态。
- `DECISIONS.md`：已确认的技术决策、禁止回退项、设计边界。
- `FILES.md`：关键路径、构建命令、诊断目录、关键源码文件。

## 已建立

- `ItemInfoCN/`
- `Lantern_ShootZombies_Night/`
- `PlayersInfo/`：当前版本线 `0.1.1`；DLL、发行文档和 0.1.1 zip 已同步到队友临时体力数字裁切修复。
- `DreamyAscent/`：永久暂停/归档。除标准四件套外，另有 `MAP_GENERATION.md`、`IMPLEMENTATION_MATRIX.md`、`MAP_GENERATION_RESEARCH_NOTES.md` 和 `CROSS_SEGMENT_PLACEMENT.md`，仅作为历史资料；除非用户明确恢复，否则不要继续 DA 工作。
- `WhySoLaggy/`
- `TerrainCustomiserCN/`：当前发布 `0.1.2`，对应原版 `TerrainCustomiser 0.3.2`；详情以该目录四件套为准。
- `PeakMapBrowser/`：当前插件版本 `0.1.1`，已完成账号安全改动和发布包；UI 重构暂缓，详情以该目录四件套为准。

## 命名说明

- 文件系统项目名 `Lantern&ShootZombies&Night` 在 memory 中使用目录名 `Lantern_ShootZombies_Night`，避免路径中的 `&` 影响命令和链接。


