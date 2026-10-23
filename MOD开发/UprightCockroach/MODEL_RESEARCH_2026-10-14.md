# 直立蟑螂模型初筛（2026-10-14）

> 后续更新：用户在初筛后选择 Cockroach alien，随后提供本地 `3d66.com_JDH5455253615.fbx`。本地文件是另一种四臂卡通造型，详见 `FBX_INSPECTION_2026-10-14.md`；正在等用户确认使用哪款及四臂处理。下方“首选 Friendly”仅为当时初筛建议。

## 结论

优先检查 **Friendly Cockroach Character / MAROUANE**：官方预览可见完整直立身体、双臂双腿、双触须，外形是卡通蟑螂，适合作为本项目的首轮导入候选。公开接口确认免费、CC BY 4.0、10,000 三角面，存在原始包、glTF、GLB 和 USDZ 归档。

**尚未取得模型源文件，也未检查骨架、蒙皮权重或导入 PEAK。** 页面没有声明可用人形骨架；0 个动画不等于没有骨架。GLB 归档存在不代表当前已下载。未登录的官方下载 API 返回需身份验证，不从预览数据还原模型，也不绕过登录。

当前程序化白色细棍外壳不合格；正式模型导入仍需要适配材质、Scout 骨骼和第一人称可见性，不能把候选存在当成已完成导入。

## 1. 首选：Friendly Cockroach Character

- 作者：MAROUANE。
- 页面：https://sketchfab.com/3d-models/friendly-cockroach-character-53bc86f393034470ad462b9c45f9bacb
- 官方元数据：https://api.sketchfab.com/v3/models/53bc86f393034470ad462b9c45f9bacb
- 可见造型：棕色卡通蟑螂、双臂平举、双足直立、长触须、分节腹部；符合用户不做六足趴地版的方向。
- 面数：10,000 三角面；动画数：0。
- 费用/下载：作者明确免费；平台 isDownloadable=true、downloadType=free。原始归档约 1.24 MB；公开归档状态显示 source / glTF / GLB / USDZ 均存在。原文件名仅为 model，没有证明它是 FBX 或 Blend。
- 授权：页面标注 CC Attribution，对应 CC BY 4.0；保留作者、来源、许可和修改说明。仍须核对取得的包内说明。
- 未核验：骨架、触须骨骼、蒙皮、纹理尺寸、子网格拆分和源文件格式。
- 我的判断：优先下载 GLB 检查；若无骨架，需要给模型绑定 Scout 骨架并给左右触须增加独立骨链，不能直接交给 PEAK 播放人形动画。

## 2. 备选：Cockroach alien

- 作者：danielnachon。
- 页面：https://sketchfab.com/3d-models/cockroach-alien-e6f40a1727c34931aa7af4eab920e703
- 官方元数据：https://api.sketchfab.com/v3/models/e6f40a1727c34931aa7af4eab920e703
- 预览可见：拟人上半身、甲壳、手臂和双触须，偏外星怪物风格；当前缩略图没有完整展示下肢，不能据此确认完整双足拓扑。
- 面数：11,272 三角面；动画数：0。
- 平台标记可下载、CC BY 4.0；源文件格式和骨架均未核验。
- 我的判断：作为外观备选，优先级低于可完整核对双足造型的 Friendly。

## 3. 外观参考：Cockroach Man

- 作者：Charles Smith (Blitz Mobile Apps)。
- 页面：https://sketchfab.com/3d-models/cockroach-man-7720b19962034d6e96ef37de034559a5
- 官方元数据：https://api.sketchfab.com/v3/models/7720b19962034d6e96ef37de034559a5
- 官方预览确认：双足直立、背部大翅壳、双长触须；比 Friendly 更写实。
- 面数：31,656 三角面；页面有 1 个动画。
- 当前 isDownloadable=false，未列出可复用许可；不能当成现成可用素材。骨骼结构、售价和交付格式未知。
- 我的判断：造型很贴近需求，但仅作参考；若以后要使用，需要作者提供正式模型和授权，本轮未联系作者。

## 排除项与检索限制

- Cockroach / CK：标签为 6legged，排除。
- Roach Mannequin：官方预览是六足体态，虽然作者提到 FBX / IK，仍不符合方向；作者也表示部分素材来源不完全确定。
- Creepy girl/cockroach Hybrid：官方预览为横卧混合怪物，排除。
- Dr. Cockroach、Oggy、Project Playtime 等角色素材不作为发行首选；本轮未核验其原始权利链。
- BOOTH / TurboSquid 页面当前返回人机验证，未核验具体付费候选、价格或授权。不要沿用此前未核验的商品 ID。
- 内置 web 搜索与打开没有返回可引用正文；后改用公开 Sketchfab API 和官方缩略图核验。证据保存在同目录 JSON，字段未包含预览网格或下载凭据。

## 已授权后续工作（本轮先选型）

1. 触须配置：挂点骨骼名、左右根部局部位置/旋转、长度/粗细；说明数值相对哪个骨骼，第一人称需要独立偏移。
2. 取得候选源文件后，检查 GLB skins / joints / weights、贴图与许可文件；不能仅凭商品预览判断绑定完成。
3. 在 Blender 调整站姿、身高和身体比例，匹配 Scout bind pose；处理权重与左右触须骨链。
4. Unity 构建适配游戏版本的 AssetBundle，挂接原角色动画；第一人称头部、手臂、身体分别控制可见性。
5. 模型加载成功后才隐藏原人形；加载失败保留/恢复原模型。修复游戏 shader 颜色与局部轴问题。
6. 编译验证与游戏画面验收分开记录；用户现有截图已证明占位外壳存在实机视觉缺陷。

本轮仅选型并保存证据，没有购买、下载源模型、修改插件代码、构建或部署 DLL。

