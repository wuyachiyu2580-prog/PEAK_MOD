# WhereIsThing-renlei 0.1.3.1 与开发版 0.1.3 对比

日期：2026-11-16。用户要求查看新增反编译与 MOD开发 的差异。本轮为静态源码对比，仅更新本地 memory；没有修改生产源码、配置、DLL、发行文件，没有构建、部署或启动游戏。

## 对照范围和版本

- 反编译：`引用参考代码/反编译/BepInEx/plugins/WhereIsThing-renlei/`。BepInPlugin/PluginVersion 为 0.1.3.1，AssemblyVersion/FileVersion 为 0.1.3.1。目录名不作为作者或来源证明。
- 开发：`MOD开发/WhereIsThing/WhereIsThing/`。PluginVersion/csproj 为 0.1.3，AssemblyVersion/FileVersion 为 0.1.3.0。
- GUID 都是 `com.wuyachiyu.WhereIsThing`，不是两个独立插件身份。开发区现有 Git 差异为空。
- 开发源码有 17 个业务 C# 文件（不含 Properties/bin/obj）；反编译有 24 个。主要因 partial 合并及一个源码文件内的多个类型拆分，不能把文件数或代码行数当新增功能。
- 已按方法、字段、配置键、字符串和关键调用链对照，区分显式转换、枚举数值、常量内联、lambda 重写等反编译噪声。未进行逐 IL 等价证明。

## 确认的行为变化

### 1. 本地预设由单选改为多选合并

- 新增隐藏配置 `Presets/ActiveLocalPresetIds`，保存逗号分隔 ID 列表；旧 `ActiveLocalPresetId` 保留兼容，读取时可迁移，保存时写入列表首项。已有其他 23 个配置键保留。
- 物品 ID、箱子类型、场景类型分别取所有启用预设的并集。
- 同一个按钮变为启用/停用；最后一个启用预设不能关闭。新建预设插入启用列表首位；删除后的空列表回退第一个预设。
- 编辑某个预设只设置编辑目标，不再自动切换当前启用预设；编辑未启用预设不等于启用它。
- 证据：反编译 `WhereIsThingPlugin.cs:399`、`:1529`、`:1658`、`:1699`、`:1775`、`:1841`、`:1878`、`:1897`；`ThingPresetPickerWindow.cs:404`、`:635`。开发对照 `WhereIsThingPlugin.cs:1495`。

### 2. 客机预设限制被彻底放开

- 开发版 `IsClientPresetRestricted()` 返回“已联机且非离线且不是房主”。
- 反编译该函数直接 `return false`，因此正常界面和扫描始终走本地预设路径；客机也可编辑并组合自己的本地预设。
- 原有房主发布、房间属性接收、共享预设单选代码还在，协议版本仍 2，`WIT.*` 属性键没变。但这些保留代码不代表客机正常界面仍会使用房主共享预设。房主发布仍可能服务旧版客机；未做混版联机验收。
- 证据：反编译 `WhereIsThingPlugin.cs:2050`、`:1775`、`:1824`、`:2161`；开发 `WhereIsThingPlugin.cs:1691`。
- 这是明确的产品行为变化，不能作为无副作用优化直接搬回。

### 3. 玩家放置物改为预设门控

- 开发版只要选中了对应物品 ID，就可显示该物品的放置形态。
- 反编译增加 `_labelPlacedFromItemSelection`，仅在启用了内置 `preset-player-placed` 时开启“按物品 ID 匹配放置物”。传统场景类型选中分支仍保留。
- 因此仅在自定义/生存预设勾选绳索、蘑菇等，不一定继续显示其放置形态；内置玩家放置物预设启用后才恢复该匹配路径。
- 开关针对合并后的整个物品选择集合，不是只针对玩家放置物预设自己的 ID。
- 证据：反编译 `WhereIsThingPlugin.cs:1775`、`:2952`；开发 `WhereIsThingPlugin.Discovery.cs:482`。

### 4. 三类蘑菇拆分有毒/无毒

- 为 `Mushroom Cluster`、`Mushroom Lace`、`Mushroom Normie` 及各自 ` Poison` 版本设置 6 个独立分组键，避免按同名合并。
- 显示名称追加中文“（有毒）/（无毒）”或英文“(Poisonous)/(Safe)”；字体探测文本补充蘑菇和毒性字样。
- 这是对 6 个具体 prefab 名的规则，不是对任意食物或全部蘑菇运行毒性分析。
- 证据：反编译 `ThingCatalog.cs:98`、`:271`、`:284`、`:303`、`:872`；开发 `ThingTypes.cs:467`、`:930` 附近。

### 5. 地面物品增加可交互过滤

- `IsReusableGroundItem` 排除 `blockInteraction=true` 的物品，以及有 Flare 组件且同时不能放入口袋/背包的物品。创建标签和已有标签有效性检查都使用此条件。
- 当前游戏 3.1.0 的 `Flare.Update` 会在信号棒点燃后将两个收纳标志设为 false，因此这会过滤点燃后丢在地上的信号棒。
- 检查加在网络地面 Item 路径；不能概括为所有手持、背包、植物、放置体都使用相同过滤。
- 证据：反编译 `WhereIsThingPlugin.cs:2805`、`:3589`；游戏 `引用参考代码/反编译/3.1.0/Assembly-CSharp/Flare.cs:27`。

### 6. 远距离标签和发现开销优化

- 新建标签前、初次为 PhotonView 建索引前，以及部分场景目标扫描，先做距离平方判断。
- 使用 `MaxDistance + 10m` 的缓冲范围；显示仍按 MaxDistance。超过缓冲范围的已有标签被移除，而开发版主要隐藏标签但继续保留。MaxDistance≤0 仍不作距离限制。
- 每帧共用相机位置；远目标在屏幕投影/开平方前返回。隐藏时除 alpha=0 外还关闭标签根 GameObject，构造时初始禁用。
- 原发现预算仍为每帧 128 个网络对象/0.75ms、重试 32 个/0.25ms，网络刷新 0.5秒、场景全量复核5秒、场景20步。不是全扫描算法重写。
- 证据：反编译 `WhereIsThingPlugin.cs:519`、`:1134`、`:1179`、`:1210`、`:2450`；`ThingLabel.cs:165`、`:319`；开发 `ThingLabel.cs:122`、`:215`。
- 静态结构支持减少远距离标签更新工作；没有实机数据，不能量化 FPS/内存收益。

## 小问题及验证边界

1. 版本日志不一致：BepInPlugin 是 0.1.3.1，但反编译 `WhereIsThingPlugin.cs:440` 的启动日志仍硬编码 v0.1.3。
2. 毒性后缀匹配直接用 `gameObject.name`，没有去除 `(Clone)`。若实际实例保留 Clone 后缀，地面/手持实例的毒性后缀可能匹配失败；目录和背包 prefab 不一定受影响。此为静态条件风险，未实机证明。
3. 远距离首次索引用 PhotonView 根节点位置，与绳索等最终标签位置不一定相同；长目标和距离边缘进出后的重发现需实测。已有索引并未同样统一按距离清除，不能宣称所有远端缓存都被回收。
4. 新 ActiveLocalPresetIds 是 Hidden 配置，但 ModConfigLocalization 的已知键列表未增加它；目前主要由专用窗口管理，不据此认定可见菜单故障。
5. 同一 GUID/配置文件，不能把这两份当作互不影响的插件并存。多选列表与旧单选回退共用配置，切回旧版只会理解单选字段。
6. 当前关卡/全图、植物真实拾取身份、箱子筛选、owner 来源、主要 ModConfig 适配仍保留开发版的对应机制；没有发现这些机制被整体重写。
7. 反编译 csproj 没有原工程的 HintPath/OutputPath，含 Unknown result type 和空 PrivateImplementationDetails 文件。它是比较参考，不能当已验证可直接编译的完整开发工程。

## 后续

本轮对比已完成，未授权或执行合并。若继续移植，应分别决定多预设、客机自由配置、放置物门控三个行为，补毒性实例名和范围边缘回归，再沿用开发版工程做隔离构建及实机验证。旧开发版的 71 项测试不证明这份反编译改版已通过。当前没有 update_memory 实体接口，仅同步本地 Markdown，不声称更新外部记忆服务。
