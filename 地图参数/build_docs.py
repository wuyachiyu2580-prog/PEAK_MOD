#!/usr/bin/env python3
"""PEAK 地图参数文档生成器
扫描 DreamyAscent 诊断数据，提取关卡-变体-生成器-步骤的完整参数树，
注入 DisplayNameTranslator 中文翻译，输出中间 JSON + HTML + Excel。
"""

from __future__ import annotations

import json, re, sys, os
from pathlib import Path
from collections import defaultdict, OrderedDict
from datetime import datetime

# ── 路径配置 ──────────────────────────────────────────
BASE = Path(r"c:\Users\Administrator\Desktop\MOD\PEAK")
DATA_ROOT = BASE / "MOD开发" / "作废" / "DreamyAscent" / "data" / "map-data"
OFFICIAL_DIR = DATA_ROOT / "1.62.a-snapshot-v2" / "DreamyAscent Diagnostics"
TR_DIR = DATA_ROOT / "TerrainRandomiser-snapshot-v2" / "DreamyAscent Diagnostics"
TRANSLATOR_CS = BASE / "MOD开发" / "TerrainCustomiserCN" / "TerrainCustomiserCN" / "UI" / "DisplayNameTranslator.cs"
OUT_DIR = BASE / "地图参数"
RAW_DIR = OUT_DIR / "原始数据"
INTERMEDIATE = OUT_DIR / "_aggregated_data.json"

# ── 翻译表 ──────────────────────────────────────────────
# 从 DisplayNameTranslator.cs 提取的核心翻译
TRANSLATIONS = {
    # Segment 英文→中文
    "Beach_Segment": "海滩",
    "Jungle_Segment": "雨林",
    "Roots Segment": "森蕈",
    "Snow_Segment": "雪山",
    "Desert_Segment": "沙漠",
    "Caldera_Segment": "破火山口",
    "Volcano_Segment": "火山口",

    # 变体翻译
    # Beach
    "Default": "默认",
    "SnakeBeach": "蛇滩",
    "RedBeach": "红滩",
    "BlueBeach": "蓝滩",
    "JellyHell": "水母地狱",
    "BlackSand": "黑沙海滩",
    # Jungle
    "Lava": "岩浆",
    "Pillars": "石柱",
    "Thorny": "荆棘",
    "Bombs": "炸弹",
    "Ivy": "藤蔓",
    "SkyJungle": "空中雨林",
    # Roots
    "Cave Mania": "洞穴狂热",
    "Deep Water": "深水",
    "Bomb Beetle": "炸弹甲虫",
    "Deep Woods": "密林",
    "Clearcut": "秃林",
    # Snow
    "Spiky": "尖刺",
    "GeyserHell": "喷泉地狱",
    # Desert
    "NoVariant": "标准",
    "ScorpionsHell": "蝎子地狱",
    "CacusHell": "仙人掌地狱",
    "CactusForest": "仙人掌森林",
    "DynamiteHell": "炸药地狱",
    "TornadoHell": "龙卷风地狱",
    "TumblerHell": "滚石地狱",

    # Grouper 翻译
    "PlateauProps": "台地物件",
    "PlateauRocks": "台地岩石",
    "WallProps": "岩壁物件",
    "WallRocks": "岩壁岩石",
    "Edges": "边缘",
    "Middle": "中部",
    "Props": "物件",
    "Rocks": "岩石",
    "Redwood": "红杉",
    "Platteau": "台地",
    "Waterfalls": "瀑布",
    "LavaRivers": "熔岩河",
    "Pops_Plat": "台地爆炸物",
    "Props_Plat": "台地物件",
    "Props_Wall": "岩壁物件",
    "Rocks_Plat": "台地岩石",
    "Rocks_Wall": "岩壁岩石",
    "IceRockSpawn_L": "冰岩生成·左",
    "IceRockSpawn_R": "冰岩生成·右",
    "Lights": "灯光",
    "- Bomb Beetle Variant": "炸弹甲虫变体",
    "- Cave Mania Variant": "洞穴狂热变体",
    "- Deep Water variant": "深水变体",
    "- Redwood Clearcut Variant": "红杉砍伐变体",

    # Step 翻译
    "Palms": "棕榈树",
    "Bushes": "灌木",
    "Trees": "树木",
    "Driftwood": "漂流木",
    "Jellies": "水母",
    "SeaShells": "贝壳",
    "BeachGrass": "海滩草",
    "Eggs": "蛋",
    "Oasis": "绿洲",
    "Oasis Palms": "绿洲棕榈",
    "Aloe": "芦荟",
    "Beach": "海滩",
    "Cactus": "仙人掌",
    "CactusOnTop": "顶仙人掌",
    "Cactus_Balls": "仙人球",
    "Cactus_Balls (1)": "仙人球(1)",
    "Cactus_Big": "大仙人掌",
    "Cactus_Big_Dry": "大干仙人掌",
    "Dynamite": "炸药",
    "Dynamite_Outside": "外部炸药",
    "Scorpions": "蝎子",
    "ScorpionsHell": "蝎子地狱",
    "TumblerHell": "滚石地狱",
    "Tumblers": "滚石",
    "CanyonScorps": "峡谷蝎",
    "Dead Grass": "枯草",
    "MirageOasis": "幻影绿洲",
    "Dolo": "白云岩",
    "Rocks": "岩石",
    "Rocks (1)": "岩石(1)",
    "RocksBig": "大岩石",
    "RocksSmall": "小岩石",
    "Rocks Flat": "扁平岩石",
    "Small Rocks": "小岩石",
    "Connecting rocks": "连接岩石",
    "Mushroom": "蘑菇",
    "Mush Trees": "蘑菇树",
    "Mush Trees (1)": "蘑菇树(1)",
    "Mush Trees (spores)": "蘑菇树(孢子)",
    "Mush Trees (spores) (1)": "蘑菇树(孢子)(1)",
    "Funky Mushrooms": "诡异菇",
    "Funky Mushrooms (1)": "诡异菇(1)",
    "ExploShrooms": "爆炸菇",
    "ExploShrooms (1)": "爆炸菇(1)",
    "PoisonShrooms": "毒菇",
    "SporeShrooms": "孢子菇",
    "Spore Shrooms": "孢子菇",
    "Shelf Shrooms": "踏板菇",
    "Shelf Shroom Spawns": "踏板菇生成",
    "ShroomSpawner": "蘑菇生成器",
    "Moss Spawners": "苔藓生成器",
    "moss patches": "苔藓",
    "Ferns": "蕨类",
    "Ferns (1)": "蕨类(1)",
    "Vines": "藤蔓",
    "Vines (1)": "藤蔓(1)",
    "Connecting Vines": "连接藤蔓",
    "Thorns": "荆棘",
    "Roots": "树根",
    "Ropes": "绳索",
    "Platform Spawns": "平台生成",
    "Platforms": "平台",
    "Bridges": "桥",
    "Caves": "洞穴",
    "Mineshafts": "矿井",
    "Tunnels": "隧道",
    "Tunnels_Foot": "隧道脚",
    "Rings": "环",
    "Rings small": "小环",
    "Spider Spawners": "蜘蛛生成器",
    "Spiders": "蜘蛛",
    "Beetles": "甲虫",
    "Urchins": "海胆",
    "Urchins (1)": "海胆(1)",
    "Antlion": "蚁狮",
    "LuggageSpawner": "行李生成器",
    "LuggageSpawner (1)": "行李生成器(1)",
    "LuggageSpawner Platforms": "行李平台",
    "LuggageSpawner_Canyon": "峡谷行李",
    "LuggageSpawner_High": "高层行李",
    "LuggageSpawner_Inside": "内部行李",
    "LuggageSpawner_Low": "低层行李",
    "LuggageSpawner_Mirrage": "幻影行李",
    "LuggageSpawner_Outside": "外部行李",
    "Big": "大",
    "Big (1)": "大(1)",
    "Big Redwood": "大红杉",
    "Medium": "中",
    "Small": "小",
    "small": "小",
    "Small (1)": "小(1)",
    "Tall": "高",
    "Mid": "中",
    "Middle": "中部",
    "Start": "起点",
    "End": "终点",
    "Shape": "形状",
    "Foundation": "地基",
    "Base Rocks": "基石",
    "base Stumps": "树桩",
    "Bassalt": "玄武岩",
    "Bassalt (1)": "玄武岩(1)",
    "BassaltClusters": "玄武岩簇",
    "BassaltClusters (1)": "玄武岩簇(1)",
    "Waterfalls": "瀑布",
    "LavaRivers": "熔岩河",
    "LavaRivers (1)": "熔岩河(1)",
    "LavaRivers (2)": "熔岩河(2)",
    "LavaRivers (3)": "熔岩河(3)",
    "Magma": "岩浆",
    "ErikSpawner": "Erik生成器",
    "ShittyPiton": "烂岩钉",
    "Pillars": "石柱",
    "Destroyer": "摧毁者",
    "Destroyer (2)": "摧毁者(2)",
    "Light": "光",
    "Light (1)": "光(1)",
    "Light (2)": "光(2)",
    "Zombie Spawners": "僵尸生成器",
    "SmallRocks": "小岩石",
    "Edge": "边缘",
    "Big_Overhang": "大悬岩",
    "Foot": "脚",
    "Foot_Small": "小脚",
    "Small_End": "小端",
    "Small_Foot": "小脚",
    "Small_Inner": "小内",
    "Medium_Foot": "中脚",
    "Timple": "石笋",
    "redwoods (1)": "红杉(1)",
    "- redwoods": "红杉",
    "redwoods": "红杉",
    "Beetles (1)": "甲虫(1)",
    "Bridges (1)": "桥(1)",
    "Caves (1)": "洞穴(1)",
    "Caves (2)": "洞穴(2)",
    "Caves (3)": "洞穴(3)",
    "End_L": "终点·左",
    "End_R": "终点·右",
    "FlyingFoundation": "浮空地基",
    "FlyingFoundation (1)": "浮空地基(1)",
    "Geysers": "间歇泉",
    "Ivy Spread": "藤蔓丛",
    "Ivy_Big": "大藤蔓",
    "Ivy_Big_Wall": "大藤蔓·墙",
    "Ivy_Wall": "藤蔓·墙",
    "Medium (1)": "中型(1)",
    "SmallFlyers": "小飞虫",
    "Snake": "蛇",
    "Snake (1)": "蛇(1)",
    "Snake (2)": "蛇(2)",
    "Spiders (1)": "蜘蛛(1)",
    "Spires": "石笋群",
    "Spore Shroom Trees": "孢子菇树",
    "Spore Shroom Trees (1)": "孢子菇树(1)",
    "SporeShrooms (1)": "孢子菇(1)",
    "Stumps": "树桩",
    "TreePlatformBridges": "树台桥",
    "TreePlatformBridges (1)": "树台桥(1)",
    "Trees_RockOnly": "岩上树",
    "Trees_Tall": "高树",
    "BigThorns": "大荆棘",
    "BigTree": "巨树",
    "ClusterBerries": "丛生莓",
    "Cold": "寒冷",
    "ColdMedium": "偏冷",
    "DeadTree": "枯树",
    "FlashPlant": "荧光草",
    "Flat": "平坦区",
    "Monsteras": "龟背竹",
    "Mushrooms": "蘑菇丛",
    "NapBerry": "午睡莓",
    "NiceThorns": "荆棘丛",
    "Pine": "松树",
    "Shrub": "矮灌木",
    "Weed": "杂草",
    "Behive": "蜂巢",

    # Step Type 翻译
    "PropSpawner": "物件生成器",
    "PropSpawner_Line": "线性生成器",
    "PropSpawner_Sphere": "球形生成器",
    "PropGrouper": "分组器",
    "DecorSpawner": "装饰生成器",
    "DesertRockSpawner": "沙漠岩石生成器",
    "PropDeleter": "物件删除器",
    "MushroomManager": "蘑菇管理器",

    # Modifier Type 翻译
    "PSM_SetUpRotationToNormal": "向上对齐法线",
    "PSM_RandomScale": "随机缩放",
    "PSM_RandomRotation": "随机旋转",
    "PSM_LocalOffset": "本地偏移",
    "PSM_NormalOffset": "法线偏移",
    "PSM_PlacementOffset": "放置偏移",
    "PSM_RandomOffset": "随机偏移",
    "PSM_ReplaceMaterial": "替换材质",
    "PSM_SetMaterial": "设置材质",
    "PSM_SetMaterialOnChild": "设置子材质",
    "PSM_SetMaterialsOnChild": "设置子材质组",
    "PSM_SetRandomMaterial": "随机材质",
    "PSM_ReplaceMaterialWithRayTargetMaterial": "命中材质替换",
    "PSM_SingleItemSpawner": "单物品生成",
    "PSM_ChildSpawners": "子生成器",
    "PSM_SpecificRotation": "指定旋转",
    "PSM_UpLerp": "向上插值",
    "PSM_BlastVine": "爆炸藤",
    "PSM_PitonNormal": "岩钉法线",
    "PSM_BakedVolumeLightModiferIntensity": "烘焙光强",
    "PSM_RayDirectionOffset": "射线方向偏移",
    "PSM_SetForwardRotationToNormal": "向前对齐法线",
    "PSM_AddPlayerCountBasedDespawner": "按人数销毁",
    "PSM_SetSpawnerPlayerCountRequirement": "人数需求",

    # Constraint Type 翻译
    "PSC_Perlin": "噪声条件",
    "PSC_Height": "高度条件",
    "PSC_Normal": "法线条件",
    "PSC_BannedMaterial": "禁材质",
    "PSC_RequiredMaterial": "需材质",
    "PSC_CircleMask": "圆遮罩",
    "PSC_LineCheck": "射线检测",
    "PSC_NearObject": "近对象",
    "PSC_SameTypeDistance": "同类距离",
    "PSC_SurfaceRestrictions": "表面限制",
    "PSC_VolumeLight": "体积光",
    "PSC_Embedded": "嵌入检测",
    "PSCP_ConnectTreePlatforms": "连接树平台",
    "PSCP_Custom": "自定义后置",
    "PSCP_LineCheck": "后置射线",

    # Property 翻译
    "area": "范围",
    "nrOfSpawns": "生成数量",
    "randomSpawns": "随机生成",
    "chanceToUseSpawner": "使用概率",
    "minSpawnCount": "最少数量",
    "layerType": "层类型",
    "rayDirectionOffset": "射线偏移",
    "rayLength": "射线长度",
    "rayNearCutoff": "近端裁剪",
    "raycastPosition": "射线定位",
    "syncTransforms": "同步变换",
    "rayCastSpawn": "射线生成",
    "spawnChance": "生成概率",
    "minMax": "数值范围",
    "minMaxSpawn": "最小最大生成",
    "minEffect": "最小效果",
    "maxEffect": "最大效果",
    "randomPow": "随机强度",
    "mute": "静音",
    "flipNormal": "翻转法线",
    "maxScaleMult": "最大缩放",
    "minScaleMult": "最小缩放",
    "childName": "子对象名",
    "minRotation": "最小旋转",
    "maxRotation": "最大旋转",
    "addRotation": "加旋转",
    "eulerAngles": "欧拉角",
    "eulerAnglesRandom": "随机角",
    "minAngle": "最小角",
    "maxAngle": "最大角",
    "invert": "反转",
    "inverted": "反转",
    "height": "高度",
    "maxHeight": "最高",
    "minHeight": "最低",
    "offset": "偏移",
    "minOffset": "最小偏移",
    "maxOffset": "最大偏移",
    "radius": "半径",
    "circleSize": "圆尺寸",
    "desiredResult": "期望值",
    "DesiredResult": "期望值",
    "bounds": "范围",
    "outerBounds": "外范围",
    "blendSize": "混合尺寸",
    "inBounds": "范围内",
    "axisMultipliers": "轴倍率",
    "xMult": "X倍",
    "yMult": "Y倍",
    "zMult": "Z倍",
    "spawnCount": "生成数",
    "bannedMaterial": "禁材质",
    "minDistance": "最小距离",
    "findAllSpawners": "全生成器",
    "objects": "物件",
    "onePerPlayer": "每人一个",
    "customColor": "自定义颜色",
    "color": "颜色",
    "customIntensity": "自强度",
    "intensity": "强度",
    "heightOffset": "高度偏移",
    "perlinAmount": "噪声强度",
    "perlinScale": "噪声缩放",
    "perlinSize": "噪声尺寸",
    "perlinOffset": "噪声偏移",
    "postPerlinAmount": "后置噪声强度",
    "postPerlinScale": "后置噪声缩放",
    "resolution": "分辨率",
    "Deadzone": "死区",
    "cellSize": "单元格尺寸",
    "layerMask": "层遮罩",
    "maxRaycastLength": "最大射线长度",
    "drawRayGizmos": "绘制射线辅助线",
    "drawSamplePoints": "绘制采样点",
    "requiredParents": "所需父级",
    "triggerInteraction": "触发器交互",
    "minBadEffects": "最少负面效果",
    "minGoodEffects": "最少正面效果",
    "overallSpawnChance": "总生成概率",
    "effectedLayers": "影响层",
    "whitelistedTagWords": "白名单词",
    "blacklistedTagWords": "黑名单词",
    "destroyAllIfLessThan": "少于销毁",
    "pos": "位置",
    "normal": "法线",
    "rayDir": "射线方向",
    "placement": "放置",
    "spawnerTransform": "生成器变换",
    "currentSpawns": "当前生成",
    "props": "物件",
    "mat": "材质",
    "mats": "材质组",
    "edits": "编辑",
    "objToSpawn": "生成物",
    "replaceThis": "替换项",
    "withThis": "替换为",
    "snapToIncrement": "吸附步",
    "increment": "步长",
    "blend": "混合",
    "minUpLerp": "最小上插",
    "maxUpLerp": "最大上插",
    "center": "中心",
    "spawnedObjects": "已生成对象",
    "spawnPoints": "生成点",
    "scaleMinMax": "最小最大缩放",
    "clampHeights": "限制高度",
    "enterenceObjects": "入口对象",
    "enterences": "入口",
    "inside": "内部",
    "blockerObjects": "阻挡对象",
    "overrideSun": "覆盖日光",
    "daylLightIntensity": "日间光强",
    "nightLightIntensity": "夜间光强",
    "specialSunColor": "日光色",
    "useCustomSun": "自定义光源",
    "specialLight": "光源",
    "useCustomColorVals": "自定义颜色",
    "specialTopColor": "顶色",
    "specialMidColor": "中色",
    "specialBottomColor": "底色",
    "globalShaderVals": "着色值",
    "overrideFog": "改雾",
    "fogDensity": "雾密度",
    "treePlatformParent": "树平台",
    "localStart": "起点",
    "localEnd": "终点",
    "localStartEnd": "起终点",
    "timing": "时机",
    "parents": "父级",
    "baseFog": "基础雾效",
    "blurIterations": "模糊迭代",
    "blurRadius": "模糊半径",
    "outVal": "输出值",
    "returnVal": "返回值",
    "mushroomEffects": "蘑菇效果",
    "mushroomStamAmt": "蘑菇体力",
    "ValidateAfterwards": "随后验证",
    "validationConstraints": "验证条件",
}

# LayerType 枚举值
LAYER_VALUES = {0: "默认", 1: "地形图", 2: "全实体", 3: "全实体非角色", 4: "全实体非默认", 5: "角色+默认", 6: "地图", 7: "地形"}

def tr(key: str) -> str:
    """查翻译表，找不到返回原文"""
    return TRANSLATIONS.get(key, key)

def format_value(v) -> str:
    """把任意JSON值转成可读字符串"""
    if v is None:
        return "—"
    if isinstance(v, bool):
        return "是" if v else "否"
    if isinstance(v, (int, float)):
        if isinstance(v, float):
            if abs(v) < 0.001:
                return "0"
            if abs(v - int(v)) < 0.001:
                return str(int(v))
            return f"{v:.2f}"
        return str(v)
    if isinstance(v, dict):
        # 向量类型
        if "x" in v and "y" in v:
            z = v.get("z", "")
            if z:
                return f"({v['x']:.1f}, {v['y']:.1f}, {z:.1f})"
            return f"({v['x']:.1f}, {v['y']:.1f})"
        return json.dumps(v, ensure_ascii=False)
    if isinstance(v, list):
        return ", ".join(str(x) for x in v[:5]) + ("..." if len(v) > 5 else "")
    return str(v).replace("\n", " ")

def normalize_segment_name(name: str) -> str:
    """统一 Segment 名称"""
    name = name.strip()
    if name == "Roots_Segment":
        return "Roots Segment"
    return name

def parse_directory(diag_dir: Path, source_label: str) -> list[dict]:
    """解析一个诊断目录，返回该目录中所有 segment 的数据"""
    re_file = diag_dir / "RuntimeExport.json"
    if not re_file.exists():
        print(f"  ⚠ 缺少 RuntimeExport.json: {diag_dir.name}")
        return []

    with open(re_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    map_key = data.get("map", {}).get("mapKey", "?")
    segments = data.get("map", {}).get("segments", [])
    results = []

    for seg in segments:
        seg_name = normalize_segment_name(seg.get("segmentName", "?"))
        variant = seg.get("normalizedVariantName", "?")
        variant_type = seg.get("variantSelectionType", "?")
        level_slot = seg.get("levelSlot", -1)

        groupers_data = []
        for g in seg.get("groupers", []):
            g_name = g.get("grouperName", "?")
            steps_data = []
            for s in g.get("steps", []):
                step = {
                    "name": s.get("stepName", "?"),
                    "type": s.get("stepType", "?"),
                    "properties": {},
                    "modifiers": [],
                    "constraints": [],
                }
                # 收集 properties
                for p in s.get("properties", []):
                    step["properties"][p["name"]] = p.get("value")
                # 收集 modifiers
                for m in s.get("modifiers", []):
                    mod = {"type": m["type"], "properties": {}}
                    for p in m.get("properties", []):
                        mod["properties"][p["name"]] = p.get("value")
                    step["modifiers"].append(mod)
                # 收集 constraints
                for c in s.get("constraints", []):
                    con = {"type": c["type"], "properties": {}}
                    for p in c.get("properties", []):
                        con["properties"][p["name"]] = p.get("value")
                    step["constraints"].append(con)
                steps_data.append(step)

            groupers_data.append({"name": g_name, "steps": steps_data})

        results.append({
            "segment": seg_name,
            "variant": variant,
            "variantType": variant_type,
            "levelSlot": level_slot,
            "mapKey": map_key,
            "source": source_label,
            "groupers": groupers_data,
        })

    return results


def aggregate(all_records: list[dict]) -> dict:
    """按 关卡→变体→分组器→步骤 聚合，同变体多样本合并"""
    tree = defaultdict(lambda: defaultdict(list))  # segment → variant → [records]

    for rec in all_records:
        seg = rec["segment"]
        var = rec["variant"]
        tree[seg][var].append(rec)

    result = {}
    for seg_name, variants in sorted(tree.items()):
        seg_cn = tr(seg_name)
        var_dict = {}

        for var_name, records in sorted(variants.items()):
            sample_count = len(records)
            # 取第一个样本作为主数据
            main = records[0]

            # 合并 groupers（去重）
            grouper_map = OrderedDict()
            for rec in records:
                for g in rec["groupers"]:
                    gname = g["name"]
                    if gname not in grouper_map:
                        grouper_map[gname] = {
                            "name": gname,
                            "nameCN": tr(gname),
                            "steps": OrderedDict(),
                        }
                    for s in g["steps"]:
                        sname = s["name"]
                        if sname not in grouper_map[gname]["steps"]:
                            grouper_map[gname]["steps"][sname] = {
                                "name": sname,
                                "nameCN": tr(sname),
                                "type": s["type"],
                                "typeCN": tr(s["type"]),
                                "properties": OrderedDict(),
                                "modifiers": OrderedDict(),
                                "constraints": OrderedDict(),
                            }
                        # 合并 properties（same key 取 first value）
                        for k, v in s["properties"].items():
                            if k not in grouper_map[gname]["steps"][sname]["properties"]:
                                grouper_map[gname]["steps"][sname]["properties"][k] = v
                        # 合并 modifiers（按 type 去重）
                        for m in s["modifiers"]:
                            mtype = m["type"]
                            if mtype not in grouper_map[gname]["steps"][sname]["modifiers"]:
                                grouper_map[gname]["steps"][sname]["modifiers"][mtype] = m["properties"]
                        # 合并 constraints
                        for c in s["constraints"]:
                            ctype = c["type"]
                            if ctype not in grouper_map[gname]["steps"][sname]["constraints"]:
                                grouper_map[gname]["steps"][sname]["constraints"][ctype] = c["properties"]

            # 把 OrderedDict 转回 list
            groupers_out = []
            for gname, gdata in grouper_map.items():
                steps_out = []
                for sname, sdata in gdata["steps"].items():
                    steps_out.append({
                        "name": sdata["name"],
                        "nameCN": sdata["nameCN"],
                        "type": sdata["type"],
                        "typeCN": sdata["typeCN"],
                        "properties": dict(sdata["properties"]),
                        "modifiers": {tr(k): dict(v) for k, v in sdata["modifiers"].items()},
                        "constraints": {tr(k): dict(v) for k, v in sdata["constraints"].items()},
                    })
                groupers_out.append({
                    "name": gdata["name"],
                    "nameCN": gdata["nameCN"],
                    "steps": steps_out,
                })

            var_dict[var_name] = {
                "name": var_name,
                "nameCN": tr(var_name),
                "type": main["variantType"],
                "sampleCount": sample_count,
                "sampleSources": sorted(set(r["mapKey"] for r in records)),
                "groupers": groupers_out,
            }

        result[seg_name] = {
            "name": seg_name,
            "nameCN": seg_cn,
            "variants": var_dict,
        }

    return result


def main():
    print("=" * 60)
    print("PEAK 地图参数文档生成器")
    print("=" * 60)

    # Phase 1: 扫描诊断目录
    print("\n[1/4] 扫描诊断目录...")
    diag_dirs = []
    if OFFICIAL_DIR.exists():
        for d in sorted(OFFICIAL_DIR.iterdir()):
            if d.is_dir():
                diag_dirs.append((d, "official"))
    if TR_DIR.exists():
        for d in sorted(TR_DIR.iterdir()):
            if d.is_dir():
                diag_dirs.append((d, "terrain_randomiser"))
    print(f"  找到 {len(diag_dirs)} 个诊断目录 ({sum(1 for _,t in diag_dirs if t=='official')} 官方 + {sum(1 for _,t in diag_dirs if t=='terrain_randomiser')} TR)")

    # Phase 2: 解析
    print("\n[2/4] 解析 RuntimeExport.json...")
    all_records = []
    for diag_dir, src_label in diag_dirs:
        records = parse_directory(diag_dir, src_label)
        all_records.extend(records)
        if records:
            segs = sorted(set(r["segment"] for r in records))
            print(f"  ✓ {diag_dir.name} → {len(records)} segments: {', '.join(segs)}")

    print(f"  总计 {len(all_records)} 条 segment 记录")

    # Phase 3: 聚合
    print("\n[3/4] 聚合数据...")
    aggregated = aggregate(all_records)
    for seg_name, seg_data in aggregated.items():
        v_count = len(seg_data["variants"])
        g_count = sum(len(v["groupers"]) for v in seg_data["variants"].values())
        s_count = sum(sum(len(g["steps"]) for g in v["groupers"]) for v in seg_data["variants"].values())
        print(f"  {seg_data['nameCN']}({seg_name}): {v_count} 变体, ~{g_count} 分组器, ~{s_count} 步骤")

    # Phase 4: 输出中间 JSON
    print(f"\n[4/4] 输出中间数据...")
    os.makedirs(str(OUT_DIR), exist_ok=True)
    output = {
        "generatedAt": str(datetime.now()),
        "totalDiagnosticDirs": len(diag_dirs),
        "totalSegmentRecords": len(all_records),
        "segments": aggregated,
    }
    with open(str(INTERMEDIATE), "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)
    size_kb = INTERMEDIATE.stat().st_size / 1024
    print(f"  ✓ {INTERMEDIATE.name} ({size_kb:.0f} KB)")


if __name__ == "__main__":
    main()
