# Mods Memory

更新时间：2026-09-21

每个 MOD 必须独立成目录。不要再把某个 MOD 的上下文堆到根目录或 `common/`。

## 标准结构

- `README.md`：项目概览和接手入口。
- `RECENT.md`：近期完成内容、验证结论、当前状态。
- `DECISIONS.md`：已确认的技术决策、禁止回退项、设计边界。
- `FILES.md`：关键路径、构建命令、诊断目录、关键源码文件。

## 已建立

- `ItemInfoCN/`
- `Lantern_ShootZombies_Night/`
- `PlayersInfo/`：`0.2.5` 发行文件已备齐，含当前全部已实现改动；无 ZIP、未上传，完整实机待验收。
- `StateKeeper/`：当前版本 `0.1.0` 开发中，展示名 `STATE KEEPER` / `状态分析`；旧 PeakRunAnalytics 已完成开发工程、本机 DLL/配置和本机数据目录改名，整体合并、面板和 PEAK-MAP 匿名提交仍按计划推进，发行草稿暂不更新。
- `PEAK-MAP/`：StateKeeper 自愿匿名数据提交的网站集成调研，当前仅有 memory 方案，未改网站源码。
- `ModConfigDiagnostics/`：当前版本 `0.1.0`；只读检查 ModConfig/PEAKLib 版本、重复 DLL/设置和异常 UI 文本。实测已把 `LOC: 0` 收敛到未激活的原版 `SettingsCell` 模板，公共依赖和五个旧菜单类型兼容项目的修改建议见 `PLAN.md`。
- `OldPC/`：当前开发版本 `0.0.1`；本地旧电脑画面和望远镜清晰模式，尚未记录构建、发行或实机验收。
- `DreamyAscent/`：永久暂停/归档。研究资料位于 `archive/`，除非用户明确恢复，否则不要继续 DA 工作。
- `WhySoLaggy/`：`1.0.5` 发行文件已备齐，含当前全部已实现改动；无 ZIP、未上传，完整实机待验收。
- `TerrainCustomiserCN/`：当前发布 `0.1.2`，对应原版 `TerrainCustomiser 0.3.2`；详情以该目录四件套为准。
- `PeakMapBrowser/`：当前插件版本 `0.1.1`，已完成账号安全改动和发布包；UI 重构暂缓，详情以该目录四件套为准。
- `WhereIsThing/`：`0.1.2` 发行文件已备齐，含当前全部已实现改动；无 ZIP、未上传，完整实机待验收。
- `WhereIsMyAmulet/`：`1.0.4` 发行文件已备齐，含当前全部已实现改动；无 ZIP、未上传，完整实机待验收。

## 命名说明

- 文件系统项目名 `Lantern&ShootZombies&Night` 在 memory 中使用目录名 `Lantern_ShootZombies_Night`，避免路径中的 `&` 影响命令和链接。
