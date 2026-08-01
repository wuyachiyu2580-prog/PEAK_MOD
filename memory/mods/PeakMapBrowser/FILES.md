# PeakMapBrowser Files

更新时间：2026-07-30

## 路径

- 主项目：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapBrowser.csproj`
- 主源码目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser`
- UI 主文件：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapWindow.cs`
- API 客户端：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapApiClient.cs`
- API 模型：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\ApiModels.cs`
- session 存储：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapSessionStore.cs`
- MOD API 文档：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\API.md`
- 服务端 API 文档：`C:\Users\Administrator\Desktop\MOD\PEAK-MAP\API.md`
- 服务端账号/地图接口：`C:\Users\Administrator\Desktop\MOD\PEAK-MAP\app\api`
- 服务端迁移：`C:\Users\Administrator\Desktop\MOD\PEAK-MAP\supabase\migrations\20260720120000_add_accounts_map_ownership_and_likes.sql`、`20260720130000_add_map_json_revisions.sql`
- 测试输出：`C:\Users\Administrator\Desktop\MOD\PEAK\测试环境\BepInEx\plugins\PeakMapBrowser.dll`
- 当前发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\发行\0.1.1`
- 当前发行包：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\发行\0.1.1\wuyachiyu-PeakMapBrowser-0.1.1.zip`
- 旧发行目录：`C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\发行\0.1.0`

## 构建

```powershell
dotnet build "C:\Users\Administrator\Desktop\MOD\PEAK\MOD开发\PeakMapBrowser\PeakMapBrowser\PeakMapBrowser.csproj"
```

项目输出路径固定为测试环境的 BepInEx plugins 目录；编译前应确认 PEAK 未锁定 DLL。发行包需要从该输出复制 DLL，并单独更新 manifest、README、CHANGELOG 和 API 文档。

## 关键源码职责

- `Plugin.cs`：BepInEx 入口、配置项、快捷键和 `OnGUI` 生命周期。
- `PeakMapWindow.cs`：当前 IMGUI 页面、上传/登录/详情/图片选择器、我的地图编辑器和输入焦点处理。
- `PeakMapApiClient.cs`：登录刷新、Bearer、guest cookie、列表、上传、编辑、删除、点赞和纹理下载。
- `ApiModels.cs`：地图、账号、session、点赞、版本和分页响应模型。
- `PeakMapSessionStore.cs`：本地 session JSON，密码不落盘。
- `MapSaveService.cs`：本地 JSON/封面扫描和白名单图片选择。
