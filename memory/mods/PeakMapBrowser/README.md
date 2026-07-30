# PeakMapBrowser

更新时间：2026-07-23

## 项目定位

`PeakMapBrowser` 是 PEAK 的 BepInEx 地图库客户端，连接独立的 `peakmap.top` 服务端，提供社区地图浏览、下载、上传、账号地图管理和点赞。

- BepInEx GUID：`com.wuyachiyu.peakmapbrowser`
- AssemblyName：`PeakMapBrowser`
- 当前插件版本：`0.1.0`
- 源码目录：`MOD开发\PeakMapBrowser\PeakMapBrowser`
- 服务端目录：`C:\Users\Administrator\Desktop\MOD\PEAK-MAP`
- 默认 API：`https://peakmap.top`
- 默认快捷键：`/`
- 当前 UI：Unity IMGUI，UI 重构暂缓，优先级低

## 功能轮廓

- 社区地图分页、搜索、按最新/下载量排序、MOD 版本筛选。
- 地图 JSON 下载和本地 Map Saves 扫描。
- 上传地图 JSON、描述和可选封面；登录后上传自动归属当前账号。
- 账号登录、退出、访问令牌刷新和本地 session 保存；不保存密码。
- 登录账号查看我的地图，编辑名称、作者、描述、MOD 版本、JSON 和封面，或删除地图。
- 登录账号或 guest cookie 点赞/取消点赞。
- 注册、找回密码和邮箱验证仍通过 `https://peakmap.top/account` 网页完成。

## 接手入口

1. 先读 `RECENT.md`，确认当前客户端、服务端和 UI 状态。
2. 再读 `DECISIONS.md`，尤其是点赞逻辑和 UI 暂缓决策。
3. 修改源码前读 `FILES.md`，确认源码、构建输出和 API 文档路径。
4. 上下文压缩或换 AI 后先读 `temp/2026-07-23.md`，再读正式四件套。

## 当前状态

账号、个人地图管理、上传归属和点赞同步已经接入源码；最新测试环境 DLL 已通过构建。发行目录中的 `0.1.0` 包仍是早期基础版，除非用户要求发布，否则不要把测试 DLL 直接当作已更新发行包。

