using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.IO;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class WorkshopPatches
    {
        private static MethodInfo _tr;
        private static Func<string, string, string> _original;
        private static Action _bump;
        private static int _generation;
        private sealed class MarkerStamp { public MarkerStamp() { } internal int Generation = -1; internal Func<string> Maker; }
        private static ConditionalWeakTable<object, MarkerStamp> Markers = new ConditionalWeakTable<object, MarkerStamp>();
        private static readonly Dictionary<MethodBase, Tuple<int, int>> Sites = new Dictionary<MethodBase, Tuple<int, int>>();
        private static readonly Dictionary<MethodBase, Tuple<Func<object, string>, Func<object, string>>> FieldReaders = new Dictionary<MethodBase, Tuple<Func<object, string>, Func<object, string>>>();
        internal static int PatchedSites => Sites.Values.Sum(v => v.Item1);
        internal static int DynamicSites => Sites.Values.Sum(v => v.Item2);
        internal static int FailedTargets;
        [DataContract] private sealed class Target
        {
            [DataMember] public int Token = 0;
            [DataMember] public string Type = null;
            [DataMember] public string Name = null;
        }
        [DataContract] private sealed class GenericTarget
        {
            [DataMember] public int Token = 0;
            [DataMember] public string[] Arguments = null;
        }

        internal static void Install(Harmony harmony, Assembly assembly)
        {
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != "CC1ECDE51B0A4F6F36DB54DE2232FB49CAB59CEDE43CBFFEECBC83EC7A868ADC")
                    throw new InvalidOperationException("PeakWorkshop DLL differs from the verified 0.2.0 baseline; localization was not installed.");
            var loc = assembly.GetType("PeakMapForge.Loc", true);
            _tr = AccessTools.Method(loc, "Tr", new[] { typeof(string), typeof(string) });
            if (_tr == null) throw new MissingMethodException("PeakMapForge.Loc.Tr(string, string)");
            _original = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>), _tr);
            _bump = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(loc, "Bump"));
            harmony.Patch(AccessTools.Method(loc, "Bump"), postfix: new HarmonyMethod(typeof(WorkshopPatches), nameof(BumpPostfix)));
            var transpiler = new HarmonyMethod(typeof(WorkshopPatches), nameof(TranslateCalls));
            foreach (var target in CatalogData.Load<Target[]>("PatchManifest.json"))
            {
                try
                {
                    var method = assembly.ManifestModule.ResolveMethod(target.Token);
                    if (method.DeclaringType.FullName != target.Type || method.Name != target.Name) throw new InvalidOperationException("Manifest signature mismatch");
                    harmony.Patch(method, transpiler: transpiler);
                }
                catch (Exception ex) { FailedTargets++; Plugin.Warn("Target failed: " + target.Type + "." + target.Name + ": " + ex); }
            }
            // Do not transpile WorkshopPanel.Strip<T> at runtime. Unity/Mono shares
            // the generic body between closed IList<T> instantiations; Harmony's
            // generated wrapper can then use the wrong array element type and throw
            // ArrayTypeMismatchException from Enumerable.ToList on every OnGUI.
            // The callers remain patched, so their fixed/dynamic labels are safe;
            // the generic "Show all" label is intentionally left in the original
            // language until a non-generic display boundary is available.
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
            ResourcePatches.Install(assembly, harmony);
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
                if (postfixType == null)
                {
                    string prefix = propertyName == "Desc" ? "Desc" : propertyName == "Hint" ? "Hint" : "";
                    FieldReaders[getter] = Tuple.Create(BoundAccess.Getter<string>(AccessTools.Field(type, prefix + "De")), BoundAccess.Getter<string>(AccessTools.Field(type, prefix + "En")));
                }
                harmony.Patch(getter, prefix: postfixType == null ? null : new HarmonyMethod(postfixType, postfixName),
                    postfix: postfixType == null ? new HarmonyMethod(typeof(WorkshopPatches), nameof(FieldPostfix)) : null);
            }
            catch (Exception ex) { Plugin.Warn("Field text hook skipped: " + typeName + "." + propertyName + ": " + ex.Message); }
        }

        private static void FieldPostfix(object __instance, MethodBase __originalMethod, ref string __result)
        {
            if (!Plugin.Chinese || __instance == null) return;
            var readers = FieldReaders[__originalMethod];
            string de = readers.Item1(__instance), en = readers.Item2(__instance);
            if (CatalogData.TryPair("@fields", de, en, out var chinese) || TranslationCatalog.TryGet(en, out chinese)) __result = chinese;
        }

        private static void InvalidateMarkerLabel(object __instance, ref string ____cmLabel, Func<string> ___CmMakeLabel)
        {
            var stamp = Markers.GetOrCreateValue(__instance);
            if (stamp.Generation == _generation && ReferenceEquals(stamp.Maker, ___CmMakeLabel)) return;
            ____cmLabel = null;
            stamp.Generation = _generation;
            stamp.Maker = ___CmMakeLabel;
        }

        private static void BumpPostfix() { unchecked { _generation++; } }

        private static IEnumerable<CodeInstruction> TranslateCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var list = instructions.ToList();
            string source = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name;
            int fixedCount = LiteralRewriter.Rewrite(list, _tr, AccessTools.Method(typeof(WorkshopPatches), nameof(TranslateLiteral)), CatalogData.CanonicalSource(source));
            int dynamicCount = LiteralRewriter.RewriteDynamic(list, _tr, AccessTools.Method(typeof(WorkshopPatches), nameof(TranslateDynamic)), source);
            Sites[__originalMethod] = Tuple.Create(fixedCount, dynamicCount);
            RawLiteralRewriter.Rewrite(list, __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name);
            FontPatches.Rewrite(list);
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

        internal static void NotifyLanguageChange() => _bump?.Invoke();

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
