using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PeakWorkshopCN
{
    /// <summary>
    /// Keeps resource keys unchanged while translating the editor's display boundary.
    /// Only known built-in keys are translated; author supplied names remain untouched.
    /// </summary>
    internal static class ResourcePatches
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Desert_Rock_1"] = "方山岩石 1",
            ["Basalt Cluster"] = "玄武岩簇",
            ["Basalt Cluster 02"] = "玄武岩簇 2",
            ["Basalt Column fire Decoy"] = "玄武岩柱（火焰诱饵）",
            ["Basalt Column Fixed Variant"] = "玄武岩柱（固定变体）",
            ["CliffPillar"] = "峭壁石柱",
            ["LavaPillar"] = "熔岩石柱",
            ["LavaPillar2"] = "熔岩石柱 2",
            ["LavaPillar_ExtraTall"] = "超高熔岩石柱",
            ["LavaPillar_Platform"] = "熔岩石柱平台",
            ["LavaPillar_Tall"] = "高熔岩石柱",
            ["Petrify Crystal 1"] = "石化水晶 1",
            ["Pillar"] = "石柱",
            ["rock_LongPillar"] = "长石柱",
            ["rock_Ring"] = "环形岩石",
            ["rock_Ring_Thin"] = "薄环形岩石",
            ["rock_Snake"] = "蛇形岩石",
            ["Rock_Wall"] = "岩石墙",
            ["Spire"] = "尖塔岩石",
            ["BlueRock"] = "蓝色岩石",
            ["DesertRock"] = "方山岩石",
            ["DesertRockSpec"] = "方山特殊岩石",
            ["RealSaltRock"] = "天然盐岩",
            ["RedRock"] = "方山岩石",
            ["Rock"] = "雨林岩石",
            ["Rock 1"] = "岩石 1",
            ["Rock Campfire"] = "营火岩石",
            ["Rock Crystal"] = "水晶岩石",
            ["Rock ice"] = "雪山冰岩",
            ["Rock peak"] = "顶峰岩石",
            ["Rock Volcano"] = "火山岩石",
            ["Rock Volcano Hot"] = "火山热岩",
            ["Rock Volcano Hot Bridge"] = "火山热岩桥",
            ["Rock Volcano petrify"] = "火山石化岩",
            ["SaltRock"] = "海岸盐岩",
            ["SwampRock Swampy"] = "雾沼湿岩",
            ["TempleRock"] = "神殿岩石",
            ["Petrified Salt Rock"] = "石化盐岩",
            ["M_Rock"] = "岩石材质",
            ["M_Stone"] = "石材质",
            ["M_Sand"] = "沙地材质",
            ["M_Snow"] = "雪地材质",
            ["M_Ice"] = "冰材质",
            ["M_Mud"] = "泥地材质",
            ["M_Lava"] = "熔岩材质",
            ["M_Water"] = "水材质",
            ["M_Grass"] = "草地材质",
            ["M_Dirt"] = "泥土材质"
        };

        private static readonly Dictionary<string, string> DisplayCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static MethodInfo _nativeBiomes;
        private static MethodInfo _isAsset;
        private static MethodInfo _isModel;
        private static MethodInfo _isCustomItem;

        private static readonly (string Prefix, string Label)[] BuiltinPrefixes =
        {
            ("Desert_", "方山"), ("Mesa_", "方山"), ("Shore_", "海岸"),
            ("Beach_", "海岸"), ("Tropics_", "雨林"), ("Jungle_", "雨林"),
            ("Roots_", "森蕈"), ("Alpine_", "雪山"), ("Snow_", "雪山"),
            ("Caldera_", "火山"), ("Volcano_", "火山"), ("Kiln_", "熔炉"),
            ("TheKiln_", "熔炉"), ("Gloom_", "雾沼"), ("Swamp_", "雾沼"),
            ("Citadel_", "城塞"), ("Peak_", "顶峰"), ("Nadir_", "天底")
            ,("M_Rock_", "岩石材质"), ("M_Stone_", "石材质"), ("M_Sand_", "沙地材质"),
            ("M_Snow_", "雪地材质"), ("M_Ice_", "冰材质"), ("M_Mud_", "泥地材质"),
            ("M_Lava_", "熔岩材质"), ("M_Water_", "水材质"), ("M_Grass_", "草地材质"),
            ("M_Dirt_", "泥土材质")
        };

        private static readonly (string Token, string Label)[] BuiltinTokens =
        {
            ("Basalt", "玄武岩"), ("Cliff", "峭壁"), ("Pillar", "石柱"),
            ("Lava", "熔岩"), ("Volcano", "火山"), ("Rock", "岩石"),
            ("Crystal", "水晶"), ("Petrify", "石化"), ("Spire", "尖塔"),
            ("Bridge", "桥"), ("Wall", "墙"), ("Platform", "平台"),
            ("Tree", "树"), ("Palm", "棕榈树"), ("Mushroom", "蘑菇"),
            ("Fungus", "真菌"), ("Shroom", "蘑菇"), ("Flower", "花"),
            ("Bush", "灌木"), ("Vine", "藤蔓"), ("Moss", "苔藓"),
            ("Cactus", "仙人掌"), ("Ice", "冰"), ("Snow", "雪"),
            ("Campfire", "营火"), ("Temple", "神殿"), ("Tower", "高塔"),
            ("Chest", "箱子"), ("Spawner", "生成器"), ("Geyser", "间歇泉"),
            ("Jellyfish", "水母"), ("Scorpion", "蝎子"), ("Beehive", "蜂巢")
        };

        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Grundformen"] = "基础形状", ["Eigene Modelle"] = "自定义模型", ["Eigene Figuren (animiert)"] = "动画角色摆件",
            ["Eigene Assets"] = "自定义素材", ["Insel-Bausteine"] = "岛屿构件", ["Felsen"] = "岩石",
            ["Felsformationen"] = "岩石群", ["Bäume & Stämme"] = "树木与树干", ["Pflanzen & Büsche"] = "植物与灌木",
            ["Ranken & Moos"] = "藤蔓与苔藓", ["Pilze"] = "蘑菇", ["Wege & Brücken"] = "道路与桥梁",
            ["Bauwerke & Ruinen"] = "建筑与遗迹", ["Beute & Spawner"] = "战利品与生成器", ["Gegenstände"] = "物品",
            ["Lagerfeuer & Spielfluss"] = "营火与流程", ["Licht & Wasser"] = "光照与水", ["Gegner & Tiere"] = "游戏生物",
            ["Fallen & Gefahren"] = "陷阱与危险", ["Giftpflanzen & Dornen"] = "毒植物与荆棘", ["Sonstiges"] = "其他",
            ["Gelände"] = "地形", ["Streuflächen"] = "散布区域", ["Flächen"] = "区域", ["Spawner"] = "生成器",
            ["Kisten"] = "箱子", ["Werfer"] = "发射器", ["Gefahren & Wetter"] = "危险与天气",
            ["Essen & Trinken"] = "食物与饮料", ["Heilung"] = "治疗", ["Bewegung"] = "移动", ["Werkzeug & Licht"] = "工具与照明",
            ["Waffen"] = "武器", ["Werfen & Spielen"] = "投掷与玩具", ["Mystisch"] = "神秘"
        };

        internal static void Install(Assembly assembly, Harmony harmony)
        {
            _nativeBiomes = AccessTools.Method(assembly.GetType("PeakMapForge.Build.PrefabCatalog"), "NativeBiomes", new[] { typeof(string) });
            _isAsset = AccessTools.Method(assembly.GetType("PeakMapForge.Build.AssetLibrary"), "IsAsset", new[] { typeof(string) });
            _isModel = AccessTools.Method(assembly.GetType("PeakMapForge.Build.Models.ModelLibrary"), "IsModel", new[] { typeof(string) });
            _isCustomItem = AccessTools.Method(assembly.GetType("PeakMapForge.Build.Items.CustomItems"), "IsCustom", new[] { typeof(string) });
            PatchPostfix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "InPrimLabel", nameof(PrefabPostfix), new[] { typeof(string) });
            PatchPostfix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "InSurfaceLabel", nameof(SurfacePostfix), new[] { typeof(string) });
            PatchPostfix(assembly, harmony, "PeakMapForge.Editor.ScatterPresets", "BiomeLabel", nameof(BiomePostfix), new[] { typeof(string) });
            PatchPostfix(assembly, harmony, "PeakMapForge.Editor.PrefabCategories", "Display", nameof(CategoryPostfix), new[] { typeof(string) });
            PatchPostfix(assembly, harmony, "PeakMapForge.Build.RockLook", "Label", nameof(MaterialPostfix), new[] { typeof(string) });
            PatchPrefix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "ListRow", nameof(RowPrefix));
            PatchPrefix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "DragListRow", nameof(RowPrefix));
            PatchPrefix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "DrawRowContent", nameof(RowContentPrefix));
            PatchPrefix(assembly, harmony, "PeakMapForge.Editor.EditorUi", "KeyValue", nameof(KeyValuePrefix));
            PatchTranspiler(assembly, harmony, "PeakMapForge.Editor.PrefabCategories", "Grouped", nameof(SearchTranspiler));
            PatchTranspiler(assembly, harmony, "PeakMapForge.Editor.EditorUi", "DrawTerrain", nameof(SearchTranspiler));
        }

        internal static string Display(string key)
        {
            if (!Plugin.Chinese || string.IsNullOrEmpty(key) || key.StartsWith("model:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("asset:", StringComparison.OrdinalIgnoreCase))
                return key;
            if (DisplayCache.TryGetValue(key, out var cached)) return cached;
            if (Exact.TryGetValue(key, out var value))
            {
                DisplayCache[key] = value;
                return value;
            }
            foreach (var prefix in BuiltinPrefixes)
            {
                if (!key.StartsWith(prefix.Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var tail = key.Substring(prefix.Prefix.Length).Replace('_', ' ').Replace('-', ' ').Trim();
                value = string.IsNullOrEmpty(tail) ? prefix.Label : prefix.Label + " " + tail;
                DisplayCache[key] = value;
                return value;
            }
            if (IsBuiltinResource(key))
            {
                value = TokenizeBuiltin(key);
                DisplayCache[key] = value;
                return value;
            }
            return key;
        }

        private static bool IsBuiltinResource(string key)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("model:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("asset:", StringComparison.OrdinalIgnoreCase)) return false;
            if (IsAuthorResource(key)) return false;
            if (_nativeBiomes != null)
            {
                try
                {
                    var native = _nativeBiomes.Invoke(null, new object[] { key }) as System.Collections.ICollection;
                    if (native != null && native.Count > 0) return true;
                }
                catch { }
            }
            return key.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Pillar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Basalt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Volcano", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Lava", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Petrify", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf("Spire", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAuthorResource(string key)
        {
            foreach (var method in new[] { _isAsset, _isModel, _isCustomItem })
            {
                if (method == null) continue;
                try
                {
                    if (method.Invoke(null, new object[] { key }) is bool value && value) return true;
                }
                catch { }
            }
            return false;
        }

        private static string TokenizeBuiltin(string key)
        {
            string value = key.Replace('_', ' ').Replace('-', ' ').Trim();
            foreach (var token in BuiltinTokens)
                value = ReplaceToken(value, token.Token, token.Label);
            return value;
        }

        private static string ReplaceToken(string value, string token, string label)
        {
            int at = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return value;
            return value.Substring(0, at) + label + value.Substring(at + token.Length);
        }

        internal static int SearchIndex(string key, string search, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(search)) return 0;
            int raw = key == null ? -1 : key.IndexOf(search, comparison);
            if (raw >= 0) return raw;
            string shown = Display(key);
            return shown == key ? -1 : shown.IndexOf(search, comparison);
        }

        private static void PrefabPostfix(string key, ref string __result)
        {
            if (Plugin.Chinese) __result = Display(key);
        }

        private static void SurfacePostfix(string surface, ref string __result)
        {
            if (Plugin.Chinese && !string.IsNullOrEmpty(surface)) __result = Display(surface);
        }

        private static void BiomePostfix(string biome, ref string __result)
        {
            if (Plugin.Chinese) __result = OfficialTerms.Biome(biome);
        }

        private static void MaterialPostfix(string name, ref string __result)
        {
            if (Plugin.Chinese) __result = Display(name);
        }

        private static void CategoryPostfix(string category, ref string __result)
        {
            if (Plugin.Chinese && category != null && Categories.TryGetValue(category, out var value)) __result = value;
        }

        private static void RowPrefix(ref string __1, ref string __2)
        {
            if (!Plugin.Chinese) return;
            __1 = Display(__1);
            __2 = Display(__2);
        }

        private static void RowContentPrefix(ref string __1, ref string __2)
        {
            if (!Plugin.Chinese) return;
            __1 = Display(__1);
            __2 = Display(__2);
        }

        private static void KeyValuePrefix(ref string __1)
        {
            if (Plugin.Chinese) __1 = Display(__1);
        }

        private static IEnumerable<CodeInstruction> SearchTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var indexOf = AccessTools.Method(typeof(string), nameof(string.IndexOf), new[] { typeof(string), typeof(StringComparison) });
            var search = AccessTools.Method(typeof(ResourcePatches), nameof(SearchIndex));
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call) && Equals(instruction.operand, indexOf))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = search;
                }
                yield return instruction;
            }
        }

        private static void PatchPostfix(Assembly assembly, Harmony harmony, string typeName, string methodName, string patchName, Type[] parameters)
        {
            try
            {
                var method = AccessTools.Method(assembly.GetType(typeName), methodName, parameters);
                if (method == null) return;
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(ResourcePatches), patchName));
            }
            catch (Exception ex) { Plugin.Warn("Resource postfix skipped: " + typeName + "." + methodName + ": " + ex.Message); }
        }

        private static void PatchPrefix(Assembly assembly, Harmony harmony, string typeName, string methodName, string patchName)
        {
            try
            {
                var type = assembly.GetType(typeName);
                foreach (var method in type?.GetMethods(AccessTools.allDeclared) ?? Array.Empty<MethodInfo>())
                    if (method.Name == methodName) harmony.Patch(method, prefix: new HarmonyMethod(typeof(ResourcePatches), patchName));
            }
            catch (Exception ex) { Plugin.Warn("Resource prefix skipped: " + typeName + "." + methodName + ": " + ex.Message); }
        }

        private static void PatchTranspiler(Assembly assembly, Harmony harmony, string typeName, string methodName, string patchName)
        {
            try
            {
                var type = assembly.GetType(typeName);
                foreach (var method in type?.GetMethods(AccessTools.allDeclared) ?? Array.Empty<MethodInfo>())
                    if (method.Name == methodName && method.GetMethodBody() != null)
                        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ResourcePatches), patchName));
            }
            catch (Exception ex) { Plugin.Warn("Resource transpiler skipped: " + typeName + "." + methodName + ": " + ex.Message); }
        }
    }
}
