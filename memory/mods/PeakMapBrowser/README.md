# PeakMapBrowser

更新时间：2026-07-30

## 项目定位

`PeakMapBrowser` 是 PEAK 的 BepInEx 地图库客户端，连接独立的 `peakmap.top` 服务端，提供社区地图浏览、下载、上传、账号地图管理和点赞。

- BepInEx GUID：`com.wuyachiyu.peakmapbrowser`
- AssemblyName：`PeakMapBrowser`
- 当前插件版本：`0.1.1`
- 源码目录：`MOD开发\PeakMapBrowser\PeakMapBrowser`
- 服务端目录：`C:\Users\Administrator\Desktop\MOD\PEAK-MAP`
- 默认 API：`https://peakmap.top`
- 默认快捷键：`/`
- 当前 UI：Unity IMGUI，UI 重构暂缓，优先级低

## 功能轮廓

- 社区地图分页、搜索、按最新/下载量排序、MOD 版本筛选。
- 地图 JSON 下载和本地 Map Saves 扫描。
- 上传地图 JSON、描述和可选封面；登录后上传自动归属当前账号。
- 账号登录、退出、访问令牌刷新和本地 session 保存；不保存密码，access token 只在内存中存在。
- refresh token 使用 Windows DPAPI `CurrentUser` 加密后持久化，旧版明文 session 会在成功读取后迁移。
- 登录账号查看我的地图，编辑名称、作者、描述、MOD 版本、JSON 和封面，或删除地图。
- 登录账号或 guest cookie 点赞/取消点赞。
- 注册、找回密码和邮箱验证仍通过 `https://peakmap.top/account` 网页完成。

## 接手入口

1. 先读 `RECENT.md`，确认当前客户端、服务端和 UI 状态。
2. 再读 `DECISIONS.md`，尤其是点赞逻辑和 UI 暂缓决策。
3. 修改源码前读 `FILES.md`，确认源码、构建输出和 API 文档路径。
4. 上下文压缩或换 AI 后先读最新的 `temp/2026-07-30.md`，再读正式四件套。

## 当前状态

账号、个人地图管理、上传归属、点赞同步、图片缓存和 session 安全改动已经接入源码。`0.1.1` 已完成构建和打包：

- 测试 DLL：`测试环境\BepInEx\plugins\PeakMapBrowser.dll`
- 发布目录：`MOD开发\PeakMapBrowser\发行\0.1.1`
- 发布包：`wuyachiyu-PeakMapBrowser-0.1.1.zip`
- 构建结果：`0` warnings，`0` errors

客户端已支持调用 `POST /api/auth/sign-out`；线上 `https://peakmap.top/api/auth/sign-out` 已部署并通过无 token 拒绝测试。使用有效 access token 时，MOD 会请求服务端撤销当前会话并清理本地 session。
