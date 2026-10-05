using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class WorkshopPatches
    {
        private static MethodInfo _tr;
        private static Func<string, string, string> _original;
        private static MethodInfo _bump;
        internal static int PatchedSites;
        internal static int DynamicSites;

        internal static void Install(Harmony harmony, Assembly assembly)
        {
            var loc = assembly.GetType("PeakMapForge.Loc", true);
            _tr = AccessTools.Method(loc, "Tr", new[] { typeof(string), typeof(string) });
            if (_tr == null) throw new MissingMethodException("PeakMapForge.Loc.Tr(string, string)");
            _original = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>), _tr);
            _bump = AccessTools.Method(loc, "Bump");
            var transpiler = new HarmonyMethod(typeof(WorkshopPatches), nameof(TranslateCalls));
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(AccessTools.allDeclared).Cast<MethodBase>()
                    .Concat(type.GetConstructors(AccessTools.allDeclared)))
                {
                    try
                    {
                        if (method.IsAbstract || (method.IsConstructor && method.IsStatic) || method.GetMethodBody() == null) continue;
                        var il = PatchProcessor.ReadMethodBody(method).ToList();
                        bool candidate = false;
                        for (int i = 2; i < il.Count; i++)
                            if (il[i].Key == OpCodes.Call && Equals(il[i].Value, _tr) &&
                                il[i - 2].Key == OpCodes.Ldstr && il[i - 1].Key == OpCodes.Ldstr &&
                                TranslationCatalog.TryGet(il[i - 1].Value as string, out _)) { candidate = true; break; }
                        if (candidate || (DynamicCatalog.HasRules(type.FullName + "." + method.Name) &&
                            il.Any(i => i.Key == OpCodes.Call && Equals(i.Value, _tr)))) harmony.Patch(method, transpiler: transpiler);
                    }
                    catch (Exception ex) { Plugin.Warn("Text hook skipped: " + type.Name + "." + method.Name + ": " + ex.Message); }
                }
            }
            foreach (var target in new[]
            {
                "PeakMapForge.Editor.PrefabCategories:Display",
                "PeakMapForge.Build.Items.ItemCategories:Display",
                "PeakMapForge.Airport.WorkshopLibrary:DisplayCategory",
                "PeakMapForge.Airport.WorkshopLibrary:DisplayDifficulty",
                "PeakMapForge.Airport.WorkshopPanel:DisplayAssetCategory"
            })
            {
                int colon = target.IndexOf(':');
                try
                {
                    var method = AccessTools.Method(assembly.GetType(target.Substring(0, colon)), target.Substring(colon + 1), new[] { typeof(string) });
                    if (method != null) harmony.Patch(method, postfix: new HarmonyMethod(typeof(WorkshopPatches), nameof(CategoryPostfix)));
                }
                catch (Exception ex) { Plugin.Warn("Category hook skipped: " + target + ": " + ex.Message); }
            }
            PatchMethod(assembly, harmony, "PeakMapForge.Airport.WorkshopPanel", "LobbyPlayerRow");
            PatchMethod(assembly, harmony, "PeakMapForge.Airport.WorkshopPanel", "NpcCategoryTiles");
            PatchMethod(assembly, harmony, "PeakMapForge.Airport.WorkshopPanel", "Strip");
            PatchGetter(assembly, harmony, "PeakMapForge.Editor.ScatterPreset", "Label");
            PatchGetter(assembly, harmony, "PeakMapForge.Editor.ScatterPreset", "Hint");
            PatchGetter(assembly, harmony, "PeakMapForge.Editor.ScatterPreset", "Short");
            PatchGetter(assembly, harmony, "PeakMapForge.Build.Items.ItemSounds+Preset", "Label");
            PatchGetter(assembly, harmony, "PeakMapForge.Build.Items.ForgeVfx+Element", "Name");
            PatchGetter(assembly, harmony, "PeakMapForge.Build.Items.ForgeVfx+Element", "Desc");
            PatchGetter(assembly, harmony, "PeakMapForge.Airport.AssetOrigin+LicenseInfo", "Label");
            PatchGetter(assembly, harmony, "PeakMapForge.Airport.AssetOrigin+LicenseInfo", "Hint");
            PatchGetter(assembly, harmony, "PeakMapForge.Editor.EditorSession+MarkerRef", "Label", typeof(WorkshopPatches), nameof(InvalidateMarkerLabel));
            try
            {
                var getText = AccessTools.Method(typeof(LocalizedText), "GetText", new[] { typeof(string), typeof(bool) });
                if (getText != null) harmony.Patch(getText, postfix: new HarmonyMethod(typeof(WorkshopPatches), nameof(GameTextPostfix)));
            }
            catch (Exception ex) { Plugin.Warn("Game text hook skipped: " + ex.Message); }
        }

        private static void PatchMethod(Assembly assembly, Harmony harmony, string typeName, string methodName)
        {
            var type = assembly.GetType(typeName);
            if (type == null) return;
            foreach (var method in type.GetMethods(AccessTools.allDeclared).Where(m => m.Name == methodName))
            {
                try
                {
                    if (!method.IsAbstract && method.GetMethodBody() != null)
                        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(WorkshopPatches), nameof(TranslateCalls)));
                }
                catch (Exception ex) { Plugin.Warn("Direct text hook skipped: " + typeName + "." + methodName + ": " + ex.Message); }
            }
        }

        private static void PatchGetter(Assembly assembly, Harmony harmony, string typeName, string propertyName,
            Type postfixType = null, string postfixName = null)
        {
            var type = assembly.GetType(typeName);
            var getter = type == null ? null : AccessTools.Property(type, propertyName)?.GetGetMethod(true);
            if (getter == null) return;
            try
            {
                harmony.Patch(getter, prefix: postfixType == null ? null : new HarmonyMethod(postfixType, postfixName),
                    postfix: postfixType == null ? new HarmonyMethod(typeof(WorkshopPatches), nameof(FieldPostfix)) : null);
            }
            catch (Exception ex) { Plugin.Warn("Field text hook skipped: " + typeName + "." + propertyName + ": " + ex.Message); }
        }

        private static void FieldPostfix(object __instance, ref string __result)
        {
            if (!Plugin.Chinese || __instance == null) return;
            var type = __instance.GetType();
            string de = type.GetField("De", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
            string en = type.GetField("En", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
            de = de ?? type.GetField("DescDe", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
            en = en ?? type.GetField("DescEn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
            foreach (var prefix in new[] { "Label", "Hint", "Short", "Name" })
            {
                de = de ?? type.GetField(prefix + "De", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
                en = en ?? type.GetField(prefix + "En", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(__instance) as string;
            }
            if (CatalogData.TryPair("@fields", de, en, out var chinese) || TranslationCatalog.TryGet(en, out chinese)) __result = chinese;
        }

        private static void InvalidateMarkerLabel(object __instance)
        {
            var field = __instance.GetType().GetField("_cmLabelEn", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(__instance, !LocEn());
        }

        private static bool LocEn()
        {
            var loc = AccessTools.TypeByName("PeakMapForge.Loc");
            return (bool)(loc?.GetField("En", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) ?? false);
        }

        private static IEnumerable<CodeInstruction> TranslateCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var list = instructions.ToList();
            PatchedSites += LiteralRewriter.Rewrite(list, _tr, AccessTools.Method(typeof(WorkshopPatches), nameof(TranslateLiteral)));
            DynamicSites += LiteralRewriter.RewriteDynamic(list, _tr, AccessTools.Method(typeof(WorkshopPatches), nameof(TranslateDynamic)),
                __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name);
            RawLiteralRewriter.Rewrite(list, __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name);
            return list;
        }

        public static string TranslateLiteral(string german, string english, string chinese)
        {
            string original = _original(german, english);
            return Plugin.Chinese ? chinese : original;
        }

        public static string TranslateDynamic(string german, string english, string source)
        {
            string original = _original(german, english);
            return Plugin.Chinese && DynamicCatalog.TryTranslate(source, german, english, out var chinese) ? chinese : original;
        }

        public static string TranslateRaw(string value)
        {
            if (!Plugin.Chinese || value == null) return value;
            return value == "Kick" ? "踢出" : value == "1 NPC" ? "1 个 NPC" : value == "{0} NPCs" ? "{0} 个 NPC" : value;
        }

        private static void CategoryPostfix(string __0, ref string __result)
        {
            if (Plugin.Chinese && Categories.TryGetValue(__0 ?? "", out var chinese)) __result = chinese;
        }

        private static void GameTextPostfix(string __0, ref string __result)
        {
            if (!Plugin.Chinese || __0 == null) return;
            switch (__0.ToUpperInvariant())
            {
                case "NAME_WORKSHOPPASS": __result = "工坊通行证"; break;
                case "WORKSHOPPASS_OPEN": __result = "打开大厅"; break;
                case "WORKSHOPPASS_VIEW": __result = "查看大厅"; break;
                case "WORKSHOPPASS_LOOK": __result = "自定义外观（护照）"; break;
            }
        }

        internal static void NotifyLanguageChange() => _bump?.Invoke(null, null);

        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Grundformen"] = "基础形状", ["Eigene Modelle"] = "自定义模型", ["Eigene Figuren (animiert)"] = "动画角色摆件",
            ["Eigene Assets"] = "自定义素材", ["Insel-Bausteine"] = "岛屿构件", ["Felsen"] = "岩石",
            ["Felsformationen"] = "岩石群", ["Bäume & Stämme"] = "树木与树干", ["Pflanzen & Büsche"] = "植物与灌木",
            ["Ranken & Moos"] = "藤蔓与苔藓", ["Pilze"] = "蘑菇", ["Wege & Brücken"] = "道路与桥梁",
            ["Bauwerke & Ruinen"] = "建筑与遗迹", ["Beute & Spawner"] = "战利品与生成器", ["Gegenstände"] = "物品",
            ["Lagerfeuer & Spielfluss"] = "篝火与流程", ["Licht & Wasser"] = "光照与水", ["Gegner & Tiere"] = "游戏生物",
            ["Fallen & Gefahren"] = "陷阱与危险", ["Giftpflanzen & Dornen"] = "毒植物与荆棘", ["Sonstiges"] = "其他",
            ["Gelände"] = "地形", ["Streuflächen"] = "散布区域", ["Flächen"] = "区域", ["Spawner"] = "生成器",
            ["Kisten"] = "箱子", ["Werfer"] = "发射器", ["Gefahren & Wetter"] = "危险与天气",
            ["Essen & Trinken"] = "食物与饮料", ["Heilung"] = "治疗", ["Bewegung"] = "移动", ["Werkzeug & Licht"] = "工具与照明",
            ["Waffen"] = "武器", ["Werfen & Spielen"] = "投掷与玩具", ["Mystisch"] = "神秘",
            ["aufstieg"] = "攀登", ["minispiel"] = "小游戏", ["erkundung"] = "探索", ["rätsel"] = "谜题", ["wettkampf"] = "竞技",
            ["leicht"] = "简单", ["mittel"] = "中等", ["schwer"] = "困难", ["extrem"] = "极难",
            ["bauwerke"] = "建筑", ["brücken & wege"] = "桥梁与道路", ["felsen & gelände"] = "岩石与地形",
            ["pflanzen & natur"] = "植物与自然", ["deko & möbel"] = "装饰与家具", ["parkour & hindernisse"] = "跑酷与障碍",
            ["fahrzeuge"] = "载具", ["waffen & werkzeuge"] = "武器与工具", ["figuren & tiere"] = "角色与动物"
        };
    }
}
