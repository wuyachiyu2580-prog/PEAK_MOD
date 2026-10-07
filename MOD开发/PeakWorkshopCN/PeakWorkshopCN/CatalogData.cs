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
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> Fixed = BuildFixed();
        private static readonly Dictionary<string, BuiltinRule[]> Dynamic = Rules.ToDictionary(p => p.Key, p => p.Value.Where(r => r.En.IndexOf('{') >= 0).ToArray());
        internal static readonly Dictionary<string, string> SourceAliases = Load<Dictionary<string, string>>("SourceAliases.json");

        internal static T Load<T>(string name)
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

        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> BuildFixed()
        {
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.Ordinal);
            foreach (var group in Rules)
            {
                var english = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
                foreach (var rule in group.Value.Where(r => r.En.IndexOf('{') < 0))
                {
                    if (!english.TryGetValue(rule.En, out var german)) english.Add(rule.En, german = new Dictionary<string, string>(StringComparer.Ordinal));
                    german[rule.De] = Texts[rule.En];
                }
                result[group.Key] = english;
            }
            return result;
        }

        internal static string CanonicalSource(string source) => source != null && SourceAliases.TryGetValue(source, out var canonical) ? canonical : source;

        internal static bool TryPair(string source, string de, string en, out string cn)
        {
            cn = null;
            source = CanonicalSource(source);
            if (source == null || de == null || en == null || de.Length > 8192 || en.Length > 8192) return false;
            if (Fixed.TryGetValue(source, out var english) && english.TryGetValue(en, out var german) && german.TryGetValue(de, out cn)) return true;
            if (!Dynamic.TryGetValue(source, out var rules)) return false;
            foreach (var rule in rules)
                if (rule.TryMatch(de, en, out cn)) return true;
            return false;
        }

        internal static bool HasDynamicRules(string source) => source != null && Dynamic.TryGetValue(CanonicalSource(source), out var rules) && rules.Length != 0;
    }

    [DataContract]
    internal sealed class BuiltinRule
    {
        [DataMember] internal string Source = null;
        [DataMember] internal string De = null;
        [DataMember] internal string En = null;
        [DataMember] internal int[] Same = Array.Empty<int>();
        private Regex _de, _en;
        private string[] _parts;
        private int[] _slots;
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
                if (_parts == null)
                {
                    var text = CatalogData.Texts[En];
                    var matches = Slots.Matches(text).Cast<Match>().ToArray();
                    var parts = new List<string>();
                    int offset = 0;
                    foreach (var slot in matches)
                    {
                        parts.Add(text.Substring(offset, slot.Index - offset).Replace("{{", "{").Replace("}}", "}"));
                        offset = slot.Index + slot.Length;
                    }
                    parts.Add(text.Substring(offset).Replace("{{", "{").Replace("}}", "}"));
                    _slots = matches.Select(m => int.Parse(m.Groups[1].Value)).ToArray();
                    _parts = parts.ToArray();
                }
                var output = new System.Text.StringBuilder(_parts[0]);
                for (int i = 0; i < _slots.Length; i++) output.Append(em.Groups["p" + _slots[i]].Value).Append(_parts[i + 1]);
                cn = output.ToString();
                return true;
            }
            catch (RegexMatchTimeoutException) { return false; }
        }
    }
}
