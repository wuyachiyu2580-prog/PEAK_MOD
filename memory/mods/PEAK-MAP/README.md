# PEAK-MAP

更新时间：2026-09-06

## 项目定位

`PEAK-MAP` 是 PEAK 地图网站。当前已有地图上传、Supabase 元数据和 Cloudflare R2 文件存储能力。后续计划增加独立的 STATE KEEPER / 状态分析匿名数据提交入口，但本次没有修改网站源码，也没有上线接口。匿名策略采用适度脱敏：用户看到“玩家 A/B/C”等模糊名称，技术层仍删除账号/连接标识并粗化时间、距离和位置。

## 路径与现状

- 源码：`C:/Users/Administrator/Desktop/MOD/PEAK-MAP`
- 现有地图上传接口：`app/api/upload/route.ts`
- R2 客户端：`lib/r2.ts`
- R2 文件代理：`app/api/r2/[...key]/route.ts`
- 现有安全工具：`lib/security.ts`、`lib/rate-limiter.ts`
- 现有 UI 进度条：`components/ui/progress.tsx`
- 现有图表组件：`components/ui/chart.tsx`

## 当前状态

只完成调研和集成计划。STATE KEEPER 数据必须走独立接口、独立表和私有 R2 前缀，不能复用地图上传的 `maps` 表或默认公开文件代理。

完整网站修改建议见 `memory/mods/PEAK-MAP/PLAN.md`。
