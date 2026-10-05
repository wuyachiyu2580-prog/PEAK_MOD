using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PeakWorkshopCN
{
    internal static class DynamicCatalog
    {
        private sealed class Rule
        {
            internal readonly Regex German, English;
            internal readonly Func<Match, string> Format;
            internal Rule(string german, string english, Func<Match, string> format)
            {
                German = Pattern(german);
                English = Pattern(english);
                Format = format;
            }
            private static Regex Pattern(string value) => new Regex("\\A" + value + "\\z",
                RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(10));
        }

        private const string Number = "[-+]?[0-9]+(?:[.,][0-9]+)?";
        private static string Num(string name) => "(?<" + name + ">" + Number + ")";
        private static string Value(Match match, string name) => match.Groups[name].Value;
        internal static bool HasRules(string source) => source != null && (Rules.ContainsKey(source) || CatalogData.HasDynamicRules(source) || FieldSources.Contains(source));

        internal static bool TryTranslate(string source, string german, string english, out string chinese)
        {
            chinese = null;
            if (source != null && (CatalogData.TryPair(source, german, english, out chinese) ||
                FieldSources.Contains(source) && CatalogData.TryPair("@fields", german, english, out chinese))) return true;
            if (source == null || german == null || english == null || german.Length > 8192 || english.Length > 8192 ||
                !Rules.TryGetValue(source, out var rules)) return false;
            try
            {
                foreach (var rule in rules)
                {
                    var en = rule.English.Match(english);
                    if (!en.Success) continue;
                    var de = rule.German.Match(german);
                    if (!de.Success) continue;
                    bool consistent = true;
                    foreach (var name in rule.English.GetGroupNames())
                        if (name != "0" && !string.Equals(en.Groups[name].Value, de.Groups[name].Value, StringComparison.Ordinal))
                        { consistent = false; break; }
                    if (!consistent) continue;
                    chinese = rule.Format(en);
                    return true;
                }
            }
            catch (RegexMatchTimeoutException) { return false; }
            return false;
        }

        private static readonly Dictionary<string, Rule[]> Rules = new Dictionary<string, Rule[]>
        {
            ["PeakMapForge.Launch.LaunchPanel.OnGUI"] = new[]
            {
                new Rule(Num("count") + " im Kartenordner", Num("count") + " in the maps folder",
                    m => "地图文件夹中有 " + Value(m, "count") + " 张地图")
            },
            ["PeakMapForge.Launch.LaunchPanel.DrawTeamsLine"] = new[]
            {
                new Rule(Num("ready") + "/" + Num("total") + " bereit", Num("ready") + "/" + Num("total") + " ready",
                    m => Value(m, "ready") + "/" + Value(m, "total") + " 已就绪"),
                new Rule("Teams " + Num("count"), "Teams " + Num("count"), m => "队伍 " + Value(m, "count"))
            },
            ["PeakMapForge.Launch.LaunchPanel.DrawPlayers"] = new[]
            {
                new Rule(Num("ready") + "/" + Num("total") + " haben die Karte", Num("ready") + "/" + Num("total") + " have the map",
                    m => Value(m, "ready") + "/" + Value(m, "total") + " 已有地图"),
                new Rule("„(?<name>.+)“ \\(Rev\\. " + Num("revision") + "\\) liegt auf diesem Rechner – gebaut wird die Karte des Hosts, nicht deine Auswahl unten\\.",
                    "“(?<name>.+)” \\(rev " + Num("revision") + "\\) is on this computer – the host’s map gets built, not your selection below\\.",
                    m => "本机已有“" + Value(m, "name") + "”（修订 " + Value(m, "revision") + "）。起飞时构建房主的地图，而不是你下方的选择。"),
                new Rule("„(?<name>.+)“ \\(Rev\\. " + Num("revision") + "\\) fehlt auf diesem Rechner\\. Beim Abflug verlässt du den Raum – hol dir die Karte vom Host \\(\\.peakmap\\)\\.",
                    "“(?<name>.+)” \\(rev " + Num("revision") + "\\) is missing on this computer\\. You’ll leave the room on departure – get the map from the host \\(\\.peakmap\\)\\.",
                    m => "本机缺少“" + Value(m, "name") + "”（修订 " + Value(m, "revision") + "）。起飞时你将离开房间，请向房主获取地图（.peakmap）。"),
                new Rule("Nicht alle haben Mod und Karte: (?<names>.+)\\. Wer die Karte nicht hat, verlässt beim Aufbau den Raum; ohne Mod sieht man die rohe Insel\\.",
                    "Not everyone has the mod and the map: (?<names>.+)\\. Anyone without the map leaves the room while it builds; without the mod they see the raw island\\.",
                    m => "以下玩家尚未准备好 MOD 或地图：" + Value(m, "names") + "。缺少地图者会在构建时离开房间；未安装 MOD 者只能看到原始岛屿。")
            },
            ["PeakMapForge.Teams.TeamHud.DrawBanner"] = new[]
            {
                new Rule("(?<name>.+) gewinnt!", "(?<name>.+) wins!", m => Value(m, "name") + " 获胜！")
            },
            ["PeakMapForge.Npc.NpcHud.DrawBosses"] = new[]
            {
                new Rule("(?<name>.+) BESIEGT!", "(?<name>.+) DEFEATED!", m => "已击败 " + Value(m, "name") + "！")
            },
            ["PeakMapForge.Editor.EditorUi.OvSize"] = new[]
            {
                new Rule(Num("width") + " × " + Num("depth") + " m · " + Num("height") + " m hoch",
                    Num("width") + " × " + Num("depth") + " m · " + Num("height") + " m high",
                    m => Value(m, "width") + " × " + Value(m, "depth") + " 米 · 高 " + Value(m, "height") + " 米")
            },
            ["PeakMapForge.Editor.EditorUi.ScRules"] = new[]
            {
                new Rule(Num("height") + " m lichte Höhe nötig\\.", "Needs " + Num("height") + " m of clear height\\.",
                    m => "需要 " + Value(m, "height") + " 米净空高度。")
            },
            ["PeakMapForge.Editor.EditorUi.OvTerrainDraft"] = new[]
            {
                new Rule(Num("length") + " m lang · " + Num("height") + " m hoch",
                    Num("length") + " m long · " + Num("height") + " m high",
                    m => "长 " + Value(m, "length") + " 米 · 高 " + Value(m, "height") + " 米")
            }
        };

        private static readonly HashSet<string> FieldSources = new HashSet<string>(StringComparer.Ordinal)
        {
            "PeakMapForge.Editor.ScatterPreset.get_Label", "PeakMapForge.Editor.ScatterPreset.get_Hint", "PeakMapForge.Editor.ScatterPreset.get_Short",
            "PeakMapForge.Generation.LoadOverlay.StageText", "PeakMapForge.Airport.LobbyOverlay.Body", "PeakMapForge.Airport.LobbyOverlay.StepOf",
            "PeakMapForge.Net.JoinFlow.get_ConfirmText"
        };
    }
}
