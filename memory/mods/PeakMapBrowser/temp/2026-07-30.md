# PeakMapBrowser 临时记忆 - 2026-07-30

## 0.1.1 发布与安全收口

- `PeakMapBrowser` 已升为 `0.1.1`，源码版本同步位置：
  - `MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapBrowser.csproj` 的 `<Version>`
  - `MOD开发\PeakMapBrowser\PeakMapBrowser\Plugin.cs` 的 `BepInPlugin` 版本
- 构建命令：
  `dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapBrowser.csproj" --no-restore`
- 构建结果：`0` warnings，`0` errors。
- 测试 DLL：`C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\PeakMapBrowser.dll`。
- 发布目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\发行\0.1.1`。
- 发布包：`wuyachiyu-PeakMapBrowser-0.1.1.zip`，内含 DLL、manifest、README、CHANGELOG、API.md、icon；Thunderstore zip 根目录为文件本身，不包含额外目录。

## Session 安全

- `session.json` 只持久化 DPAPI 加密后的 `protected_refresh_token`、账号基础信息和 guest id。
- `access_token` 只在运行时内存中保存，密码不保存。
- DPAPI 使用 `DataProtectionScope.CurrentUser` 和固定 entropy；复制到其他 Windows 用户通常无法解密。
- 旧版明文 session 会在成功读取后迁移；解密或保存失败时不回退写入明文 token。
- 后台每 30 秒最多发起一次检查；access token 缺失或剩余少于 300 秒时请求 `/api/auth/refresh`。
- 两个退出登录入口调用 `/api/auth/sign-out`，服务端失败仍清本地并提示。

## 服务端边界

- `C:\Users\Administrator\Desktop\MOD\PEAK-MAP` 本轮未修改。
- 线上 `https://peakmap.top/api/auth/sign-out` 已部署并测试：无 Bearer token 的空 JSON POST 返回 `401`、`{"success":false,"error":"Missing or invalid Bearer token"}` 和 `Cache-Control: no-store`；GET 返回 `405`。
- 仍未用真实账号 token 做撤销验证，避免在测试过程中暴露凭据；需要实机退出登录后观察该 refresh token 是否无法再次刷新。
