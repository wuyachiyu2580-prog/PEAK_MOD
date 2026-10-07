using System;
using System.Collections.Generic;

namespace PeakWorkshopCN
{
    internal static class OfficialTerms
    {
        private static readonly Dictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shore"] = "SHORE", ["Beach"] = "SHORE", ["Tropics"] = "TROPICS", ["Jungle"] = "TROPICS",
            ["Roots"] = "ROOTS", ["Alpine"] = "ALPINE", ["Snow"] = "ALPINE", ["Mesa"] = "MESA", ["Desert"] = "MESA",
            ["Volcano"] = "CALDERA", ["Caldera"] = "CALDERA", ["Kiln"] = "THE KILN", ["TheKiln"] = "THE KILN",
            ["Citadel"] = "THE CITADEL", ["Gloom"] = "GLOOM", ["Swamp"] = "GLOOM", ["Peak"] = "PEAK", ["Nadir"] = "AREA_VOID"
        };
        private static readonly Dictionary<string, string> Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SHORE"] = "海岸", ["TROPICS"] = "雨林", ["ROOTS"] = "森蕈", ["ALPINE"] = "雪山", ["MESA"] = "方山",
            ["CALDERA"] = "火山", ["THE KILN"] = "熔炉", ["GLOOM"] = "雾沼", ["THE CITADEL"] = "城塞",
            ["PEAK"] = "顶峰", ["AREA_VOID"] = "天底", ["CAMPFIRE"] = "营火"
        };
        internal static void Initialize(Func<string, string> getText)
        {
            foreach (var key in new List<string>(Text.Keys))
            {
                var value = getText(key);
                if (!string.IsNullOrEmpty(value)) Text[key] = value;
            }
        }
        internal static string Biome(string key) => key != null && Keys.TryGetValue(key, out var id) ? Text[id] : key;
        internal static string Get(string id) => Text.TryGetValue(id, out var text) ? text : id;
    }
}
