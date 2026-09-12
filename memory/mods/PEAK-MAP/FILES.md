# PEAK-MAP Files

更新时间：2026-09-06

## 当前已知文件

- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/app/api/upload/route.ts`：现有地图上传接口，后续不要直接复用。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/app/api/r2/[...key]/route.ts`：现有 R2 文件代理，StateKeeper 原始匿名文件默认不接入。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/lib/r2.ts`：Cloudflare R2 S3 兼容客户端。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/lib/security.ts`：服务端安全校验工具。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/lib/rate-limiter.ts`：现有限流实现，StateKeeper 后续需要独立配额。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/components/ui/progress.tsx`：可复用的进度条组件。
- `C:/Users/Administrator/Desktop/MOD/PEAK-MAP/components/ui/chart.tsx`：后续分析展示可参考的图表组件，当前不实现具体算法。

## 计划新增

- `memory/mods/PEAK-MAP/PLAN.md`：完整网站修改计划和安全验收边界。
- `app/api/statekeeper/submit/route.ts`：独立匿名提交接口。
- StateKeeper 专用 Supabase migration 和 `statekeeper_submissions` 表。
- StateKeeper 专用上传开关、文件大小限制、限流和审核状态配置。
- 脱敏 schema/校验模块、提交确认 UI 和失败清理逻辑。
