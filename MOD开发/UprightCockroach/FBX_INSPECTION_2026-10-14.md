# 用户提供 FBX 检查（2026-10-14）

## 当前结论

输入是 `3d66.com_JDH5455253615.fbx`，不是已验证身份的 Sketchfab Cockroach alien。实际预览为卡通蟑螂：双足直立、四条手臂、双触须，另有麦克风、三丛草和地面。用户已确认采用本文件、保留四臂并去掉麦克风和草丛；工作转换资源也排除场景地面，原 FBX 不改。

源 FBX 保持原样；只读取结构、提取内嵌图片并生成诊断预览。未导入游戏、未绑定、未生成 AssetBundle。

## 已核验数据

| 项目 | 结果 |
|---|---|
| 文件大小 | 23,051,232 bytes |
| SHA-256 | 8D8F6567677E0C55F1180A92EEA78EC2C598E5B7FCCCC86586C645650F36C72E |
| 格式 | FBX Binary 7500，文件标记来自 3ds Max |
| Mesh | 29 |
| 顶点 | 64,259（原始控制点总数，非 Unity 导入后拆分顶点数） |
| 三角形 | 95,802（含草、麦克风、地面等场景对象） |
| 材质 | 11 |
| 内嵌图片 | 18，均 2048 × 2048，已提取并用 Pillow 解码验证 |
| 骨架 | 0 LimbNode |
| 蒙皮 | 0 Skin、0 Cluster、0 Deformer |
| 动画 | 1 AnimationStack、1 AnimationLayer，但无 AnimationCurve/AnimationCurveNode；不能视为可用动作 |
| 坐标 | GlobalSettings Y-up，原始 Z-up，单位比例 1（FBX 厘米）；网格节点统一 -90° X 预旋转和同一几何平移 |

所有节点为 Mesh/Null，无蒙皮数据，因此本文件是静态摆姿模型。四肢已弯曲、有拿物品的姿势，接入攀爬、行走前需要重置姿态、建立关节和权重，不能只把整个 FBX 挂在 Hip 上。

## 本地产物

- `model-import/fbx-inspection.json`：完整结构、各网格包围盒、变换、材质连接、内嵌图像哈希。
- `model-import/textures/`：18 张从 FBX 提取的 JPG；源文件中的旧绝对路径不表示贴图丢失。
- `model-import/model-preview.png`：正面/侧面/背面诊断图；两三角形地面仅在预览中不显示。
- `model-import/mesh-parts.png` 与 `mesh-parts.json`：29 个部件编号及对应 FBX Geometry ID。
- `model-import/texture-contact.png`：贴图缩略图清单。
- `tools/inspect_fbx.py`：可复跑的二进制结构检查与嵌入图片提取工具。
- `tools/preview_fbx.py`：本文件专用诊断预览，使用源 Z-up 坐标、贴图三角形中心采样；不等于 Unity 材质/法线贴图渲染，不是通用 FBX 转换器。

触须是两个独立网格：编号 01 / Geometry 1698692189952、编号 02 / Geometry 1698692193216，均 336 三角形。转换资源已为其加入左右根/上下段触须骨链，并接入 config。

用户已确认保留四臂并去掉麦克风、草丛；工作资源同时排除地面，原 FBX 不变。转换资源共 24 网格、91,600 三角形、8 张实际使用贴图；新增 16 根 Scout 驱动骨、6 根额外手臂骨和 4 根触须骨。四臂关节使用离线 landmark 绑定，仍需实机动画复核。

## 下一步

1. 用户已确认使用本 FBX、保留四臂、去麦克风和草丛。原始 FBX 不修改。
2. 工作转换资源已清理场景道具并匹配 Scout bind pose；后续只做实机姿态修正。
3. 已建立身体、双腿、四臂、触须骨链；配置根挂点/位置/旋转与第一人称偏移已接入正式模型。
4. 保留原玩家 Animator、IK、物理和网络对象，客户端只替换渲染。
5. 本机发现 Unity Editor 6000.0.36f1，当前 PEAK 日志为 6000.3.15f1；尚未启动编辑器或验证资源兼容性。
6. 本地 FBX 的授权来源尚未核验，不能把另一个 Sketchfab 作品的 CC BY 4.0 自动套到此文件。公开发行前核对实际下载来源和条款。

## 同轮已完成的触须配置

`AntennaConfiguration.cs` 提供左右独立挂点名、根坐标/旋转、两段中心/旋转、长度/粗细和第一人称额外偏移，共 48 项。启动后配置保存文件变化每秒检查一次；具体说明见 `ANTENNA_CONFIG.md`。现在作用于正式转换模型的触须骨链。

同时修复构建失败先隐藏人形的问题：先建立替换部件，成功后才抑制原 Renderer；失败清理部件并保留/恢复原模型。改用 forceRenderingOff，避免卸载时恢复旧 enabled 覆盖游戏最新状态；primitive 的 Collider 在销毁前立即禁用。插件卸载清理现有视觉组件。

验证：Release 0 warnings / 0 errors；13 项真实 BepInEx 配置保存/重载/左右独立/边界值检查通过。原 FBX SHA-256 前后相同。Unity 实机视觉、坐标热更新、失败回退和场景生命周期未验收，shader 白色与身体局部轴问题仍待解决。

2026-10-14 21:20:20 确认无 PEAK 进程后，将配置版 0.1.0 DLL 部署至 `2.0.a/BepInEx/plugins/UprightCockroach.dll`，21,504 bytes，SHA-256 `2E2A49F683C280223DB1F3D7569A4B626497D862740834CD4B4D83EE403B1D7D`。旧 DLL 备份到 `.build/uprightcockroach-config/backups/20261014-212020/`；配置文件不存在时复制默认值，未覆盖用户已有配置。
