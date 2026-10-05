using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace PeakWorkshopCN
{
    internal static class CatalogData
    {
        internal static readonly Dictionary<string, string> Texts = LoadTexts();
        internal static readonly Dictionary<string, List<BuiltinRule>> Rules = Load<BuiltinRule[]>("BuiltinRules.json")
            .Where(r => Texts.ContainsKey(r.En)).GroupBy(r => r.Source).ToDictionary(g => g.Key, g => g.ToList());

        private static T Load<T>(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PeakWorkshopCN.Data." + name))
            {
                if (stream == null)
                    return typeof(T).IsArray ? (T)(object)Array.CreateInstance(typeof(T).GetElementType()!, 0) :
                        (T)(object)new Dictionary<string, string>(StringComparer.Ordinal);
                return (T)new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).ReadObject(stream);
            }
        }

        private static Dictionary<string, string> LoadTexts()
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var resource in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith("PeakWorkshopCN.Data.Translations", StringComparison.Ordinal)))
                foreach (var pair in Load<Dictionary<string, string>>(resource.Substring("PeakWorkshopCN.Data.".Length))) texts.Add(pair.Key, pair.Value);
            return texts;
        }

        internal static bool TryPair(string source, string de, string en, out string cn)
        {
            cn = null;
            if (de == null || en == null || de.Length > 8192 || en.Length > 8192 || !Rules.TryGetValue(source, out var rules)) return false;
            foreach (var rule in rules)
                if (rule.TryMatch(de, en, out cn)) return true;
            return false;
        }

        internal static bool HasDynamicRules(string source) => source != null && Rules.TryGetValue(source, out var rules) && rules.Any(r => r.En != null && r.En.IndexOf('{') >= 0);
    }

    [DataContract]
    internal sealed class BuiltinRule
    {
        [DataMember] internal string Source = null;
        [DataMember] internal string De = null;
        [DataMember] internal string En = null;
        [DataMember] internal int[] Same = Array.Empty<int>();
        private Regex _de, _en;
        private static readonly Regex Slots = new Regex(@"(?<!\{)\{(\d+)\}(?!\})", RegexOptions.CultureInvariant);

        private static Regex Pattern(string template)
        {
            var seen = new HashSet<string>();
            int offset = 0;
            string pattern = "\\A";
            foreach (Match slot in Slots.Matches(template))
            {
                pattern += Regex.Escape(template.Substring(offset, slot.Index - offset).Replace("{{", "{").Replace("}}", "}"));
                string name = "p" + slot.Groups[1].Value;
                pattern += seen.Add(name) ? "(?<" + name + ">.*?)" : "\\k<" + name + ">";
                offset = slot.Index + slot.Length;
            }
            pattern += Regex.Escape(template.Substring(offset).Replace("{{", "{").Replace("}}", "}")) + "\\z";
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(10));
        }

        internal bool TryMatch(string de, string en, out string cn)
        {
            cn = null;
            if (En.IndexOf('{') < 0)
            {
                if (de != De || en != En) return false;
                cn = CatalogData.Texts[En];
                return true;
            }
            try
            {
                var em = (_en ?? (_en = Pattern(En))).Match(en);
                if (!em.Success) return false;
                var dm = (_de ?? (_de = Pattern(De))).Match(de);
                if (!dm.Success || Same.Any(i => em.Groups["p" + i].Value != dm.Groups["p" + i].Value)) return false;
                cn = Slots.Replace(CatalogData.Texts[En], m => em.Groups["p" + m.Groups[1].Value].Value).Replace("{{", "{").Replace("}}", "}");
                return true;
            }
            catch (RegexMatchTimeoutException) { return false; }
        }
    }
}
