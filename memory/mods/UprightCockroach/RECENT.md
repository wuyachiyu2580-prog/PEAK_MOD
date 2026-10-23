# UprightCockroach Recent
## 2026-10-15 00:02 实机截图问题修正版

- 用户确认第二张截图才是当前实机画面：远端蟑螂只剩头/四肢，第一人称有巨大触须挡在中央。
- 根因修正候选：URP 材质关闭背面剔除；第一人称默认隐藏触须网格，保留腹部和手臂；模型资源仍保留第三人称触须。
- PEAK 已退出后部署 `UprightCockroach.dll`，12,261,376 bytes，SHA-256 `431C3D4EFE9E7CBE2A8DB4A5923DE6E21A4E19C603928D1CBA61D90A1F258BEB`。备份：`.build/uprightcockroach-fix/backups/20251015-000242/`。
- 待用户重新启动 PEAK 复核截图；本轮没有自动关闭或改动 Steam。

## 2026-10-14 23:35 本地 FBX 已转换、绑定并接入候选构建（当前）

- 用户确认使用 `3d66.com_JDH5455253615.fbx`，保留四臂，去掉麦克风和草丛；原 FBX 保留不改，转换资源同时去掉地面。
- 源文件 23,051,232 bytes；29 Mesh、64,259 控制点、95,802 三角形、11 材质、18 内嵌 2K 图；0 LimbNode、0 Skin/Cluster。提取全部图片、结构报告与三视图，原文件哈希不变，未绑定/游戏导入。
- 左右触须 config 48项改为控制真实转换模型的骨链；保留 `[Antenna.Left]` / `[Antenna.Right]` 兼容已有配置。模型资源使用原 FBX 内嵌的 8 张实际 diffuse 贴图，材质候选按 PEAK URP shader 创建。
- 生成 12,234,367-byte `uprightcockroach.roach`，24 网格/91,600 三角形/59,440 顶点；增加 16 根 Scout 驱动骨、6 根额外手臂骨和 4 根触须骨。候选 DLL Release 0/0，47 项模型/配置检查和离线姿态压力检查通过。PEAK 未运行时再部署；尚未游戏内验收。
- 详情 `FBX_INSPECTION_2026-10-14.md` / `ANTENNA_CONFIG.md`。本地文件实际来源许可仍需公开发行前核验。

## 2026-10-14 模型初筛与用户反馈（历史）

- 用户实机截图已证明占位外壳出现白色细棍/错位；此前“无实机证据”仅是历史记录，不再代表当前状态。代码实际是 CreatePrimitive 后 Destroy Collider，早期“不创建 Collider”的描述不准确。
- 用户要求触须坐标等可在 config 调整、导入正式模型，最新指示先搜索模型；配置和导入均未实现。
- 首选 Friendly Cockroach Character / MAROUANE：官方预览确认直立双足、双臂、长触须；元数据免费 CC BY 4.0、10,000 三角面，存在 source/glTF/GLB/USDZ 归档。未取得源文件，骨架未核验。
- 备选 Cockroach alien / danielnachon：可下载、CC BY 4.0、11,272 三角面；预览只展示拟人上半身，完整下肢/骨架待查。Cockroach Man / Charles Smith 外观符合，但未开放下载、未列可复用许可。
- 保存 `MODEL_RESEARCH_2026-10-14.md` 与 `model-candidates-2026-10-14.json`。本轮未购买、未联系作者、未下载源模型、未改代码/构建/部署。
- 内置 web 未返回正文，后通过公开官方 API/缩略图核验；BOOTH/TurboSquid 遇人机验证。不要沿用此前未核验商品 ID 或“带触须骨架”的推断。

## 2026-10-13 调研停点

- 用户选择直立蟑螂，不做六足趴地版；优先做纯客户端视觉替换。
- 推荐保留 PEAK 原有 `Character`、Animator、Ragdoll、IK、碰撞、攀爬、拿物品和 Photon 对象，只替换本地客户端的玩家 Renderer。
- 每个安装 MOD 的客户端都能看到自己和远端角色是蟑螂；未安装客户端仍看到普通人类。无需新增 RPC、房间属性或同步字段。
- 2.6.b 已安装在 `C:\SteamLibrary\steamapps\common\PEAK`；当前反编译参考为 `引用参考代码\\反编译\\2.6.a`，代码入口需用 2.6.b 程序集再次核对。
- 2.6.b `Assembly-CSharp.dll` 中仍存在 `CharacterCustomization`、`CustomizationRefs`、`HideTheBody`、`PlayerCustomizationDummy`、`CharacterSpawner` 等关键类型；这支持在角色生成后做视觉挂接。
- `CharacterCustomization.OnPlayerDataChange()` 会重新设置人类网格/材质，因此蟑螂替换必须在该流程之后重新应用，或挂在生命周期补丁/运行时组件上。
- `HideTheBody` 会按本地/远端、死亡、昏迷和搬运状态切换材质可见性；蟑螂 Renderer 必须跟随同一规则，尤其不能遮挡本地第一人称视线。
- PEAK 资源中的 `BotCharacter` 有完整 Scout 人形骨架；`MainMesh`、`Shadows`、`Skeleton`、`Chicken` 使用同一套约 120 根骨骼。最稳妥的蟑螂模型是绑定到该人形骨架，或提供可确定映射的骨骼名。
- 现有 PeakCats 插件证明可以在客户端运行时创建自定义 `SkinnedMeshRenderer` 和骨骼；其 `.catmesh` 是专用格式，不能直接作为蟑螂资源方案。

模型格式已确定为本地 FBX；转换资源和候选 DLL 已完成，下一步是 PEAK 实机回归。

## 2026-10-13 MVP

- 建立独立 net472 BepInEx 工程，Harmony 挂在 `Character.Start` 后添加 `UprightCockroachVisual`。
- 当前外壳由 Unity primitive 生成，挂到 Scout 的 `Hip/Torso/Head/Leg/Foot/Arm`，包含腹部、胸部、头、双足、双臂、翅膀、复眼和双段触须；不创建 Collider、不修改 Animator、Ragdoll、IK、Photon 或角色状态。
- 组件在 LateUpdate 持续压住原人形 Renderer，避免换装回调把人形重新显示；本地第一人称检测到相机贴近头部时隐藏外壳，第三人称仍显示。
- 构建命令已通过，0 警告、0 错误；DLL 输出到 `r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/plugins/UprightCockroach.dll`，最近一次为 13,312 bytes。
- 使用 `CustomizationRefs.AllRenderers` 精确隐藏原人形外观，避免影响背包、手中物品和其他交互可视对象；跳过 Bot、Zombie 和 Scoutmaster。
- 尚未启动游戏验证。重点风险是原 Renderer 全部隐藏后的第一人称遮挡、死亡/昏迷状态表现，以及角色生成时骨骼对象尚未完成的时序。

## 2026-10-14

- 按 2.6.b 实际字段修正外观范围：复用 `CustomizationRefs.AllRenderers`，不再按整棵角色层级抓 Renderer，避免误伤背包和手中物品。
- 补齐双足/双臂的骨段生成，优先使用 `Leg/Knee/Foot` 和 `Arm/Elbow/Hand` 之间的实际骨骼方向；跳过 Bot、Zombie、Scoutmaster。
- 从 `data.unity3d` 提取 120 根 Scout 骨骼名和 bind pose 到 `scout-reference.json`，供正式蟑螂网格绑定使用。
- 新增首轮 `TEST_PLAN.md`；当前没有运行中的 PEAK 进程，现有 LogOutput 不能作为本 MOD 的实机通过证据。
- 按用户要求调整第一人称：保留双臂/手、触须、翅膀和下腹部外壳；隐藏贴近相机的头部、眼睛、胸部和脚，避免完全遮挡视野。
