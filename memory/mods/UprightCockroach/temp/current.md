# UprightCockroach 临时恢复摘要

更新时间：2026-10-14

用户只做直立蟑螂客户端视觉替换，不做六足趴地。MVP 已构建到 2.0.a profile，但用户截图显示白色细棍与错位，未达标。现代码按 CustomizationRefs.AllRenderers 隐藏原人形，加载嵌入的四臂真实转换资源，并保留第一人称手臂/触须/部分身体；仍需实机回归。

用户先选 Cockroach alien，后提供项目根目录 `3d66.com_JDH5455253615.fbx`。已检查并保存 FBX_INSPECTION_2026-10-14.md：源文件是另一只四臂、双足直立卡通蟑螂，带麦克风/草/地面；29 网格、95,802 三角形、18 张内嵌2K贴图，无骨架/蒙皮。源文件不变，model-import 下有结构 JSON、完整提取图片、model-preview.png/mesh-parts.png。用户已确认保留四臂、去麦克风和草丛；转换资源同时排除地面。

触须 config 已实现（48项），左右根挂点/位置/角度、上下段位置/角度、长度/粗细、第一人称偏移；保存每秒检查，当前作用于真实转换模型骨链。Release 0/0，13项 BepInEx 配置测试通过。2026-10-14 21:20:20 确认游戏退出后备份并部署 21,504 bytes DLL，SHA256 2E2A49F683C280223DB1F3D7569A4B626497D862740834CD4B4D83EE403B1D7D；配置已写入profile，说明 ANTENNA_CONFIG.md。旧版备份 .build/uprightcockroach-config/backups/20261014-212020。

修复创建失败先隐藏原模型的问题；成功后用 forceRenderingOff 抑制，失败恢复/清理，卸载清理组件。shader 白色、默认局部轴、HideTheBody 状态仍待解决。FBX 已离线转换为版本化 `uprightcockroach.roach` 并作为嵌入资源读取；未使用 AssetBundle，避免 Unity 编辑器许可证/版本差异。找到 Unity 6000.0.36f1，游戏6000.3.15f1，兼容性未验证。模型选择确认后再处理 Scout 适配；本地 FBX 许可待核对，不套用 Sketchfab CC BY。
