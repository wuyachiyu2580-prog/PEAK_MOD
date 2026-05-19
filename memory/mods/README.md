# Mods Memory

更新时间：2026-05-13

每个 MOD 必须独立成目录。不要再把某个 MOD 的上下文堆到根目录或 `common/`。

## 标准结构

- `README.md`：项目概览和接手入口。
- `RECENT.md`：近期完成内容、验证结论、当前状态。
- `DECISIONS.md`：已确认的技术决策、禁止回退项、设计边界。
- `FILES.md`：关键路径、构建命令、诊断目录、关键源码文件。

## 已建立

- `ItemInfoCN/`
- `Lantern_ShootZombies_Night/`
- `PlayersInfo/`
- `DreamyAscent/`：除标准四件套外，另有 `MAP_GENERATION.md`、`IMPLEMENTATION_MATRIX.md`、`MAP_GENERATION_RESEARCH_NOTES.md` 和 `CROSS_SEGMENT_PLACEMENT.md`，用于承载地图生成长期结论、需求到实现矩阵、多轮依据和跨区段放置。
- `WhySoLaggy/`

## 命名说明

- 文件系统项目名 `Lantern&ShootZombies&Night` 在 memory 中使用目录名 `Lantern_ShootZombies_Night`，避免路径中的 `&` 影响命令和链接。


