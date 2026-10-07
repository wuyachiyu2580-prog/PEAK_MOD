using System.Text;
using System.Text.Json;
using Mono.Cecil;
using PeakWorkshopCN;

// Offline research only. Never loads Unity or writes the plugin/profile.
var root = Path.GetFullPath(args[0]);
var output = Path.Combine(root, "MOD开发/PeakWorkshopCN/research/2026-10-05-terms-performance");
Directory.CreateDirectory(output);
var dll = @"C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx\plugins\PeakCode-PeakWorkshop\PeakWorkshop\PeakWorkshop.dll";
using var module = ModuleDefinition.ReadModule(dll);
var targets = new[] { "PeakMapForge.Editor.EditorUi", "PeakMapForge.Teams.TeamUi", "PeakMapForge.Airport.WorkshopPanel", "PeakMapForge.Launch.LaunchPanel", "PeakMapForge.Build.ForgeHud", "PeakMapForge.Npc.NpcHud" };
var counts = new List<string[]>();
var fields = new List<string[]>();
foreach (var name in targets)
{
    var type = module.GetType(name) ?? throw new InvalidOperationException(name);
    var relevant = type.Fields.Where(f => !f.IsInitOnly &&
        (f.FieldType.FullName is "UnityEngine.Font" or "UnityEngine.GUIStyle" ||
        (f.FieldType.FullName == "UnityEngine.GUISkin" && f.Name == "_skin"))).ToArray();
    counts.Add(new[] { name, type.Fields.Count.ToString(), relevant.Length.ToString(),
        relevant.Count(f => f.FieldType.FullName == "UnityEngine.Font").ToString(),
        relevant.Count(f => f.FieldType.FullName == "UnityEngine.GUIStyle").ToString(),
        relevant.Count(f => f.FieldType.FullName == "UnityEngine.GUISkin").ToString() });
    foreach (var field in relevant) fields.Add(new[] { name, field.Name, field.FieldType.FullName, field.IsStatic.ToString() });
}
Csv("font-field-counts.csv", new[] { "type", "all_declared_fields", "font_hook_relevant_fields", "Font", "GUIStyle", "GUISkin" }, counts);
Csv("font-field-details.csv", new[] { "type", "field", "field_type", "static" }, fields);
var official = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(Path.Combine(root, ".build/peakworkshopcn-research/SerializedTermsData.json")));
var comparisons = new List<string[]>();
foreach (var (key, values) in official.OrderBy(p => p.Key))
{
    if (values.Length <= 9 || string.IsNullOrEmpty(values[0]) || string.IsNullOrEmpty(values[9])) continue;
    if (TranslationCatalog.TryGet(values[0], out var current))
        comparisons.Add(new[] { key, values[0], values[9], current, current == values[9] ? "same" : "different_review_context" });
}
Csv("official-catalog-candidates.csv", new[] { "official_key", "english", "official_zh", "current_catalog_zh", "comparison" }, comparisons);
var groups = CatalogData.Rules.Select(p => new[] { p.Key, p.Value.Count.ToString(), p.Value.Count(r => !r.En.Contains('{')).ToString(), p.Value.Count(r => r.En.Contains('{')).ToString() }).OrderByDescending(r => int.Parse(r[1])).ToArray();
Csv("rule-buckets.csv", new[] { "source", "rules", "fixed", "dynamic" }, groups);
Console.WriteLine(JsonSerializer.Serialize(new { fontFieldCounts = counts, exactEnglishMatches = comparisons.Count, differingCandidates = comparisons.Count(r => r[4] != "same"), largestRuleBuckets = groups.Take(5) }, new JsonSerializerOptions { WriteIndented = true }));

void Csv(string name, string[] header, IEnumerable<string[]> rows)
{
    using var writer = new StreamWriter(Path.Combine(output, name), false, new UTF8Encoding(true));
    writer.WriteLine(string.Join(",", header.Select(Quote)));
    foreach (var row in rows) writer.WriteLine(string.Join(",", row.Select(Quote)));
}
static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
