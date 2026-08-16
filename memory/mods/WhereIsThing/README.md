# WhereIsThing

更新时间：2026-08-14

WhereIsThing 是参考 `WhereIsMyAmulet` 为 PEAK 2.1.a 准备的多物品位置显示 MOD。当前为可编译的 0.1.0 基础版，已经接入物品数据库、选择窗口、实时标签、快捷键和配置持久化，尚未完成进游戏实机验证和发布包制作。

## 当前能力

- `C` 扫描当前已选择的物品。
- 标签每帧按目标 Transform 更新位置、距离和屏幕边缘方向。
- `Alt+C` 打开选择窗口，可搜索、按类别筛选、批量全选/清除和应用。
- 同一游戏显示名的多个 prefab/`itemID` 会合并成一个窗口项，项名后显示变体数量。
- 支持地面物品、手持物品，以及落地背包内物品的背包位置。
- 支持独立追踪未打开的行李箱；行李箱来自 `Luggage.ALL_LUGGAGE`，不是 `ItemDatabase` 中的物品。
- 窗口内可勾选显示范围：地面、手持、背包、行李箱。
- 分类区域内使用自适应网格，根据窗口宽度自动显示多列。
- 窗口打开期间持续显示并解锁鼠标，关闭时恢复打开前的鼠标状态。
- 物品名称使用游戏 `LocalizedText` 的名称表，窗口可切换跟随游戏、English、简体中文。
- 扫描显示支持常驻或按秒数自动隐藏。

## 可显示类别初稿

物品列表不是硬编码名单，而是从 2.1.a `ItemDatabase.itemLookup` 动态读取。当前窗口会按以下类别归类：

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
- 其他：未命中标签或名称规则的物品，仍然可被选择和显示。

这些类别用于窗口筛选，不限制实际可追踪物品。后续实机发现特殊物品或误分类时，优先增加游戏标签判断或精确名称规则。

## 接手顺序

先读 `RECENT.md`，再读 `DECISIONS.md` 和 `FILES.md`；压缩恢复或换 AI 后先读 `temp/` 下最新日期文件。
