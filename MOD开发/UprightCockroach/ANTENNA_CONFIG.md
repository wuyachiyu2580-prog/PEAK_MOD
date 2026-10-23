# 触须配置

配置路径：`C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/config/wuyachiyu.peak.uprightcockroach.cfg`。

两个分组 `[Antenna.Left]` / `[Antenna.Right]` 独立；现在控制导入模型的触须骨链，并保留旧分组名以兼容已有配置。游戏运行时保存文件，插件每秒检测一次文件更新时间并重载；首次使用新 DLL 需要重新启动游戏。现在控制正式 FBX 转换资源的触须骨链。

| 键 | 作用 |
|---|---|
| AttachmentBone | 精确的 Scout 骨骼名，默认 Head；无效名字回退到 Head |
| PositionX/Y/Z | 整根触须相对挂点骨骼的局部位置偏移 |
| RotationX/Y/Z | 整根触须相对挂点骨骼的局部欧拉角，单位度 |
| FirstPersonOffsetX/Y/Z | 仅本地第一人称增加的位置偏移，同一挂点局部坐标系 |
| LowerPositionX/Y/Z | 下段骨相对导入姿态的位置偏移 |
| LowerRotationX/Y/Z | 下段骨的局部旋转偏移 |
| UpperPositionX/Y/Z | 上段骨相对下段骨的位置偏移 |
| UpperRotationX/Y/Z | 上段骨的局部旋转偏移 |
| LengthScale | 两段长度倍率，默认 1，范围 0.05–5； |
| ThicknessScale | 两段粗细倍率，默认 1，范围 0.05–5 |
| FirstPersonLengthScale | 本地第一人称触须长度倍率，默认 0.3，范围 0.05–2 |
| FirstPersonThicknessScale | 本地第一人称触须粗细倍率，默认 0.55，范围 0.05–2 |

重要：Scout 骨骼局部轴不保证与世界 X/Y/Z 一致，局部 Y 不一定向上。这里的坐标是挂点的局部数值；请一次只调一个轴观察方向。左右不自动镜像，以便独立修正。

例如修改 `[Antenna.Left]` 下 `PositionX = 0.05` 可把整根左触须沿挂点局部 X 平移 0.05；将其改回 0 则恢复根偏移默认值。此例不是已验证的最佳位置。

新模型默认上下段偏移为 0，使用转换资源的导入姿态；旧配置已在本次部署前备份并重置这些段偏移。第一人称默认把触须移到左右边缘，并缩短、变细；如果仍遮挡准星，优先调小 `FirstPersonLengthScale`，再调 `FirstPersonOffsetX/Y/Z`。所有参数只影响本地显示，不发送 RPC。

## 第一人称头部和翅壳

`[FirstPerson]` 分组控制本地视角的额外视觉：

| 键 | 作用 |
|---|---|
| HeadVisible | 是否显示缩小后的头部 |
| HeadScale | 头部缩放，默认 0.32 |
| HeadOffsetX/Y/Z | 头部相对角色局部坐标的偏移 |
| WingShellVisible | 是否显示翅壳 |
| WingShellScale | 翅壳缩放，默认 0.9 |
| WingShellOffsetX/Y/Z | 翅壳相对角色局部坐标的偏移 |
| AntennaVisible | 是否显示第一人称触须 |

检查工具：`dotnet build MOD开发/UprightCockroach/tests/ConfigChecks.csproj --configuration Release --nologo`；构建后运行 `.build/uprightcockroach-config/checks/ConfigChecks.exe <临时输出目录>`。不要把测试输出目录指到玩家配置目录。已通过 14 项配置检查，游戏内画面仍待验收。
