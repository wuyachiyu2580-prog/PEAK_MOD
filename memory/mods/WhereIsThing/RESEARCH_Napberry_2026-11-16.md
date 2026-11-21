# 晚安莓未采摘植株扫描调研

日期：2026-11-16。用户明确先调研、不改代码。本轮仅读取开发源码、3.1.0反编译和实际资源，记录memory；没有修改生产源码/配置、构建、部署或启动游戏。

## 结论

1. 选择独立的“自然危险 → 未摘下的晚安莓”（NapberryHypnoOrb）时，会扫描带 OrbThatMakesYouSleepy 的植株对象，并用它的 transform 定位。当前资源中对象名为 NapBerrySpawn。它是植株整体，不是单独识别根部网格，也不是扫描可采摘果实本身。
2. 只选普通“晚安莓”物品不会自动启用植株扫描。物品英文名 Napberry 与场景目标 Unpicked Napberry 不匹配，不会合并成同一目录条目。已采摘实体走物品/背包路径。额外 FakeItem 植物扫描只处理 Mandrake 与 Medicinal Root，不含晚安莓。
3. 现有缺口：场景扫描只判断植株 activeInHierarchy、目标选择、距离和地图范围，没有检查 fakeBerry、pickedUp 或果实是否仍激活。因此采摘后，只要植株根对象仍激活，就会继续标记“未摘下的晚安莓”，存在空株误标。
4. renlei反编译也未传附加过滤；该缺口不是本次多预设/客机config整合引入。

## 源码证据

- MOD开发/WhereIsThing/WhereIsThing/WhereIsThingPlugin.SceneDiscovery.cs:126：直接调用 RefreshActiveSceneTargets<OrbThatMakesYouSleepy>，无附加过滤条件。
- WhereIsThingPlugin.cs:821：按 _selectedSceneTargetTypes 启用，标签绑定 captured.transform；有效性只检查对象存在/激活/仍被选中。AddLabel另有地图和距离过滤。
- ThingTypes.cs:420 附近：目录按英文名称匹配合并；Napberry 与 Unpicked Napberry不同。
- ThingPickupIdentity.cs:19 / WhereIsThingPlugin.Plants.cs:40：FakeItem补充发现仅包含曼德拉草和药用根茎。
- 引用参考代码/反编译/3.1.0/Assembly-CSharp/OrbThatMakesYouSleepy.cs:43：果实失活后停止动画、关闭环境粒子，没有关闭植株自身；:64起催眠效果也依赖果实仍激活。
- FakeItem.cs:108：采摘只关闭果实自身GameObject、登记hiddenItems并置pickedUp=true，不关闭植株父对象。

## 资源交叉验证

- resources.assets：Item pathID 33570，GameObject/UIData.itemName 均为 Napberry。
- sharedassets4.assets：OrbThatMakesYouSleepy pathID 33446，所在对象 NapBerrySpawn，m_IsActive=true；fakeBerry指针 fileID=0/pathID=33452。
- 33452 是另一对象 FakeNapBerry 的 FakeItem，父对象Pivot。因此果实关闭与植株对象关闭不是同一操作。
- UnityPy读取标准头/外部脚本引用；按3.1字段布局读取Orb的fakeBerry PPtr offset104，raw length116。未写资源文件。

## 后续建议（未实施）

发现与现有标签同时检查：fakeBerry存在、未pickedUp且仍activeInHierarchy。标签锚点可选果实transform或明确的植株锚点。普通晚安莓与植株是否合并选择，需按后续需求决定，本轮不改变筛选语义。若表达催眠危险，还需考虑组件enabled与Hazard_NapberryHypno规则。

待实机验证自己/他人采摘和晚加入后的标签生命周期。本轮只做静态核查，不声称已实机复现；此前143项回归不覆盖这条采摘后场景标签有效性。仅同步本地memory，外部update_memory接口不可用。

## 统一为晚安莓单一目标的可行方案（未实施）

用户追问能否把未采摘植株和物品果实统一标注。沿用上一条先调研、不改代码的约束，本轮仅确认可行方案。玩家界面可以只有一个“晚安莓”入口和统一标签名；内部仍分别识别FakeItem果实与已采摘Item/背包数据。未摘时以果实位置标记，采摘隐藏或pickedUp后撤掉植株标签，已摘物品按现有地面/手持/背包范围显示。这样可以避免空株残留，不需要改游戏对象或网络状态。

FakeItemManager.RPC_RequestFakeItemPickup按realItemPrefab.itemID调用player.AddItem，再广播PickUpVisibly，因此不是同一Unity实例原地改状态，不宜承诺每颗果实的跨采摘唯一身份或零延迟连续跟踪。实现时可复用ThingTargetDefinition同时关联场景类型与物品ID的机制，但应显式关联Napberry而非依赖英文名碰巧相等；旧itemID或NapberryHypnoOrb选择均应在有效选择层兼容，取消选择需同时清掉两者。预设共享协议和旧枚举值保留。未采摘归地面范围是建议语义，未写入代码；需要覆盖旧预设/共享预设、自己和他人采摘、拾取失败恢复、晚加入及范围过滤的回归与实机。
