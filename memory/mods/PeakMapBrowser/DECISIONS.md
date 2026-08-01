# PeakMapBrowser Decisions

更新时间：2026-07-30

## 服务端与客户端边界

- 服务端项目是独立的 `C:\Users\Administrator\Desktop\MOD\PEAK-MAP`，MOD 通过 HTTPS API 使用，不把 Supabase 密钥放入客户端。
- 注册、找回密码和邮箱验证不在 MOD 内实现，只打开网页账号入口。
- 客户端不保存用户密码；只保存 session token、过期信息、用户基础信息和 guest id。
- access token 只保存在进程内存，不写入磁盘。
- refresh token 使用 Windows DPAPI `DataProtectionScope.CurrentUser` 加密后写入 `session.json`；不得因为 DPAPI 失败而回退保存明文 refresh token。
- `session.json` 使用版本化格式和临时文件替换写入；旧版明文 session 仅在成功读取后迁移为加密格式。
- 上传、编辑和删除都由服务端验证账号身份与地图 owner，客户端不能自行决定所有权。
- 退出登录先尝试调用服务端 `POST /api/auth/sign-out` 撤销当前会话，再无论结果如何清理本地 session；服务端失败必须向用户提示。

## 点赞禁止回退项

- 点赞继续使用账号或 guest cookie 作为身份。
- IP 继续只做 rate limit。
- 不使用明文 IP，不新增 IP 哈希唯一约束，不改变原有点赞计数语义。

## UI 决策

- 当前继续使用 IMGUI，优先保证功能和 API 联调，不立即重写 UI 框架。
- 用户后续若重新启动 UI 重构，首选评估 Unity uGUI + TextMeshPro；PEAK 已带 `UnityEngine.UI.dll`、`Unity.TextMeshPro.dll`，适合运行时 BepInEx 注入。
- UI Toolkit 虽然存在 `UnityEngine.UIElementsModule.dll`，但当前不作为已采纳方案；不要在没有用户重新确认前改成 UI Toolkit 或 uGUI。
- 社区地图详情使用独立弹窗；弹窗期间必须禁用底层控件并隔离输入。
- 我的地图暂时保持左列表右编辑器，不要因为社区详情弹窗需求再次改成账号编辑弹窗。

## 兼容与编码

- 现有 IMGUI、Canvas 输入阻塞和网络逻辑边界不要无关重构。
- 源码和 memory 文档保持 UTF-8；PowerShell 查看中文时显式使用 `-Encoding UTF8`。
- 处理 IMGUI 文本框切换时必须清理 GUI focus/keyboard control，防止不同地图复用控件 ID 后残留选区。
