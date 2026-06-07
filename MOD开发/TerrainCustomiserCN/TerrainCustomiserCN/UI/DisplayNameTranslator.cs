using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TerrainCustomiserCN.UI
{
	public enum TranslationKind
	{
		Ui,
		Field,
		Type,
		Enum,
		Dynamic
	}

	public readonly struct MissingTranslation
	{
		public MissingTranslation(TranslationKind kind, string key, string source)
		{
			this.Kind = kind;
			this.Key = key;
			this.Source = source ?? string.Empty;
		}

		public TranslationKind Kind { get; }

		public string Key { get; }

		public string Source { get; }
	}

	public static class DisplayNameTranslator
	{
		public static event Action<MissingTranslation> MissingTranslationFound;

		private static readonly Dictionary<string, string> UiNames = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Editor", "编辑器" },
			{ "Inspector", "检查器" },
			{ "Resource", "资源" },
			{ "GodCam Settings", "自由相机设置" },
			{ "Generate", "生成" },
			{ "Save", "保存" },
			{ "Load", "加载" },
			{ "Cam", "相机" },
			{ "Gizmo", "辅助线" },
			{ "Name", "名称" },
			{ "Value", "值" },
			{ "Type", "类型" },
			{ "Buttons", "按钮" },
			{ "Swap", "切换" },
			{ "Create", "创建" },
			{ "Delete", "删除" },
			{ "Remove", "移除" },
			{ "Add", "添加" },
			{ "Change", "更改" },
			{ "None", "无" },
			{ "No custom variant.", "没有自定义变体。" },
			{ "Create Spawner", "创建生成器" },
			{ "Create Grouper", "创建分组器" },
			{ "Duplicate", "复制" },
			{ "Rename", "重命名" },
			{ "Cancel", "取消" },
			{ "Apply", "应用" },
			{ "Select", "选择" },
			{ "Filter", "筛选" }
		};

		private static readonly Dictionary<string, string> FieldNames = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "area", "范围" },
			{ "rayDirectionOffset", "射线偏移" },
			{ "rayLength", "射线长度" },
			{ "rayNearCutoff", "近裁" },
			{ "raycastPosition", "射线位置" },
			{ "rayCastSpawn", "射线生成" },
			{ "nrOfSpawns", "数量" },
			{ "randomSpawns", "随机" },
			{ "minSpawnCount", "最少" },
			{ "chanceToUseSpawner", "概率" },
			{ "spawnChance", "概率" },
			{ "currentSpawns", "当前" },
			{ "props", "物件" },
			{ "syncTransforms", "同步" },
			{ "layerType", "层类型" },
			{ "LayerType", "层类型" },
			{ "modifiers", "修改器" },
			{ "constraints", "条件" },
			{ "postConstraints", "后置条件" },
			{ "postSpawnBehaviors", "生成后行为" },
			{ "height", "高度" },
			{ "timing", "时机" },
			{ "parents", "父级" },
			{ "mat", "材质" },
			{ "bounds", "范围" },
			{ "outerBounds", "外范围" },
			{ "blendSize", "混合" },
			{ "inBounds", "范围内" },
			{ "overrideSun", "覆盖日光" },
			{ "daylLightIntensity", "日间光强" },
			{ "nightLightIntensity", "夜间光强" },
			{ "specialSunColor", "日光色" },
			{ "useCustomSun", "自定义光源" },
			{ "specialLight", "光源" },
			{ "useCustomColorVals", "自定义颜色" },
			{ "specialTopColor", "顶色" },
			{ "specialMidColor", "中色" },
			{ "specialBottomColor", "底色" },
			{ "globalShaderVals", "着色值" },
			{ "overrideFog", "改雾" },
			{ "fogDensity", "雾密度" },
			{ "treePlatformParent", "树平台" },
			{ "localStart", "起点" },
			{ "localEnd", "终点" },
			{ "bannedMaterial", "禁材质" },
			{ "circleSize", "圆尺寸" },
			{ "inverted", "反转" },
			{ "invert", "反转" },
			{ "RaycastDistance", "射线距离" },
			{ "DesiredResult", "期望" },
			{ "maxHeight", "最高" },
			{ "minHeight", "最低" },
			{ "radius", "半径" },
			{ "objects", "对象" },
			{ "minAngle", "最小角" },
			{ "maxAngle", "最大角" },
			{ "perlinSize", "噪声尺寸" },
			{ "perlinOffset", "噪声偏移" },
			{ "minMax", "最小最大" },
			{ "RequiredMaterial", "需材质" },
			{ "minDistance", "最小距离" },
			{ "findAllSpawners", "全生成器" },
			{ "axisMultipliers", "轴倍率" },
			{ "effectedLayers", "影响层" },
			{ "whitelistedTagWords", "白名单词" },
			{ "blacklistedTagWords", "黑名单词" },
			{ "onePerPlayer", "每人一个" },
			{ "destroyAllIfLessThan", "少于销毁" },
			{ "customColor", "自定义颜色" },
			{ "color", "颜色" },
			{ "customIntensity", "自强度" },
			{ "intensity", "强度" },
			{ "offset", "偏移" },
			{ "minEffect", "最小效果" },
			{ "maxEffect", "最大效果" },
			{ "randomPow", "随机幂" },
			{ "minOffset", "最小偏" },
			{ "maxOffset", "最大偏" },
			{ "xMult", "X倍" },
			{ "yMult", "Y倍" },
			{ "zMult", "Z倍" },
			{ "maxScaleMult", "最大缩" },
			{ "minScaleMult", "最小缩" },
			{ "minRotation", "最小旋" },
			{ "maxRotation", "最大旋" },
			{ "snapToIncrement", "吸附步" },
			{ "increment", "步长" },
			{ "replaceThis", "替换项" },
			{ "withThis", "替换为" },
			{ "childName", "子名" },
			{ "edits", "编辑" },
			{ "mats", "材质组" },
			{ "flipNormal", "翻法线" },
			{ "objToSpawn", "生成物" },
			{ "addRotation", "加旋转" },
			{ "eulerAngles", "欧拉角" },
			{ "random", "随机" },
			{ "eulerAnglesRandom", "随机角" },
			{ "blend", "混合" },
			{ "minUpLerp", "最小上插" },
			{ "maxUpLerp", "最大上插" },
			{ "spawnerTransform", "生成器变换" },
			{ "pos", "位置" },
			{ "normal", "法线" },
			{ "rayDir", "射线方向" },
			{ "hit", "命中" },
			{ "placement", "放置" },
			{ "spawnCount", "生成数" },
			{ "_deferredSteps", "延迟步骤" },
			{ "_hm", "高度图" },
			{ "_madeDummyData", "已创建虚拟数据" },
			{ "_propSpawnData", "物件生成数据" },
			{ "_timing", "时机" },
			{ "<spawnedProps>k__BackingField", "已生成物件" },
			{ "<ValidationState>k__BackingField", "验证状态" },
			{ "baseFog", "基础雾效" },
			{ "blockerObjects", "阻挡对象" },
			{ "blurIterations", "模糊迭代次数" },
			{ "blurRadius", "模糊半径" },
			{ "cellSize", "单元格尺寸" },
			{ "center", "中心" },
			{ "clampHeights", "限制高度" },
			{ "Deadzone", "死区" },
			{ "drawRayGizmos", "绘制射线辅助线" },
			{ "drawSamplePoints", "绘制采样点" },
			{ "enterenceObjects", "入口对象" },
			{ "enterences", "入口" },
			{ "heightOffset", "高度偏移" },
			{ "inside", "内部" },
			{ "layerMask", "层遮罩" },
			{ "maxRaycastLength", "最大射线长度" },
			{ "minBadEffects", "最少负面效果" },
			{ "minGoodEffects", "最少正面效果" },
			{ "minMaxSpawn", "最小最大生成数" },
			{ "mushroomEffects", "蘑菇效果" },
			{ "mushroomStamAmt", "蘑菇耐力量" },
			{ "mute", "静音" },
			{ "outVal", "输出值" },
			{ "overallSpawnChance", "总生成概率" },
			{ "perlinAmount", "噪声强度" },
			{ "perlinScale", "噪声缩放" },
			{ "postPerlinAmount", "后置噪声强度" },
			{ "postPerlinScale", "后置噪声缩放" },
			{ "requiredParents", "所需父级" },
			{ "resolution", "分辨率" },
			{ "returnVal", "返回值" },
			{ "scaleMinMax", "最小最大缩放" },
			{ "Segment", "区段" },
			{ "spawnedObjects", "已生成对象" },
			{ "spawnPoints", "生成点" },
			{ "triggerInteraction", "触发器交互" },
			{ "ValidateAfterwards", "随后验证" },
			{ "validationConstraints", "验证条件" }
		};

		private static readonly Dictionary<string, string> TypeNames = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Transform", "变换" },
			{ "PropSpawner", "生成器" },
			{ "PropSpawner_Line", "线性生成器" },
			{ "PropSpawner_Sphere", "球形生成器" },
			{ "PropGrouper", "分组器" },
			{ "RockMaterialSwapper", "岩石材质替换器" },
			{ "SpecialDayZone", "特殊区域" },
			{ "PropSpawnerMod", "生成修改" },
			{ "PropSpawnerConstraint", "生成条件" },
			{ "PropSpawnerConstraintPost", "后置条件" },
			{ "PostSpawnBehavior", "生成后行为" },
			{ "PSB_ChildSpawners", "子生成器" },
			{ "PSC_BannedMaterial", "禁材质" },
			{ "PSC_CircleMask", "圆遮罩" },
			{ "PSC_Embedded", "嵌入检测" },
			{ "PSC_Height", "高度条件" },
			{ "PSC_LineCheck", "射线检测" },
			{ "PSC_NearObject", "近对象" },
			{ "PSC_Normal", "法线条件" },
			{ "PSC_Perlin", "噪声条件" },
			{ "PSC_RequiredMaterial", "需材质" },
			{ "PSC_SameTypeDistance", "同类距离" },
			{ "PSC_SurfaceRestrictions", "表面限制" },
			{ "PSC_VolumeLight", "体积光" },
			{ "PSM_AddPlayerCountBasedDespawner", "人数销毁" },
			{ "PSM_BakedVolumeLightModiferIntensity", "烘焙光强" },
			{ "PSM_BlastVine", "爆炸藤" },
			{ "PSM_ChildSpawners", "子生成器" },
			{ "PSM_LocalOffset", "本地偏移" },
			{ "PSM_NormalOffset", "法线偏移" },
			{ "PSM_PitonNormal", "岩钉法线" },
			{ "PSM_PlacementOffset", "放置偏移" },
			{ "PSM_RandomOffset", "随机偏移" },
			{ "PSM_RandomRotation", "随机旋转" },
			{ "PSM_RandomScale", "随机缩放" },
			{ "PSM_RayDirectionOffset", "射向偏移" },
			{ "PSM_ReplaceMaterial", "替换材质" },
			{ "PSM_ReplaceMaterialWithRayTargetMaterial", "用命中材质替换" },
			{ "PSM_SetForwardRotationToNormal", "前向对法线" },
			{ "PSM_SetMaterial", "设材质" },
			{ "PSM_SetMaterialOnChild", "设子材质" },
			{ "PSM_SetMaterialsOnChild", "设子材质组" },
			{ "PSM_SetRandomMaterial", "随机材质" },
			{ "PSM_SetSpawnerPlayerCountRequirement", "人数需求" },
			{ "PSM_SetUpRotationToNormal", "上向对法线" },
			{ "PSM_SingleItemSpawner", "单物品生成" },
			{ "PSM_SpecificRotation", "指定旋转" },
			{ "PSM_UpLerp", "上向插值" },
			{ "Campfire_Set_Segment", "营火区段设置" },
			{ "DecorSpawner", "装饰生成器" },
			{ "DesertRockSpawner", "沙漠岩石生成器" },
			{ "MushroomManager", "蘑菇管理器" },
			{ "PropDeleter", "物件删除器" },
			{ "PSCP_ConnectTreePlatforms", "连接树平台" },
			{ "PSCP_Custom", "自定义后置条件" },
			{ "PSCP_LineCheck", "后置射线检测" },
			{ "SwampMeshGen", "沼泽网格生成器" }
		};

		private static readonly Dictionary<string, string> EnumNames = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "HelperFunctions+LayerType.AllPhysical", "全实体" },
			{ "HelperFunctions+LayerType.TerrainMap", "地形图" },
			{ "HelperFunctions+LayerType.Terrain", "地形" },
			{ "HelperFunctions+LayerType.Map", "地图" },
			{ "HelperFunctions+LayerType.Default", "默认" },
			{ "HelperFunctions+LayerType.AllPhysicalExceptCharacter", "全实体非角色" },
			{ "HelperFunctions+LayerType.CharacterAndDefault", "角色+默认" },
			{ "HelperFunctions+LayerType.AllPhysicalExceptDefault", "全实体非默认" },
			{ "PropGrouper+PropGrouperTiming.Early", "早" },
			{ "PropGrouper+PropGrouperTiming.Late", "晚" },
			{ "TerrainCustomiserCN.UI.WidgetFactory+WidgetMode.Default", "默认" },
			{ "TerrainCustomiserCN.UI.WidgetFactory+WidgetMode.Disabled", "禁用" },
			{ "TerrainCustomiserCN.UI.WidgetFactory+WidgetMode.Hidden", "隐藏" },
			{ "TerrainCustomiserCN.UI.WidgetFactory+FieldLayout.Inline", "行内" },
			{ "TerrainCustomiserCN.UI.WidgetFactory+FieldLayout.Full", "完整" },
			{ "DeferredStepTiming.None", "无" },
			{ "DeferredStepTiming.AfterCurrentStep", "当前步骤后" },
			{ "DeferredStepTiming.AfterCurrentGroupTiming", "当前分组时机后" },
			{ "DeferredStepTiming.AfterDone", "完成后" },
			{ "Peak.ProcGen.ValidationState.Unknown", "未知" },
			{ "Peak.ProcGen.ValidationState.Passed", "通过" },
			{ "Peak.ProcGen.ValidationState.Failed", "失败" },
			{ "Segment.Alpine", "雪山" },
			{ "Segment.Beach", "海滩" },
			{ "Segment.Caldera", "火山" },
			{ "Segment.Peak", "顶峰" },
			{ "Segment.TheKiln", "熔炉" },
			{ "Segment.Tropics", "雨林" },
			{ "UnityEngine.QueryTriggerInteraction.UseGlobal", "使用全局设置" },
			{ "UnityEngine.QueryTriggerInteraction.Ignore", "忽略触发器" },
			{ "UnityEngine.QueryTriggerInteraction.Collide", "碰撞触发器" }
		};

		private static readonly Dictionary<string, string> DynamicNames = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Alpine", "雪山" },
			{ "Beach", "海滩" },
			{ "Tropics", "雨林" },
			{ "Volcano", "火山" },
			{ "Mesa", "方山" },
			{ "Roots", "森蕈" },
			{ "Caldera", "火山" },
			{ "Peak", "顶峰" },
			{ "The Kiln", "熔炉" },
			{ "CULLER", "剔除器" },
			{ "Edges", "边缘" },
			{ "Eggs", "蛋" },
			{ "Mid", "中" },
			{ "Middle", "中间" },
			{ "Airplane Food", "飞机餐" },
			{ "AloeVera", "芦荟" },
			{ "AncientIdol", "古老玩偶" },
			{ "Antidote", "解毒剂" },
			{ "Anti-Rope Spool", "反重绳索" },
			{ "Apple Berry Green", "绿脆莓" },
			{ "Apple Berry Red", "红脆莓" },
			{ "Apple Berry Yellow", "黄脆莓" },
			{ "Backpack", "背包" },
			{ "Balloon", "气球" },
			{ "BalloonBunch", "一束气球" },
			{ "Bandages", "绷带" },
			{ "Basketball", "篮球" },
			{ "Beehive", "蜂巢" },
			{ "Berrynana Blue", "蓝莓蕉" },
			{ "Berrynana Brown", "棕莓蕉" },
			{ "Berrynana Peel Blue Variant", "莓蕉皮" },
			{ "Berrynana Peel Brown Variant", "莓蕉皮" },
			{ "Berrynana Peel Pink Variant", "莓蕉皮" },
			{ "Berrynana Peel Yellow", "莓蕉皮" },
			{ "Berrynana Pink", "粉莓蕉" },
			{ "Berrynana Yellow", "黄莓蕉" },
			{ "BingBong", "宾邦" },
			{ "BingBong_Prop Variant", "宾邦" },
			{ "Binoculars", "望远镜" },
			{ "Binoculars_Prop", "望远镜" },
			{ "BookOfBones", "骸骨之书" },
			{ "BounceShroom", "弹力菇" },
			{ "Bugfix", "蜱虫" },
			{ "Bugle", "喇叭" },
			{ "Bugle_Magic", "友谊喇叭" },
			{ "Bugle_Prop Variant", "喇叭" },
			{ "Bugle_Scoutmaster Variant", "童军领队的喇叭" },
			{ "C_Bishop B", "主教" },
			{ "C_Bishop W", "主教" },
			{ "C_Bishop_f", "青荔莓" },
			{ "C_Bishop_f Variant", "主教" },
			{ "C_Bishop_m", "青荔莓" },
			{ "C_Bishop_m Variant", "青荔莓" },
			{ "C_King", "青荔莓" },
			{ "C_King B", "国王" },
			{ "C_King Variant", "国王" },
			{ "C_King W", "国王" },
			{ "C_Knight", "青荔莓" },
			{ "C_Knight B", "骑士" },
			{ "C_Knight Variant", "骑士" },
			{ "C_Knight W", "骑士" },
			{ "C_Pawn B", "兵卒" },
			{ "C_Pawn W", "兵卒" },
			{ "C_Pawn_f", "青荔莓" },
			{ "C_Pawn_f Variant", "青荔莓" },
			{ "C_Pawn_m", "青荔莓" },
			{ "C_Pawn_m Variant", "兵卒" },
			{ "C_Queen", "青荔莓" },
			{ "C_Queen B", "皇后" },
			{ "C_Queen Variant", "皇后" },
			{ "C_Queen W", "皇后" },
			{ "C_Rook B", "战车" },
			{ "C_Rook W", "战车" },
			{ "C_Rook_f", "青荔莓" },
			{ "C_Rook_f Variant", "青荔莓" },
			{ "C_Rook_m", "青荔莓" },
			{ "C_Rook_m Variant", "战车" },
			{ "CactusBall", "仙人球" },
			{ "ChainShootable", "锁链" },
			{ "ChainShooter", "锁链发射器" },
			{ "ClimbingSpike", "岩钉" },
			{ "ClimbingSpikeHammered", "岩钉" },
			{ "CloudFungus", "云雾菇" },
			{ "Clusterberry Black", "黑葚莓" },
			{ "Clusterberry Red", "红葚莓" },
			{ "Clusterberry Yellow", "黄葚莓" },
			{ "Clusterberry_UNUSED", "青葚莓" },
			{ "Compass", "罗盘" },
			{ "Cure-All", "万灵药" },
			{ "Cursed Skull", "诅咒头骨" },
			{ "Dynamite", "炸药" },
			{ "Egg", "煎蛋" },
			{ "EggTurkey", "“鸟”" },
			{ "Energy Drink", "能量饮料" },
			{ "FireWood", "棍子" },
			{ "FirstAidKit", "急救箱" },
			{ "Flag_Plantable_Checkpoint", "检查点旗标" },
			{ "Flare", "照明棒" },
			{ "FortifiedMilk", "奶白金" },
			{ "FortifiedMilk_TEMP", "青荔莓" },
			{ "Frisbee", "飞盘" },
			{ "Glizzy", "热狗肠" },
			{ "Granola Bar", "燕麦棒" },
			{ "Guidebook", "旅行指南" },
			{ "GuidebookPage", "撕下的书页" },
			{ "GuidebookPage_0_Intro", "撕下的书页" },
			{ "GuidebookPage_1_Mushrooms", "撕下的书页" },
			{ "GuidebookPage_2_Campfire", "撕下的书页" },
			{ "GuidebookPage_3_Revival", "撕下的书页" },
			{ "GuidebookPage_4_BodyHeat Variant", "撕下的书页" },
			{ "GuidebookPage_5_Sleepy Variant", "撕下的书页" },
			{ "GuidebookPage_6_Awake Variant", "撕下的书页" },
			{ "GuidebookPage_7_Crashout Variant", "撕下的书页" },
			{ "GuidebookPage_8_FirstTeams", "撕下的书页" },
			{ "GuidebookPageScroll Variant", "卷轴" },
			{ "HealingDart Variant", "吹箭筒" },
			{ "HealingPuffShroom", "灵药菇" },
			{ "Heat Pack", "暖宝宝" },
			{ "Item_Coconut", "椰子" },
			{ "Item_Coconut_half", "半边椰子" },
			{ "Item_Honeycomb", "蜂巢蜜" },
			{ "Kingberry Green", "青荔莓" },
			{ "Kingberry Purple", "紫荔莓" },
			{ "Kingberry Yellow", "黄荔莓" },
			{ "Lantern", "提灯" },
			{ "Lantern_Faerie", "仙子提灯" },
			{ "Lollipop", "大棒棒糖" },
			{ "Lollipop_Prop", "大棒棒糖" },
			{ "MagicBean", "魔豆" },
			{ "Mandrake", "曼德拉草" },
			{ "Mandrake_Hidden", "药用根茎" },
			{ "Marshmallow", "棉花糖" },
			{ "MedicinalRoot", "药用根茎" },
			{ "Megaphone", "扩音器" },
			{ "Mushroom Chubby", "梨鲍菇" },
			{ "Mushroom Cluster", "银针菇" },
			{ "Mushroom Cluster Poison", "银针菇" },
			{ "Mushroom Glow", "诡异菇" },
			{ "Mushroom Lace", "喇叭菇" },
			{ "Mushroom Lace Poison", "喇叭菇" },
			{ "Mushroom Normie", "馒头菇" },
			{ "Mushroom Normie Poison", "馒头菇" },
			{ "Napberry", "晚安莓" },
			{ "NestEgg", "好大蛋" },
			{ "PandorasBox", "潘多拉餐盒" },
			{ "Parasol", "太阳伞" },
			{ "Parasol_Roots Variant", "太阳伞" },
			{ "Passport", "护照" },
			{ "Pepper Berry", "火烧莓" },
			{ "Pirate Compass", "海盗罗盘" },
			{ "PortableStovetop_Placed", "便携火炉" },
			{ "PortableStovetopItem", "便携火炉" },
			{ "Prickleberry_Gold", "金刺莓" },
			{ "Prickleberry_Red", "红刺莓" },
			{ "RemoteRopeSegment Variant", "绳索" },
			{ "RemoteRopeSegmentAntiRope", "反重绳索" },
			{ "RescueHook", "救援抓钩" },
			{ "RescueHook_Infinite", "救援抓钩" },
			{ "RopeSegment", "绳索" },
			{ "RopeSegmentInverted", "反重绳索" },
			{ "RopeShooter", "绳索炮" },
			{ "RopeShooterAnti", "反重绳索炮" },
			{ "RopeSpool", "绳索" },
			{ "Scorpion", "蝎子" },
			{ "ScoutCannonItem", "童子军大炮" },
			{ "ScoutCookies", "童军饼干" },
			{ "ScoutCookies_Vanilla", "童军饼干" },
			{ "ScoutEffigy", "童军雕像" },
			{ "ShelfShroom", "踏板菇" },
			{ "Shell Big", "海螺" },
			{ "Shroomberry_Blue", "蓝菇莓" },
			{ "Shroomberry_Green", "绿菇莓" },
			{ "Shroomberry_Purple", "紫菇莓" },
			{ "Shroomberry_Red", "红菇莓" },
			{ "Shroomberry_Yellow", "黄菇莓" },
			{ "Snowball", "雪球" },
			{ "Sports Drink", "运动饮料" },
			{ "Stone", "石头" },
			{ "Strange Gem", "奇怪水晶" },
			{ "Sunscreen", "防晒喷雾" },
			{ "Torch", "火炬" },
			{ "TrailMix", "混合坚果" },
			{ "Warp Compass", "曲迁罗盘" },
			{ "Winterberry Orange", "橙雪莓" },
			{ "Winterberry Yellow", "黄雪莓" },
			{ "Biome_1", "生物群系 1" },
			{ "Biome_2", "生物群系 2" },
			{ "Biome_3", "生物群系 3" },
			{ "Biome_4", "生物群系 4" },
			{ "Beach_Segment", "海滩区段" },
			{ "Caldera_Segment", "火山口区段" },
			{ "Desert_Segment", "沙漠区段" },
			{ "Jungle_Segment", "丛林区段" },
			{ "Roots Segment", "根系区段" },
			{ "Snow_Segment", "雪地区段" },
			{ "Volcano_Segment", "火山区段" },
			{ "Custom", "自定义" },
			{ "Default", "默认" },
			{ "Spiky", "尖刺" },
			{ "Spires", "尖塔" },
			{ "NoVariant", "无变体" },
			{ "BlackSand", "黑沙" },
			{ "BlueBeach", "蓝色海滩" },
			{ "Bombs", "炸弹" },
			{ "CactusForest", "仙人掌森林" },
			{ "CacusHell", "仙人掌地狱" },
			{ "DynamiteHell", "炸药地狱" },
			{ "GeyserHell", "间歇泉地狱" },
			{ "Ivy", "藤蔓" },
			{ "JellyHell", "水母地狱" },
			{ "Lava", "熔岩" },
			{ "Pillars", "石柱" },
			{ "RedBeach", "红色海滩" },
			{ "ScorpionsHell", "蝎子地狱" },
			{ "SkyJungle", "天空丛林" },
			{ "SnakeBeach", "蛇海滩" },
			{ "Thorny", "荆棘" },
			{ "TornadoHell", "龙卷风地狱" },
			{ "TumblerHell", "翻滚草地狱" },
			{ "- Bomb Beetle Variant", "炸弹甲虫变体" },
			{ "- Cave Mania Variant", "洞穴狂热变体" },
			{ "- Deep Water variant", "深水变体" },
			{ "- Redwood Clearcut Variant", "红木清伐变体" },
			{ "- Redwoods Default Variant", "红木默认变体" },
			{ "- redwoods deep woods Variant", "红木深林变体" }
		};

		private static readonly Dictionary<string, string> DynamicNameTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Aid", "急救" },
			{ "Airplane", "飞机" },
			{ "Aleo", "芦荟" },
			{ "All", "全部" },
			{ "Aloe", "芦荟" },
			{ "Anchor", "锚点" },
			{ "Ancient", "远古" },
			{ "Ant", "蚂蚁" },
			{ "Anti", "反向" },
			{ "Antidote", "解毒剂" },
			{ "Antigrav", "反重力" },
			{ "Antlion", "蚁狮" },
			{ "Apple", "苹果" },
			{ "Awake", "清醒" },
			{ "Axe", "斧" },
			{ "B", "黑" },
			{ "Backpack", "背包" },
			{ "Badge", "徽章" },
			{ "Badges", "徽章" },
			{ "Ball", "球" },
			{ "Balloon", "气球" },
			{ "Balls", "球" },
			{ "Banana", "香蕉" },
			{ "Bandages", "绷带" },
			{ "Bar", "棒" },
			{ "Basalt", "玄武岩" },
			{ "Bassalt", "玄武岩" },
			{ "Base", "基础" },
			{ "Basic", "基础" },
			{ "Basketball", "篮球" },
			{ "Beach", "海滩" },
			{ "Bean", "豆" },
			{ "Bee", "蜜蜂" },
			{ "Beehive", "蜂巢" },
			{ "Beetle", "甲虫" },
			{ "Beetles", "甲虫" },
			{ "Behive", "蜂巢" },
			{ "Berries", "浆果" },
			{ "Berry", "浆果" },
			{ "berrybush", "浆果灌木" },
			{ "Berrynana", "莓蕉" },
			{ "Big", "大型" },
			{ "Bing", "宾" },
			{ "Binoc", "双筒镜" },
			{ "Binoculars", "双筒望远镜" },
			{ "Bishop", "主教" },
			{ "Black", "黑色" },
			{ "Bl", "黑" },
			{ "Blind", "致盲" },
			{ "Blue", "蓝色" },
			{ "Body", "体温" },
			{ "Bones", "骨头" },
			{ "Bong", "邦" },
			{ "Book", "书" },
			{ "Bot", "机器人" },
			{ "Bounce", "弹跳" },
			{ "Box", "盒子" },
			{ "Bridge", "桥" },
			{ "Bridges", "桥" },
			{ "Brown", "棕色" },
			{ "Bugfix", "修复" },
			{ "Bugle", "号角" },
			{ "Bunch", "束" },
			{ "Bush", "灌木" },
			{ "Bushes", "灌木" },
			{ "C", "棋子" },
			{ "Cactus", "仙人掌" },
			{ "Campfire", "营火" },
			{ "Cannon", "炮" },
			{ "Canyon", "峡谷" },
			{ "Cave", "洞穴" },
			{ "Caves", "洞穴" },
			{ "Cell", "单元格" },
			{ "Chain", "链条" },
			{ "Chalk", "粉笔" },
			{ "Character", "角色" },
			{ "Cheat", "作弊" },
			{ "Checkpoint", "检查点" },
			{ "Chest", "箱子" },
			{ "Chubby", "胖" },
			{ "Cliff", "峭壁" },
			{ "Climbing", "攀爬" },
			{ "Cloud", "云" },
			{ "Cluster", "簇" },
			{ "Clusterberry", "簇生莓" },
			{ "Clusters", "簇" },
			{ "Coconut", "椰子" },
			{ "Cold", "寒冷" },
			{ "Collection", "集合" },
			{ "Column", "柱" },
			{ "Compass", "指南针" },
			{ "Conclusion", "结语" },
			{ "Connecting", "连接" },
			{ "Console", "控制台" },
			{ "Cookies", "饼干" },
			{ "Crashout", "崩溃" },
			{ "Crook", "弯曲" },
			{ "Cure", "治疗" },
			{ "Cursed", "诅咒" },
			{ "Custom", "自定义" },
			{ "Dart", "飞镖" },
			{ "Dead", "枯死" },
			{ "Dedication", "题献" },
			{ "Deep", "深" },
			{ "Default", "默认" },
			{ "Desert", "沙漠" },
			{ "Destroyer", "销毁器" },
			{ "Dolo", "多洛" },
			{ "Dome", "圆顶" },
			{ "Driftwood", "浮木" },
			{ "Drink", "饮料" },
			{ "DROPDOWN", "下拉框" },
			{ "Dropper", "投放器" },
			{ "Dry", "干枯" },
			{ "Dynamic", "动态" },
			{ "Dynamite", "炸药" },
			{ "E", "东" },
			{ "Edge", "边缘" },
			{ "Effigy", "雕像" },
			{ "Egg", "蛋" },
			{ "Elixir", "药剂" },
			{ "End", "末端" },
			{ "Energy", "能量" },
			{ "ENUM", "枚举" },
			{ "Epic", "史诗" },
			{ "Erik", "埃里克" },
			{ "Eruption", "喷发" },
			{ "Evil", "邪恶" },
			{ "Explo", "爆炸" },
			{ "Explosion", "爆炸" },
			{ "f", "女" },
			{ "Fae", "仙灵" },
			{ "Faerie", "仙灵" },
			{ "Fake", "假" },
			{ "Fern", "蕨类" },
			{ "Ferns", "蕨类" },
			{ "Final", "最终" },
			{ "Fire", "火" },
			{ "First", "初级" },
			{ "Fixed", "固定" },
			{ "Flag", "旗帜" },
			{ "Flare", "信号弹" },
			{ "Flash", "闪光" },
			{ "Flat", "平坦" },
			{ "FLOAT", "浮点数" },
			{ "flower", "花" },
			{ "Food", "食物" },
			{ "Foot", "脚部" },
			{ "For", "用于" },
			{ "Forest", "森林" },
			{ "Fortified", "强化" },
			{ "Foundation", "基础" },
			{ "Friend", "队友" },
			{ "Frisbee", "飞盘" },
			{ "Fungus", "真菌" },
			{ "Funky", "奇异" },
			{ "Gem", "宝石" },
			{ "Geyser", "间歇泉" },
			{ "Geysers", "间歇泉" },
			{ "Giant", "巨型" },
			{ "Glizzy", "热狗" },
			{ "Glow", "发光" },
			{ "Gold", "金色" },
			{ "Granola", "格兰诺拉" },
			{ "Grass", "草" },
			{ "Green", "绿色" },
			{ "Guidebook", "指南书" },
			{ "Half", "半个" },
			{ "Hammered", "已敲入" },
			{ "Hand", "手" },
			{ "Hang", "悬挂" },
			{ "Hanging", "悬挂" },
			{ "Healing", "治疗" },
			{ "Heat", "热量" },
			{ "Helicopter", "直升机" },
			{ "Helping", "帮助" },
			{ "Hidden", "隐藏" },
			{ "High", "高" },
			{ "Hold", "抓点" },
			{ "Holow", "中空" },
			{ "Honeycomb", "蜂巢蜜" },
			{ "Hook", "钩" },
			{ "Horsetail", "木贼草" },
			{ "Hot", "热" },
			{ "Ice", "冰" },
			{ "Icicle", "冰锥" },
			{ "Identity", "标识" },
			{ "Idol", "神像" },
			{ "Impact", "冲击" },
			{ "Infinite", "无限" },
			{ "Injury", "伤害" },
			{ "Inner", "内部" },
			{ "INPUT", "输入" },
			{ "Inside", "内部" },
			{ "Internal", "内部" },
			{ "Intro", "介绍" },
			{ "Inverted", "反转" },
			{ "Item", "物品" },
			{ "Ivy", "藤蔓" },
			{ "Jellies", "水母" },
			{ "Jelly", "水母" },
			{ "Jellyfish", "水母" },
			{ "Jungle", "丛林" },
			{ "Kiln", "窑炉" },
			{ "King", "国王" },
			{ "Kingberry", "王莓" },
			{ "Kit", "套件" },
			{ "Knight", "骑士" },
			{ "Knockback", "击退" },
			{ "L", "左" },
			{ "Lace", "蕾丝" },
			{ "Lantern", "灯笼" },
			{ "Lava", "熔岩" },
			{ "Light", "光源" },
			{ "Lights", "光源" },
			{ "Lil", "小" },
			{ "Lion", "狮" },
			{ "List", "列表" },
			{ "Loading", "加载" },
			{ "Lollipop", "棒棒糖" },
			{ "Long", "长" },
			{ "Looker", "观察者" },
			{ "Low", "低" },
			{ "Luggage", "行李" },
			{ "m", "男" },
			{ "Magic", "魔法" },
			{ "Magma", "岩浆" },
			{ "Manager", "管理器" },
			{ "Mandrake", "曼德拉草" },
			{ "Marshmallow", "棉花糖" },
			{ "Massive", "巨大" },
			{ "Medicinal", "药用" },
			{ "Medium", "中型" },
			{ "Mega", "巨型" },
			{ "Megaphone", "扩音器" },
			{ "Milk", "牛奶" },
			{ "mineshaft", "矿井" },
			{ "Mineshafts", "矿井" },
			{ "Mini", "迷你" },
			{ "Mirage", "海市蜃楼" },
			{ "Mirrage", "海市蜃楼" },
			{ "Mix", "混合" },
			{ "Monstera", "龟背竹" },
			{ "Monsteras", "龟背竹" },
			{ "Moss", "苔藓" },
			{ "Movement", "移动" },
			{ "Mush", "蘑菇" },
			{ "Mushroom", "蘑菇" },
			{ "Mushrooms", "蘑菇" },
			{ "Nap", "睡眠" },
			{ "Napberry", "睡眠莓" },
			{ "Nest", "巢" },
			{ "Nice", "良性" },
			{ "No", "无" },
			{ "Normie", "普通" },
			{ "Oasis", "绿洲" },
			{ "Of", "的" },
			{ "Old", "旧" },
			{ "Only", "仅" },
			{ "Onsen", "温泉" },
			{ "Option", "选项" },
			{ "Orange", "橙色" },
			{ "Orb", "球体" },
			{ "Out", "外部" },
			{ "Outside", "外部" },
			{ "Overhang", "悬垂" },
			{ "Pack", "包" },
			{ "Page", "页面" },
			{ "Palm", "棕榈" },
			{ "Palms", "棕榈" },
			{ "Pandora", "潘多拉" },
			{ "Pandoras", "潘多拉" },
			{ "Parasol", "遮阳伞" },
			{ "Passed", "已通过" },
			{ "Passport", "护照" },
			{ "patch", "片" },
			{ "Pawn", "兵" },
			{ "Peel", "果皮" },
			{ "Pepper", "辣椒" },
			{ "Pick", "镐" },
			{ "Pile", "堆" },
			{ "Pillar", "石柱" },
			{ "Pine", "松树" },
			{ "Ping", "标记" },
			{ "Pink", "粉色" },
			{ "Pip", "状态点" },
			{ "Pirate", "海盗" },
			{ "Piton", "岩钉" },
			{ "Placed", "已放置" },
			{ "Plane", "飞机" },
			{ "Plant", "植物" },
			{ "Plantable", "可插旗" },
			{ "Planted", "已插旗" },
			{ "Plartform", "平台" },
			{ "Plat", "平台" },
			{ "Plateau", "高原" },
			{ "Platform", "平台" },
			{ "Platforms", "平台" },
			{ "Platteau", "高原" },
			{ "Player", "玩家" },
			{ "Point", "点" },
			{ "Poison", "毒" },
			{ "Pop", "爆开" },
			{ "Pops", "弹出物" },
			{ "Portable", "便携" },
			{ "Pre", "预置" },
			{ "Preface", "前言" },
			{ "Preview", "预览" },
			{ "Prickleberry", "刺莓" },
			{ "Prop", "物件" },
			{ "Props", "物件" },
			{ "Proxy", "代理" },
			{ "Puff", "喷雾" },
			{ "Purple", "紫色" },
			{ "Queen", "王后" },
			{ "R", "右" },
			{ "Red", "红色" },
			{ "Redwood", "红木" },
			{ "redwoods", "红木" },
			{ "Remote", "远程" },
			{ "Rescue", "救援" },
			{ "Respawn", "重生" },
			{ "Revival", "复活" },
			{ "Revived", "已复活" },
			{ "Ring", "环" },
			{ "Rings", "环" },
			{ "River", "河流" },
			{ "Rivers", "河流" },
			{ "Rock", "岩石" },
			{ "Rocks", "岩石" },
			{ "Rook", "车" },
			{ "Root", "根" },
			{ "Roots", "根系" },
			{ "Rope", "绳索" },
			{ "Ropes", "绳索" },
			{ "Round", "圆形" },
			{ "Rule", "规则" },
			{ "Safe", "安全" },
			{ "Scorpion", "蝎子" },
			{ "Scorpions", "蝎子" },
			{ "Scorps", "蝎子" },
			{ "Scout", "童子军" },
			{ "Scoutmaster", "童子军队长" },
			{ "Scream", "尖叫" },
			{ "Screen", "屏幕" },
			{ "Scroll", "卷轴" },
			{ "Sea", "海" },
			{ "Segment", "区段" },
			{ "Settings", "设置" },
			{ "SFX", "音效" },
			{ "Shaky", "摇晃" },
			{ "Shape", "形状" },
			{ "Sharp", "尖锐" },
			{ "Shelf", "架状" },
			{ "Shell", "贝壳" },
			{ "Shells", "贝壳" },
			{ "Shitty", "劣质" },
			{ "Shootable", "可射击" },
			{ "Shooter", "发射器" },
			{ "Short", "矮" },
			{ "Shroom", "蘑菇" },
			{ "Shroomberry", "蘑菇莓" },
			{ "Shrooms", "蘑菇" },
			{ "Shrub", "灌木" },
			{ "Side", "侧面" },
			{ "Simple", "简单" },
			{ "Single", "单个" },
			{ "Skull", "头骨" },
			{ "Sleepy", "困倦" },
			{ "Slippery", "湿滑" },
			{ "Small", "小型" },
			{ "Smoke", "烟雾" },
			{ "Snake", "蛇" },
			{ "Snow", "雪" },
			{ "Snowball", "雪球" },
			{ "Some", "部分" },
			{ "Sound", "声音" },
			{ "Spawn", "生成" },
			{ "Spawner", "生成器" },
			{ "Spawners", "生成器" },
			{ "Spawns", "生成点" },
			{ "Spider", "蜘蛛" },
			{ "Spiders", "蜘蛛" },
			{ "Spike", "尖刺" },
			{ "Spire", "尖塔" },
			{ "Spires", "尖塔" },
			{ "Spline", "样条" },
			{ "Spool", "线轴" },
			{ "Spore", "孢子" },
			{ "spores", "孢子" },
			{ "Sports", "运动" },
			{ "Stamina", "耐力" },
			{ "Start", "起点" },
			{ "Step", "步骤" },
			{ "Stone", "石头" },
			{ "Stovetop", "炉具" },
			{ "Strange", "奇异" },
			{ "Stumps", "树桩" },
			{ "Sunscreen", "防晒霜" },
			{ "Swarm", "蜂群" },
			{ "Tall", "高" },
			{ "TCCN", "TCCN" },
			{ "Teams", "队伍" },
			{ "TEMP", "临时" },
			{ "Thick", "粗" },
			{ "Thin", "细" },
			{ "Thorns", "荆棘" },
			{ "Timple", "神庙" },
			{ "Tiny", "微型" },
			{ "Title", "标题" },
			{ "Toggle", "切换" },
			{ "Torch", "火把" },
			{ "Tower", "塔" },
			{ "Tracking", "追踪" },
			{ "Trail", "混合包" },
			{ "Transform", "变换" },
			{ "Tree", "树" },
			{ "Trees", "树" },
			{ "Trunk", "树干" },
			{ "Tumble", "翻滚草" },
			{ "Tumblers", "翻滚草" },
			{ "tumbleweed", "翻滚草" },
			{ "Tunnels", "隧道" },
			{ "Turkey", "火鸡" },
			{ "UI", "界面" },
			{ "UNUSED", "未使用" },
			{ "Urch", "海胆" },
			{ "Urchins", "海胆" },
			{ "Use", "使用" },
			{ "Vanilla", "原味" },
			{ "Variant", "变体" },
			{ "Vera", "芦荟" },
			{ "VFX", "特效" },
			{ "Vine", "藤蔓" },
			{ "Vines", "藤蔓" },
			{ "W", "白" },
			{ "Wall", "墙面" },
			{ "Warp", "传送" },
			{ "Water", "水" },
			{ "Waterfall", "瀑布" },
			{ "Waterfalls", "瀑布" },
			{ "Web", "蛛网" },
			{ "Weed", "杂草" },
			{ "Wildflower", "野花" },
			{ "Willow", "柳树" },
			{ "Windows", "窗口" },
			{ "Winterberry", "冬莓" },
			{ "With", "带" },
			{ "Wonderberry", "奇异莓" },
			{ "Wood", "木头" },
			{ "Yellow", "黄色" },
			{ "Zombie", "僵尸" },
			{ "Atlas", "图集" },
			{ "Binggrae", "宾格瑞" },
			{ "Blink", "眨眼" },
			{ "Blowgun", "吹箭筒" },
			{ "Board", "面板" },
			{ "Boarding", "登机" },
			{ "Bold", "粗体" },
			{ "Check", "检查" },
			{ "Clouds", "云" },
			{ "Columns", "柱" },
			{ "Costume", "服装" },
			{ "CRAZK", "CRAZK" },
			{ "Crown", "王冠" },
			{ "Dark", "暗" },
			{ "Daruma", "达摩" },
			{ "Dear", "Dear" },
			{ "Decal", "贴花" },
			{ "Departure", "出发" },
			{ "Depth", "深度" },
			{ "Drop", "投放" },
			{ "Droplet", "水滴" },
			{ "Drowsy", "困倦" },
			{ "Extra", "特粗" },
			{ "Eye", "眼睛" },
			{ "Fade", "淡出" },
			{ "Feature", "特性" },
			{ "Fog", "雾" },
			{ "Font", "字体" },
			{ "Glass", "玻璃" },
			{ "Gothic", "哥特体" },
			{ "Graphs", "图" },
			{ "Gui", "界面" },
			{ "Heli", "直升机" },
			{ "Im", "Im" },
			{ "In", "内" },
			{ "Info", "信息" },
			{ "Jug", "壶" },
			{ "Kiosk", "自助终端" },
			{ "Liberation", "Liberation" },
			{ "Lit", "受光" },
			{ "Mask", "遮罩" },
			{ "Material", "材质" },
			{ "Member", "成员" },
			{ "Mesh", "网格" },
			{ "Mirror", "镜子" },
			{ "Montserrat", "蒙特塞拉特" },
			{ "Mountains", "群山" },
			{ "Nick", "昵称" },
			{ "Now", "当前" },
			{ "One", "一" },
			{ "Outline", "描边" },
			{ "Pangolin", "穿山甲" },
			{ "Particle", "粒子" },
			{ "Particles", "粒子" },
			{ "Photobooth", "照相亭" },
			{ "Players", "玩家" },
			{ "Post", "后处理" },
			{ "Pro", "Pro" },
			{ "Rain", "雨" },
			{ "Regular", "常规" },
			{ "Rotors", "旋翼" },
			{ "Sand", "沙" },
			{ "Sans", "无衬线" },
			{ "SC", "简中" },
			{ "Scan", "扫描" },
			{ "SDF", "SDF" },
			{ "Semi", "半" },
			{ "Set", "套装" },
			{ "Shader", "着色器" },
			{ "Shadow", "阴影" },
			{ "Sign", "标志" },
			{ "Skybox", "天空盒" },
			{ "Snowflake", "雪花" },
			{ "Soft", "柔和" },
			{ "softy", "柔和" },
			{ "Space", "太空" },
			{ "Sprite", "精灵" },
			{ "Sprites", "精灵" },
			{ "Steam", "蒸汽" },
			{ "Sun", "太阳" },
			{ "Tetsubin", "铁瓶" },
			{ "Text", "文本" },
			{ "TMP", "TMP" },
			{ "Tomb", "墓穴" },
			{ "Unlit", "无光照" },
			{ "Visor", "面罩" },
			{ "Volume", "体积" },
			{ "White", "白色" },
			{ "Wildflowers", "野花" }
		};

		private static readonly Dictionary<string, string> GeneratedDynamicNames = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly HashSet<string> MissingKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly Regex EnglishRegex = new Regex("[A-Za-z]", RegexOptions.Compiled);

		private static readonly Regex DynamicNameBoundaryRegex = new Regex("(?<=[a-z])(?=[A-Z])|(?<=[A-Za-z])(?=\\d)|(?<=\\d)(?=[A-Za-z])", RegexOptions.Compiled);

		private static readonly Regex DynamicNameTokenRegex = new Regex("[A-Za-z]+|\\d+", RegexOptions.Compiled);

		public static string Ui(string value, string source = null)
		{
			return Translate(TranslationKind.Ui, value, UiNames, source);
		}

		public static string Field(string value, string source = null)
		{
			return Translate(TranslationKind.Field, value, FieldNames, source);
		}

		public static string TypeName(Type type, string source = null)
		{
			if (type == null)
			{
				return string.Empty;
			}
			return TypeName(type.Name, source ?? type.FullName);
		}

		public static string TypeName(string value, string source = null)
		{
			return Translate(TranslationKind.Type, value, TypeNames, source);
		}

		public static string EnumName(Type enumType, string value, string source = null)
		{
			if (enumType == null)
			{
				return Translate(TranslationKind.Enum, value, EnumNames, source);
			}
			string key = enumType.FullName + "." + value;
			if (EnumNames.TryGetValue(key, out string translated))
			{
				return translated;
			}
			ReportMissing(TranslationKind.Enum, key, source ?? enumType.FullName);
			return value;
		}

		public static string Dynamic(string value, string source = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}
			if (DynamicNames.TryGetValue(value, out string translated))
			{
				return translated;
			}
			if (GeneratedDynamicNames.TryGetValue(value, out translated))
			{
				return translated;
			}
			translated = TryTranslateDynamicName(value);
			if (translated != null)
			{
				GeneratedDynamicNames[value] = translated;
				return translated;
			}
			ReportMissing(TranslationKind.Dynamic, value, source);
			return value;
		}

		public static string Context(string value, string source = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}
			if (UiNames.TryGetValue(value, out string uiName))
			{
				return uiName;
			}
			if (TypeNames.TryGetValue(value, out string typeName))
			{
				return typeName;
			}
			ReportMissing(TranslationKind.Ui, value, source);
			return value;
		}

		public static bool HasTranslation(TranslationKind kind, string value, Type enumType = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return true;
			}
			switch (kind)
			{
			case TranslationKind.Ui:
				return UiNames.ContainsKey(value);
			case TranslationKind.Field:
				return FieldNames.ContainsKey(value);
			case TranslationKind.Type:
				return TypeNames.ContainsKey(value);
			case TranslationKind.Enum:
				return (enumType != null && EnumNames.ContainsKey(enumType.FullName + "." + value)) || EnumNames.ContainsKey(value);
			case TranslationKind.Dynamic:
				return DynamicNames.ContainsKey(value) || GeneratedDynamicNames.ContainsKey(value) || TryTranslateDynamicName(value) != null;
			default:
				return false;
			}
		}

		private static string Translate(TranslationKind kind, string value, Dictionary<string, string> table, string source)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}
			if (table.TryGetValue(value, out string translated))
			{
				return translated;
			}
			ReportMissing(kind, value, source);
			return value;
		}

		private static void ReportMissing(TranslationKind kind, string value, string source)
		{
			string missingKey = kind + ":" + value;
			if (MissingKeys.Contains(missingKey))
			{
				return;
			}
			if (!ShouldReport(value))
			{
				MissingKeys.Add(missingKey);
				return;
			}
			if (!MissingKeys.Add(missingKey))
			{
				return;
			}
			Action<MissingTranslation> missingTranslationFound = MissingTranslationFound;
			if (missingTranslationFound != null)
			{
				missingTranslationFound(new MissingTranslation(kind, value, source));
			}
		}

		private static bool ShouldReport(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}
			if (value.StartsWith("##", StringComparison.Ordinal))
			{
				return false;
			}
			return EnglishRegex.IsMatch(value);
		}

		private static string TryTranslateDynamicName(string value)
		{
			string tokenised = DynamicNameBoundaryRegex.Replace(value, " ");
			MatchCollection matches = DynamicNameTokenRegex.Matches(tokenised);
			if (matches.Count == 0)
			{
				return null;
			}
			StringBuilder builder = new StringBuilder();
			int lastIndex = 0;
			bool translatedAny = false;
			foreach (Match match in matches)
			{
				if (match.Index > lastIndex)
				{
					builder.Append(NormaliseDynamicSeparator(tokenised.Substring(lastIndex, match.Index - lastIndex)));
				}
				string token = match.Value;
				if (DynamicNameTokens.TryGetValue(token, out string translatedToken))
				{
					builder.Append(translatedToken);
					translatedAny = true;
				}
				else
				{
					builder.Append(token);
				}
				lastIndex = match.Index + match.Length;
			}
			if (lastIndex < tokenised.Length)
			{
				builder.Append(NormaliseDynamicSeparator(tokenised.Substring(lastIndex)));
			}
			if (!translatedAny)
			{
				return null;
			}
			string translated = CollapseSpaces(builder.ToString()).Trim();
			return translated.Length == 0 ? null : translated;
		}

		private static string NormaliseDynamicSeparator(string separator)
		{
			if (string.IsNullOrEmpty(separator))
			{
				return string.Empty;
			}
			return separator.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
		}

		private static string CollapseSpaces(string value)
		{
			while (value.IndexOf("  ", StringComparison.Ordinal) >= 0)
			{
				value = value.Replace("  ", " ");
			}
			return value;
		}
	}
}
