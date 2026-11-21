# WhereIsThing：反编译改动整合与识别能力调研

日期：2026-11-16；本机 Get-Date 已确认。用户认可此前对比中的其余改动，要求客机限制改成默认开启的 config，并调研类似毒性/伪装身份的可区分功能。

## 本轮结果

已修改开发源码并完成隔离构建，**未部署、未启动游戏、未更新发行包**。插件/程序集仍 0.1.3 / 0.1.3.0，开发构建标识 `0.1.3-dev.20261116.presets`。下方候选能力仅调研，没有全部实现。此状态覆盖此前“仅对比、未授权移植”的结论；原比较报告保留为历史依据。

## 已实施

- `Presets/RestrictClientPresets = true`，游戏设置显示“客机预设限制”。开启时在线非房主使用房主共享预设，房主未共享则使用保底预设；关闭时使用/编辑自己的本地预设。房主及离线不受限制。这是本地偏好，**不是房主强制规则**。中英文名称/说明、归属键检查均已接入现有 ModConfig 本地化。
- 限制的实际状态变化（配置、入退房、房主切换）会关闭旧编辑窗口并清除编辑 ID，刷新有效目标与预设窗口；编辑/保存/删除/重命名/发布回调再次检查当前权限，避免旧 UI 回调写入。未改共享协议或游戏网络权威。
- 本地多预设合并目标：新增隐藏键 `ActiveLocalPresetIds`，去重、剔除失效 ID，兼容旧单选 `ActiveLocalPresetId`；至少保留一个启用。编辑预设不改变启用状态；新建加入启用列表。受限客机沿用单个共享预设。
- 按物品 ID 识别对应放置体，需启用内置 `preset-player-placed`。显式场景目标选择依然生效。
- Cluster / Lace / Normie 三类蘑菇的普通、Poison 六种 prefab 分组独立；去除末尾 `(Clone)` 后识别实例，菜单、标签添加中英文品种后缀。旧 itemID 选择保留，原先已选的两种不会擅自清掉。
- 后缀用“有毒品种 / 无毒品种”，不是保证当前可安全食用：烹饪等实例变化可能增加/改变毒性。未知蘑菇不标成无毒。
- 网络地面物品过滤 `blockInteraction`，以及不能放进口袋或背包的 Flare；标签有效性持续检查。
- 创建标签前检查实际标签位置；超过 MaxDistance 隐藏，超过 MaxDistance+10m 移除，0 保持不限距离。逐帧更新共享相机位置，距离平方筛选先于屏幕投影，隐藏时 SetActive(false)。
- 没有照搬改版的 PhotonView 根位置发现裁剪：绳索真实标签位置可能不同于根位置，保留发现记录，使用 positionProvider 的实际位置筛选，避免漏标。

## 验证及产物

- 回归 **143/143**：原有 71 项及新增 72 项，实际生产 helper 参与编译，覆盖限制真值组合、旧配置迁移、多选并集、放置物门控、六种蘑菇/Clone/语言、地面过滤、距离边界。
- Release 构建：**0 警告、0 错误**，引用当前安装的 PEAK 3.1.0 基线。
- `git diff --check -- MOD开发/WhereIsThing` 通过；Git 的 LF/CRLF 提示不是构建错误。
- 输出 `.build/whereisthing-research-20261116/bin/WhereIsThing.dll`，185344 bytes。
- DLL SHA256：`D9456F7E5B72CF2EFCA9B7BA190F1358A4FB330021B81BBE1A7B4F52C4CE8B34`。
- 日志：同目录上层 `build.log`、`regression.log`、`build-identity.json`。
- 构建命令：`dotnet build MOD开发/WhereIsThing/WhereIsThing/WhereIsThing.csproj -c Release -p:OutputPath=C:/Users/Administrator/Desktop/MOD/PEAK/.build/whereisthing-research-20261116/bin/ --nologo`。默认 csproj 仍指向 2.0.a/plugins 根目录；验证必须覆盖 OutputPath。
- 不覆盖用户配置或已有安装；未改 WhySoLaggy 的既有工作。未提交、打包或上传。

## 尚须实机验证

1. 默认受限客机的共享/保底预设，关闭限制后多选，再重新开启；入退房、房主迁移、窗口打开期间切换。
2. 旧配置升级、重启持久化、编辑未启用预设、删除启用预设及最后一个不可停用。
3. 普通/毒蘑菇地面、手持、背包名称及过滤；中文字体、窗口布局。
4. MaxDistance 与+10m进出范围、离开后重新发现、绳索标签真实位置、已点燃信号棒。
5. 保留此前植物/箱子/段范围/自定义地图验收清单。

逻辑回归不能替代 Unity 渲染、物理、性能或多人实测。外部 update_memory 接口当前不可用，仅维护本地 memory。

## 额外能力调研（尚未实施）

依据当前游戏代码及 `resources.assets`，详细证据保存为 `.build/whereisthing-research-20261116/asset-evidence.json`，复核脚本 `asset_evidence.py`。不是根据网上旧攻略推断。

### 现在已经支持的识别

- **曼德拉草与药用根茎**：`ThingPickupIdentity.Resolve` 按 `Item.isSecretlyOtherItemPrefab` / `FakeItem.realItemPrefab` 解析真实拾取身份，保留现场载体位置。此次资源扫描的182个 Item/Backpack 中，只有 `Mandrake_Hidden` 的秘密替身引用非零，指向 Mandrake（pathID 30029）。只说明这份 resources 范围，不能推断所有场景/AssetBundle 都没有其他替身。
- **普通箱、陷阱箱、诅咒箱**：现有分类和 `WhereIsThingPlugin.Luggage.cs` 已处理，开箱后仍激活的陷阱继续显示。
- **三类毒/无毒蘑菇**：此前开发版没拆，本轮已整合；不是额外待做项。

### 建议优先做的三项

| 候选 | 当前确认 | 建议实现边界 |
|---|---|---|
| 有毒浆果提示和风险筛选 | Green Crispberry、Yellow Kingberry、Pink Berrynana 含启用的 Action_InflictPoison；Green Kingberry 在 cast_finished 触发 Poison +0.05 | 不只看名字 Poison 后缀，读取启用且会触发的效果组件；确认烹饪变化后再给当前风险结论 |
| 五色 Shroomberry 本局实际效果 | 红/黄/绿/蓝/紫分别索引0/1/2/3/4，useDebugEffect=false，读取 MushroomManager 的本局映射 | 标签可写加速/无敌/致盲等，支持“有益/危险”筛选；不要把颜色写死为一种效果，未初始化/晚加入数据未知时不猜 |
| 生熟/烤焦状态及当前毒性 | CookedAmount=0原始、1熟、2全熟、3烤焦、>=4烧毁；默认烹饪>=4会新增/增加 Poison 0.1 | 名字展示实例状态；品种与当前风险分别说明；地面/手持读 item.data，背包读 slot.data，不能只看 prefab |

蘑莓随机效果包括无限体力、加速、低重力、无敌、恢复，以及爆炸推力、致盲、昏倒、孢子、麻木。五色资源都额外带有启用且 OnConsumed 的 **Spores +0.05**，因此“随机效果有益”不等于完全无负面作用；体力额外值读取 mushroomStamAmt×0.05。`MushroomManager` 每次生成重新随机，实例非空仍可能是默认全0数组；初始化时机及主客机/晚加入一致性需要实机验证。

### 次优先项

- **剩余次数/燃料**：ItemUses、UseRemainingPercentage、Fuel 已有实例数据；可筛选“还能用的道具”。缺数据必须未知，不能当0或满；不同类型 Fuel 单位和上限不同，逐类适配。
- **同名蛋差异**：EggRaven 与 Egg 资源都显示 Egg，基础 Action_RestoreHunger 为0.075 / 0.15，均为 cast_finished。可加品种或收益说明，但先核对可生成性及官方名称，优先级低于食品风险。

### 暂不建议直接加的判断

- `MirageLuggage` 当前只切渲染材质 `_DoMirage`，不足以认定“假箱/无掉落”。
- 资源包含棋子、旧 prop、未使用残留，同名不同 prefab 不应一律拆成玩家菜单条目。Lollipop_Evil 已叫 Follypop，Cure-Some/Cure-All 名字不同，不属于额外伪装识别。
- 不对所有食物套一个烹饪公式：ignoreDefaultCookBehavior / ignoreDefaultPoisonBehavior / AdditionalCookingBehavior 会覆盖默认逻辑，部分行为会替换物品或爆炸。
- 只读标签不要调用 `Item.GetData` 补默认数据；该方法会初始化/注册条目。用 `TryGetDataEntry`，不修改道具、RPC或网络权威。

## 证据与复核方法

代码基线目录：`引用参考代码/反编译/3.1.0/Assembly-CSharp/`。

- `MushroomManager.cs`：本局映射生成；`Action_RandomMushroomEffect.cs:25` 起：效果/体力读取与十种效果。
- `ItemCooking.cs:213` 起：默认烹饪效果，含>=4毒性变化；`Item.cs:482`：烹饪名称；`:1222`：GetData 写入副作用；`DataEntryKey.cs`：实例键。
- `Action_InflictPoison.cs`、`Action_ModifyStatus.cs`、`ItemAction.cs`：毒性数值、正负值与事件触发语义。
- `MirageLuggage.cs`：渲染变化边界。
- 开发侧 `ThingPickupIdentity.cs` / `WhereIsThingPlugin.Plants.cs` / `WhereIsThingPlugin.Luggage.cs`：现有身份/箱子能力。
- `MOD开发/ItemInfoCN/ItemInfoCN/Plugin.Processing.cs:451`：本局蘑莓效果读取可作思路参考，不等于该旧模块已完成3.1兼容。

当前 Assembly-CSharp.dll SHA256：`A3643F11DC444D86491270B224C0A0CAF9126A10B26731FEB97FC85AD1D82038`。

resources.assets SHA256：`4132CFBB2A2C859708A448B79265002A3B335C81A2A4604BE869709C2959AFCC`。

UnityPy 1.25.3 读取 MonoBehaviour 标准头、PPtr解析外部MonoScript、GameObject，并按3.1反编译字段核对原始序列化数据；没有完整MonoBehaviour typetree。关键偏移：Item秘密替身 PPtr 84，UIData.itemName 长度112/文字116；ItemAction十个bool 32..68，派生效果字段72。脚本保留 enabled、triggers、pathID、原始数值和哈希，不把 disabled 组件当有效效果。扫描182个资源，其中35个相关条目入证据；**不是182件正常可获得物品清单**。
