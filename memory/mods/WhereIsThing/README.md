# WhereIsThing

## 2026-09-21 发行文件准备完成

当前版本 `0.1.2` 的发行文件已按用户要求备齐（2026-09-21），目录 `MOD开发/WhereIsThing/发行/0.1.2`。包含 DLL、icon.png、README.md、CHANGELOG.md、manifest.json；未生成 ZIP、未上传。DLL 与上一轮通过构建/测试并部署的最终产物一致，完整实机验收仍待完成。

本版覆盖：放置物及 owner 识别、玩家放置预设、分类校正、分帧发现和标签优化、20000 排序、ModConfig 适配。

此前发行目录和 ZIP 保留原样；下方旧日期/旧版本状态为历史，不覆盖本节。

## 2026-09-21 当前更新

开发/测试版本 `0.1.2`，PEAK 2.4.b / ModConfig 1.8.2 / PEAKLib.UI 1.7.2 接入已更新。补齐可见菜单刷新并限制配置 UI 翻译范围。Release 0 警告 0 错误，DLL 已部署原 2.0.a profile；`发行/0.1.2` 文件已备齐（无 ZIP、未上传），完整实机验收仍待完成。

构建/测试/产物详见 `../ModConfigDiagnostics/INTEGRATION_RESULT_2026-09-21.md`。下方旧日期发布和 hash 为历史记录。

更新时间：2026-09-06

WhereIsThing 是参考 `WhereIsMyAmulet` 开发的 PEAK 多物品位置显示 MOD。当前开发/测试版本为 `0.1.2`（程序集 `0.1.2.0`），游戏基线为 PEAK `2.4.b`；本轮放置体识别和 owner 修复已通过 `0 warnings / 0 errors` 编译，但尚未完成房主/客户端实机验收，因此不得记为已发布。现有 `发行/1.0.3` 未在本轮更新，也没有创建新 ZIP。

PEAK `2.1.a`、WhereIsThing `0.1.0`/`0.1.1`/`1.0.3` 的内容是早期开发和发布历史；若与本页顶部、`RECENT.md` 或最新 `temp/` 记录冲突，以 `2.4.b` / `0.1.2` 当前状态为准。

## 当前能力

- `C` 扫描当前已选择的物品。
- 标签每帧按目标 Transform 更新位置、距离和屏幕边缘方向。
- `Alt+C` 打开选择窗口，可搜索、按类别筛选、批量全选/清除和应用。
- 同一游戏显示名的多个 prefab/`itemID` 会合并成一个窗口项，项名后显示变体数量。
- 支持地面物品、手持物品，以及落地背包内物品的背包位置。
- 支持把已选护符显示到第 1~4 关场景雕像手中的碎片位置；这是 `Statue / 雕像` 扫描范围，不是单独的目标类别。
- 支持独立追踪未打开的行李箱；行李箱来自 `Luggage.ALL_LUGGAGE`，不是 `ItemDatabase` 中的物品。
- 支持独立追踪场景目标：危险生物（森蕈僵尸、甲虫、蝎子、蜘蛛、蜂群、Scoutmaster）、自然危险（风滚草、GhostBall、地刺、蚁狮、捕蝇草、龙卷风、未摘下的晚安莓）、机关陷阱（箭矢发射器、移动锯刃、滚刺机关、摆斧机关）和雾沼钟塔；钟塔标签显示已点亮/未点亮状态。
- 窗口内可勾选显示范围：地面、手持、背包、雕像；行李箱扫描由已选行李箱类型自动启用，未选择任何行李箱类型时自动关闭。
- 分类区域内使用自适应网格，根据窗口宽度自动显示多列。
- 窗口打开期间持续显示并解锁鼠标，关闭时恢复打开前的鼠标状态。
- 物品名称使用游戏 `LocalizedText` 的名称表，窗口可切换跟随游戏、English、简体中文。
- 扫描显示支持常驻或按秒数自动隐藏。
- 支持预设选择和共享；owner 显示由一个全局开关控制，关闭后不改变目标标签和距离。
- 玩家名仅供娱乐，不作为放置者的可靠证据；中途加入可能缺少此前放置记录，部分目标仅显示物品名称和距离。英文声明已写入发行 0.1.2 README Notes。
- 玩家放置目标使用物品目录行为组件指向的生成 prefab 做正向识别；绳索 owner 取锚点 PhotonView，并排除机场、神庙、`PeakSequence`、可破坏系统绳索和海滩桥。
- 踏板菇、弹力菇、云雾菇通过本地 `OnItemThrown` 证据尝试匹配放置者；无法唯一确认时仍显示名称和距离，但不显示玩家名。魔豆始终不猜测 owner。
- 物品继续使用互斥的用途分类；ItemSpawnerEnhanced 审计只用于精确 prefab 覆盖，不新增 Deployable/Consumable 多标签 UI。Jetpack/Rocketpack、Heat Pack、Healing Dart、放置工具、神秘变体、棋子等已修正分类，仍待实机检查窗口显示。

## 历史分类基线（2.1.a）

物品列表不是硬编码名单，而是从 `ItemDatabase.itemLookup` 动态读取。以下类别与 194 个物品 ID 审计来自 PEAK `2.1.a`，保留为历史分类依据；2.4.b 的运行时目录仍需实机确认：

- 特殊物品：Scout Amulet、Golden Idol、Book of Bones、Bing Bong 标签。
- 神秘物品：Mystical 标签。
- 食物：PackagedFood、Berry、Mushroom、GourmandRequirement 标签。
- 医疗与状态：Bandage、Medkit、Antidote、Cure、Sunscreen 等名称。
- 容器与背包：Backpack、Bag、Pack、Chest、Case 等名称；行李箱目标另列。
- 攀爬装备：Rope、Piton、Climbing、Spike、Hook、Grapple 等名称。
- 移动装备：Parachute、Parasol、Glider、Rocketpack、Jetpack、Balloon 等名称。
- 照明：Lantern、Torch、Candle、Flare、Flashlight 等名称。
- 导航与观测：Compass、Binocular、Bugle、Guidebook、Passport、Map 等名称。
- 武器与爆炸物：Dagger、Dart、Cannon、Dynamite、Gun、Weapon、Sword、Bomb 等名称。
- 生物：Bird 标签及 Bird、Beetle、Scorpion、Spider、Frog、Bug、Egg 等名称。
- 玩具与运动：Basketball、Ball、Toy、BingBong、Boombox、Record 等名称。
- 生存工具：Checkpoint Flag、Conch、Magic Bean、Megaphone、Portable Stove、Stick、Stone 等名称。
- 其他：未命中标签或名称规则的物品，仍然可被选择和显示。

这些类别用于窗口筛选，不限制实际可追踪物品。后续实机发现特殊物品或误分类时，优先增加游戏标签判断或精确名称规则。

## 2026-08-16 杂项审计（历史）

初次审计时的 33 个 `Misc` 显示名组已经按本轮方案重分类：早起虫儿归入生物，Aloe Vera 和 First Aid Kit 归入医疗，AK 和 Chain Launcher 归入武器，Frisbee 和棋子归入玩具与运动，莓蕉皮、椰子、热狗、棉花糖、王莓、怪脆莓和 Fungus 归入食物，Checkpoint Flag、Conch、Magic Bean、Megaphone、Portable Stove、Stick、Stone 归入生存工具，Scoutmaster's Soul 归入特殊物品，Beehive 归入生物。

重新按 2.1.a `resources.assets` 的 194 个物品 ID运行分类规则后，资源快照中剩余 `Misc` 为 0 组。物品变体仍按英文显示名合并，不会因为重新分类而重新产生重复入口；运行时仍需实机确认个别资源标签和名称是否与资源快照一致。

## 接手顺序

先读 `RECENT.md`，再读 `DECISIONS.md` 和 `FILES.md`；压缩恢复或换 AI 后先读 `temp/` 下最新日期文件。
