# PeakMapBrowser Recent

更新时间：2026-07-30

## 2026-07-30 - 0.1.1 安全改动与发布包

- 版本号已同步为 `0.1.1`：`PeakMapBrowser.csproj`、`Plugin.cs`、发行 `manifest.json`、README 和 CHANGELOG。
- `PeakMapSessionStore` 改为 Windows DPAPI `ProtectedData` + `DataProtectionScope.CurrentUser` 保存 refresh token；`session.json` 不再保存明文 access token 或 refresh token。
- access token 只存在当前进程内存；启动后若只有 refresh token，会自动刷新；access token 剩余时间少于 5 分钟时后台检查刷新，每次检查间隔 30 秒。
- 保留旧版明文 session 的一次性迁移逻辑；解密失败不会回退写入明文 token。
- 两处退出登录按钮统一调用 `POST /api/auth/sign-out`，服务端撤销失败时仍清理本地 session 并提示用户。
- 线上 `https://peakmap.top/api/auth/sign-out` 已部署；空 JSON 且无 Bearer token 返回 `401`、`{"success":false,"error":"Missing or invalid Bearer token"}` 和 `Cache-Control: no-store`。本地 `PEAK-MAP` checkout 是否同步该路由仍需单独确认。
- 发布包已生成：`MOD开发\PeakMapBrowser\发行\0.1.1\wuyachiyu-PeakMapBrowser-0.1.1.zip`；zip 内含 `PeakMapBrowser.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、`icon.png` 和 API 文档。
- 编译通过：`0` warnings，`0` errors；源码和发行文档按 UTF-8 检查通过。

## 账号与地图管理

- 客户端已经接入 `/api/auth/sign-in`、`/api/auth/refresh` 和 `/api/account/maps`。
- `PeakMapApiClient` 统一发送 Bearer token，并保存/发送 `peak_guest_id` cookie。
- `PeakMapSessionStore` 内存中维护 access token、加密持久化 refresh token、过期时间、用户基础信息和 guest id，不保存密码；令牌即将过期时自动刷新，刷新失败清除登录态。
- 登录后上传自动携带账号归属；当前账号可以编辑或删除自己拥有的地图。
- `MapEntry` 已包含 `likes`、`updated_at`、`revision` 和 `liked_by_me` 等字段。

## 点赞规则

- 用户明确要求保持服务端原有点赞逻辑。
- 当前身份是“登录账号”或“guest cookie”；同一身份对同一地图只能有一条 active like，再次点击取消。
- IP 只用于现有点赞限频，不用于点赞身份，也没有新增 `ip_hash` 唯一约束。
- 不要重新引入“同一 IP 对同一地图只能贡献一个赞”的方案，除非用户再次明确要求。

## 当前 IMGUI 状态

- 社区地图：已去掉右侧窄详情栏，点击地图卡片弹出独立详情窗口，显示封面、元数据、描述、下载和点赞按钮。
- 详情弹窗打开时禁用底层页面控件，避免点击穿透到非当前层按钮。
- 我的地图：恢复为左侧地图列表 + 右侧编辑器布局。
- 上传页本地 JSON 使用下拉选择器；我的地图编辑器的 JSON 选择器保持不变。
- 我的地图编辑器的 MOD 版本已改为与上传页相同的下拉选择列表。
- 切换我的地图时会清除 IMGUI 键盘焦点、文本选区和 hot control，避免地图 A 的描述选区残留到地图 B。
- 用户已明确：UI 目前有些丑，后续可能改为 uGUI + TextMeshPro，但 UI 重构暂时不做，优先级低。

## 构建验证

- 最近一次客户端构建通过：`0` warnings，`0` errors。
- 输出 DLL：`C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\PeakMapBrowser.dll`。
- `PEAK-MAP` 的 `npm.cmd run typecheck` 和 `npm.cmd run build` 已在账号功能实现阶段通过；本轮没有修改服务端。

## 待验证

- 需要用户进游戏手动验证登录、token 刷新、上传归属、我的地图编辑/删除、版本下拉、封面选择、详情弹窗和点赞状态。
- 需要确认线上 Supabase 已应用账号归属、点赞和 revision 相关迁移；源码迁移文件在 `PEAK-MAP\supabase\migrations`。
