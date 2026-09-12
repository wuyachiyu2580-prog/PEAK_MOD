# PEAK-MAP × STATE KEEPER 网站修改计划

更新时间：2026-09-06

## 状态和边界

这是基于现有网站源码的调研计划，尚未修改或部署网站代码。目标是临时承接用户自愿提交的 STATE KEEPER / 状态分析脱敏数据；数据样本足够或功能成熟后可以关闭提交入口。

现有网站为 Next.js 13.5.1 + TypeScript + Supabase + Cloudflare R2，已有地图上传、R2 S3 兼容客户端、文件代理、IP 内存限流、中英文语言上下文和 shadcn/Radix UI。STATE KEEPER 数据必须与地图业务隔离。

## 代码修改建议

计划新增：

```text
app/api/statekeeper/submit/route.ts
lib/statekeeper/schema.ts
lib/statekeeper/sanitize.ts
lib/statekeeper/config.ts
supabase/migrations/<timestamp>_statekeeper_submissions.sql
```

可复用但不能直接照搬业务规则的现有代码：

```text
app/api/upload/route.ts              # 只参考认证、错误和事务结构
lib/r2.ts                            # 复用服务端 R2 客户端
lib/security.ts                      # 扩展 StateKeeper 专用校验
lib/rate-limiter.ts                  # 新建独立 bucket/配额
components/ui/progress.tsx           # 后续网站上传/处理进度可复用
components/ui/chart.tsx              # 后续聚合分析可参考，当前不实现
```

不要修改地图 `/api/upload` 去兼容两种完全不同的 schema，也不要把 StateKeeper 文件登记到 `maps` 表。

## 存储模型

R2 使用私有前缀：

```text
statekeeper-submissions/<random-submission-id>.json.gz
```

Supabase 新表 `statekeeper_submissions` 建议只保存：

- `id`：随机提交 ID。
- `created_at`：服务器接收时间，不是原始游戏精确时间。
- `schema_version`、`mod_version`、`game_version`。
- `object_key`：私有 R2 对象键。
- `compressed_bytes`、`uncompressed_bytes`。
- `sample_count`、`inventory_count`、`event_count`、`player_count`、`duration_bucket`。
- `run_status`、`run_outcome`、`quality_flags`。
- `review_status`：`pending/reviewed/rejected/approved`。
- `delete_token_hash`：如采用一次性删除令牌，只存哈希。

不在表中保存玩家名、Photon UserId、ActorNumber、物品 GUID、精确轨迹、原始 detail 或可公开访问的对象 URL。

## 提交 API 流程

1. 检查 `statekeeper_submission_enabled`；关闭时返回明确的不可用状态。
2. 应用独立于地图上传的 StateKeeper 限流。
3. 先限制请求体压缩大小，再检查 GZip 魔数、MIME 和扩展名。
4. 有界解压并限制解压后大小，防止压缩炸弹。
5. 按版本化 JSON schema 校验深度、数组长度、字符串长度、数值范围和允许事件类型。
6. 服务端再次执行脱敏/规范化，不能相信客户端已经清理干净。
7. 先生成随机提交 ID 和私有 R2 对象键，再上传匿名数据。
8. 写 Supabase 元数据；若数据库写入失败，应删除刚上传的 R2 文件，避免孤儿对象。
9. 返回提交 ID、审核状态和一次性删除令牌；响应不返回 R2 密钥或私有对象直链。

## 适度脱敏 schema

- 每次提交把玩家重新映射为 `Player A/B/C...` 或等价整数索引；映射不能跨提交稳定。
- 删除 Photon UserId、ActorNumber、显示名原文和其他固定身份标识。
- `runId` 替换为随机 submission/run ID。
- 删除物品 GUID；保留物品类型、名称、资源量、耐久和使用事件。
- 删除精确 UTC 时间；保留局内相对秒数和粗粒度时长桶。
- 距离默认约按 5-10 米取整；绝对坐标默认删除或按后续明确规则粗粒度化。
- 删除原始 detail、机器名、本地路径、配置路径和日志；事件只保留白名单字段。
- `StaminaChanged` 等 detail 中的必要数值应先解析为有界数值字段，再丢弃原字符串。
- 保留 `LocalAuthoritative` / `RemoteObserved` 来源，不改变数据可信度语义。

## 用户同意和关闭机制

- MOD 面板先显示脱敏摘要、用途、保留/删除说明，用户主动勾选后才能提交；不开默认自动上传。
- 默认不提供上传原始详细数据的选项。
- 每次提交最好返回一次性删除令牌；网站至少保留管理员按提交 ID 删除 R2 和 Supabase 元数据的能力。
- 配置建议独立于地图上传：

```text
statekeeper_submission_enabled
statekeeper_max_upload_mb
statekeeper_max_uncompressed_mb
statekeeper_upload_rate_limit
```

- 功能关闭后拒绝新提交，但已有数据的保留或删除必须按公布的政策执行，不能把“入口关闭”误当成“数据已删除”。

## 安全和隐私验收

- 压缩大小、解压大小、JSON 深度、数组数量、字符串长度和数值范围均有服务端硬上限。
- 未知字段、未知事件类型、异常长字符串和非有限数值被拒绝或安全丢弃。
- 安全日志只记录请求结果、错误类别、大小和随机提交 ID，不记录原始数据。
- IP 如用于短期限流或安全审计，不进入分析表、不公开，也不与匿名局数据形成长期画像。
- 私有 R2 文件不接入现有公开 `/api/r2/*` 代理；以后若要公开必须重新做隐私审核。
- 地图上传、地图下载、地图表和现有 R2 对象路径回归测试全部通过。

## 实施顺序

1. 先落 Supabase migration、站点关闭开关和版本化 schema。
2. 实现服务端有界解压、二次脱敏、独立限流和私有 R2 写入。
3. 实现失败回滚、审核状态、删除令牌或管理员删除路径。
4. 与 StateKeeper 的匿名导出文件做本地端到端测试，确认敏感字段不存在。
5. 再接入 MOD 面板的自愿提交入口和上传状态；具体网站分析图表后置。
