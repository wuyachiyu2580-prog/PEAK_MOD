# UprightCockroach 当前状态

## 2026-10-23 PEAK 3.0.0 兼容性审核

0.1.0隔离Release 0/0，Character.Start/渲染API仍在，14配置+49模型/配置检查通过。保留现有实机画面、骨骼、多人和生命周期待验项。

完整结果：[3.0.0审核](../../COMPATIBILITY_3.0.0_2026-10-23.md)。本轮未改源码/版本或部署DLL，构建通过不代表实机兼容已验收。

- 2026-10-14：已建立 `MOD开发/UprightCockroach/UprightCockroach/` 的 BepInEx net472 MVP。
- 2026-10-14 23:38:30：正式模型候选 Release 0 警告、0 错误；47 项模型/配置检查和离线姿态压力检查通过。确认游戏退出后部署 `2.0.a` profile。DLL 12,261,376 bytes，SHA-256 `163D9AF10E58B0FB9C33498B0C173916F5603661CFD4D18B21E12A844F412566`，旧 DLL 与配置已备份。
- MVP 在 `Character.Start` 后挂载运行时直立蟑螂外壳，使用 Scout 的 `Hip/Torso/Head/Leg/Foot/Arm` 骨骼生成身体、四肢、翅膀、眼睛和触须；按 `CustomizationRefs.AllRenderers` 隐藏原人形外观，不按整个 CharacterCustomization 子树隐藏。
- 尚未进行游戏内单人/多人、死亡/昏迷/搬运/复活和场景切换验收；2026-10-15 00:02 已部署修正版：关闭 URP 导入材质背面剔除，第一人称默认隐藏贴脸触须、保留腹部和手臂。
- 用户已提供实机截图，显示白色细棍、部件位置异常，当前占位外壳外观未达标。编译通过不代表实机通过；进程状态必须临时重查，不沿用旧的“未运行”记录。验收步骤见 `MOD开发/UprightCockroach/TEST_PLAN.md`。
- 第一人称策略已改为显示蟑螂手臂/手、触须、翅膀和下腹部；需要实机确认位置是否自然。
- 用户随后选择 Cockroach alien；再提供 `3d66.com_JDH5455253615.fbx`。本地预览显示此文件为另一只四臂、双足直立卡通蟑螂，另带麦克风/草/地面；未自动等同所选 Sketchfab 作品。
- FBX 23,051,232 bytes；29 网格、95,802 三角形、18 张内嵌 2K 贴图；0 骨架、0 Skin/Cluster。原文件不变；图片提取与三视图完成，正式绑定/AssetBundle/游戏导入未完成。
- 2026-10-14 23:11：用户确认使用本地 FBX，保留四臂，去掉麦克风和草丛。原文件保留，工作资源同时排除场景地面；进入绑定与导入。
- 触须 config 已实现：左右独立挂点名、根位置/角度、上下段位置/角度、长短/粗细、第一人称偏移；保存每秒检测重载。当前只作用于占位模型，运行时变换仍需实机验证。
- 替换部件成功后才隐藏原模型，失败清理并恢复；原 Renderer 用 forceRenderingOff 抑制，primitive Collider 创建后立即禁用再销毁。完整 HideTheBody 状态、shader 白色和身体局部轴问题仍待修复。
