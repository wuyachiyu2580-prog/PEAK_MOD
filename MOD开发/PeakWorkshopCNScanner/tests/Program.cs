using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using HarmonyLib;
using Mono.Cecil;
using PeakWorkshopCNScanner;

internal static class Program
{
    private static int _passed;
    private static readonly Exception Sentinel = new InvalidOperationException("Original failure must survive");
    private static bool _scopeSeen, _nestedRestored;

    private static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Tests <original.dll> <UnityEngine.IMGUIModule.dll> <artifact directory>");
        var literals = new Dictionary<string, string> { ["Save"] = "Builtin.Save", ["Other"] = "Builtin.Other", ["Öffnen"] = "Builtin.Open" };
        var classify = new TextClassifier(literals);
        Check(classify.Classify("海岸 / 方山 / 营火") == null, "Chinese not flagged");
        Check(classify.Classify("Save").Kind == "builtin-candidate", "Exact builtin is candidate, not confirmed");
        Check(classify.Classify("Other").Evidence.Contains("author"), "Author name collision explicitly uncertain");
        Check(classify.Classify("Öffnen").Kind == "builtin-candidate", "German supported");
        Check(classify.Classify("火山 → Kiln").Kind == "review", "Mixed CJK residual captured");
        Check(classify.Classify("Desert_Rock_1").Kind == "resource-candidate", "Prefab key captured");
        Check(classify.Classify("M_Rock").Kind == "resource-candidate", "Material key captured");
        Check(classify.Classify("BlueRock").Kind == "review", "Clean display names captured without assuming provenance");
        Check(classify.Classify("Alice's Map") != null, "Author text remains a review candidate");
        Check(classify.Classify("<color=#FFEEAA><b>Save</b></color>").Kind == "builtin-candidate", "Rich text classification");
        Check(classify.Classify("<color=white>海岸</color>") == null, "Rich text tags not false English");
        Check(TextClassifier.PlainText(new string('<', 16000)) == new string('<', 16000), "Malformed markup stays literal with bounded linear parsing");
        Check(classify.Classify("Ctrl + F10 / WASD") == null, "Shortcut excluded");
        Check(classify.Classify("PEAK Workshop / NPC / EN / DE") == null, "Brands and technical abbreviations excluded");
        Check(classify.Classify("12.50 ms / 45 FPS / 200 MB") == null, "Units excluded");
        Check(classify.Classify("Home") != null, "Home remains reviewable rather than blanket shortcut suppression");
        Check(classify.Classify("https://example.test/Alice?x=1") == null, "URL excluded");
        Check(classify.Classify(@"C:\Users\Alice\Map.json") == null, "File path excluded");
        Check(classify.Classify("{{Author}} < 1.25 seconds") != null, "Braces and decimals preserved for review");
        Check(new TextClassifier(literals, new[] { "Alice's Map" }).Classify("Alice's Map") == null, "Explicit exact ignore");
        Check(new TextClassifier(literals, new[] { "save" }).Classify("Save") != null, "Ignore case exact");

        var store = new ScanStore(classify, 2, 5) { AnalysisBudget = 0 };
        Check(store.Observe("Editor", "Save", true) == null && store.Classifications == 0, "Budget prevents classification");
        store.AnalysisBudget = 64;
        Check(store.Observe("Editor", "Save", false) == null && store.Count == 0, "Unsampled frame adds no records");
        var record = store.Observe("Editor", "Save", true);
        Check(ReferenceEquals(record, store.Observe("Editor", "Save", true)) && store.Classifications == 1, "Repeated text uses cache");
        Check(ReferenceEquals(record, store.Observe("Editor", "Save", false)), "Markers use cache outside samples");
        store.Touch(record, 1, "utc1", "scene", "enabled", 1, 2, 3, 4);
        store.Touch(record, 1, "utc2", "scene", "enabled", 5, 6, 7, 8);
        Check(record.SampledFrames == 1 && record.X == 1, "Repeated repaint frame counted once");
        var snapshot = store.Snapshot();
        store.Touch(record, 2, "utc3", "scene2", "disabled", 5, 6, 7, 8);
        Check(snapshot[0].SampledFrames == 1 && record.SampledFrames == 2, "Writer snapshot detached from live state");
        Check(store.Observe("Workshop", "Save", true).Id == 2, "Same text on another UI scope separately identified");
        Check(store.Observe("Editor", "Other", true) == null && store.LimitReached && store.Count == 2, "Record cap enforced");
        Check(ReferenceEquals(record, store.Observe("Editor", "Save", true)), "Existing markers survive full report");
        var negative = new ScanStore(classify, 10, 2) { AnalysisBudget = 64 };
        negative.Observe("A", "方山", true); negative.Observe("A", "海岸", true); negative.Observe("A", "雪山", true);
        Check(negative.CacheCount == 2 && negative.LimitReached, "Negative cache bounded");
        var oversized = new ScanStore(classify, 10, 20) { AnalysisBudget = 64 };
        oversized.Observe("A", new string('a', ScanStore.MaxTextLength + 1), true);
        Check(oversized.OversizeTextSeen && oversized.CacheCount == 0, "Oversize safeguard reported, not silently counted as scanned");
        var charBound = new ScanStore(classify, 10000, 10000) { AnalysisBudget = 1000 };
        for (int i = 0; i < 200; i++) charBound.Observe("A", new string('a', 16000) + i, true);
        Check(charBound.LimitReached && charBound.CacheCount <= 125, "Cache character cap enforced");

        var harmony = new Harmony("scanner.tests");
        try
        {
            foreach (string name in new[] { nameof(Outer), nameof(Inner), nameof(Throws) })
                harmony.Patch(AccessTools.Method(typeof(Program), name),
                    prefix: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Prefix)),
                    finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Finalizer)));
            Outer();
            Check(_scopeSeen && _nestedRestored && ScopeState.Current == null, "Real Harmony scope nested unwind");
            Exception caught = null;
            try { Throws(); } catch (Exception ex) { caught = ex; }
            Check(ReferenceEquals(caught, Sentinel) && ScopeState.Current == null, "Real Harmony preserves exception and clears scope");
        }
        finally { harmony.UnpatchSelf(); }
        _scopeSeen = false; Inner();
        Check(!_scopeSeen && ScopeState.Current == null, "Unpatch restores scope-free behavior");
        DrawHarness.Verify(Check);

        var catalog = SourceCatalog.Load();
        using (var original = AssemblyDefinition.ReadAssembly(args[0]))
        {
            var allTypes = Walk(original.MainModule.Types).ToArray();
            var actual = allTypes.SelectMany(t => t.Methods).Where(m => m.Name == "OnGUI" && m.HasBody && !m.IsStatic &&
                !m.HasGenericParameters && !m.DeclaringType.HasGenericParameters && m.Parameters.Count == 0).ToArray();
            Check(actual.Length == catalog.Scopes.Count && actual.All(m => catalog.Scopes.Any(t => t.Token == m.MetadataToken.ToInt32())), "Manifest covers all actual non-generic OnGUI entries");
            foreach (var scope in catalog.Scopes)
            {
                var target = (MethodDefinition)original.MainModule.LookupToken(scope.Token);
                if (target.Name != "OnGUI" || target.DeclaringType.FullName.Replace('/', '+') != scope.Type || target.HasGenericParameters)
                    throw new Exception("Bad manifest scope " + scope.Type);
            }
            Check(catalog.Literals.Count == 6385 && catalog.Literals.ContainsKey("Show all"), "Embedded evidence catalog loaded from original DLL");
        }
        using (var gui = AssemblyDefinition.ReadAssembly(args[1]))
        {
            var style = gui.MainModule.Types.Single(t => t.FullName == "UnityEngine.GUIStyle");
            var draw = style.Methods.Single(m => m.Name == "Draw" && m.Parameters.Count == 7);
            Check(draw.IsPrivate && draw.HasBody && draw.Parameters[0].ParameterType.FullName == "UnityEngine.Rect" &&
                draw.Parameters[1].ParameterType.FullName == "UnityEngine.GUIContent" && draw.Parameters[2].ParameterType.FullName == "System.Int32" &&
                draw.Parameters.Skip(3).All(p => p.ParameterType.FullName == "System.Boolean"), "Exact managed GUIStyle.Draw signature matches game DLL");
            var clip = gui.MainModule.Types.Single(t => t.FullName == "UnityEngine.GUIClip");
            Check(clip.Properties.Any(p => p.Name == "visibleRect" && p.GetMethod.IsStatic), "Cached clip getter exists");
            var textField = gui.MainModule.Types.Single(t => t.FullName == "UnityEngine.GUI").Methods.Single(m => m.Name == "DoTextField" && m.Parameters.Count == 8);
            Check(textField.HasBody && textField.IsStatic && textField.Parameters[6].ParameterType.FullName == "System.String" &&
                textField.Parameters[7].ParameterType.FullName == "System.Char", "Text field suppression matches final game overload including password path");
        }

        Directory.CreateDirectory(args[2]);
        string sampleDirectory = Path.Combine(args[2], "synthetic-session");
        string raw = "<b>Save</b> {{Author}} \"<&>\"\n1.25 / Alice";
        var report = new ScanReport { ExportedUtc = "synthetic test, not game evidence", Records = new[] {
            new ScanRecord { Id = 1, Kind = "review", Text = raw, Scope = "Test.Scope", SampledFrames = 1 }
        }, VisitedScopes = new[] { "Test.Scope" }, Coverage = "Synthetic export validation only." };
        ReportWriter.Write(sampleDirectory, report);
        ReportWriter.Write(sampleDirectory, report);
        using (var json = File.OpenRead(Path.Combine(sampleDirectory, "report.json")))
        {
            var parsed = (ScanReport)new DataContractJsonSerializer(typeof(ScanReport)).ReadObject(json);
            Check(parsed.Records[0].Text == raw, "JSON round-trip preserves exact text and dynamic parameters");
        }
        string html = File.ReadAllText(Path.Combine(sampleDirectory, "report.html"));
        Check(html.Contains("&lt;b&gt;Save&lt;/b&gt;") && !html.Contains("<b>Save</b>"), "HTML escapes captured markup");
        Check(!File.Exists(Path.Combine(sampleDirectory, "report.json.tmp")), "Atomic rewrite leaves complete report");
        Check(File.ReadAllText(Path.Combine(args[2], "latest-session.txt")).Trim() == sampleDirectory, "Session pointer correct");
        Console.WriteLine("PASS: " + _passed + " checks. No Unity runtime/UI acceptance claimed.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static void Outer()
    {
        var before = ScopeState.Current; Inner(); _nestedRestored = ScopeState.Current == before;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void Inner() { _scopeSeen = ScopeState.Current?.Name == nameof(Inner); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void Throws() { throw Sentinel; }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        _passed++; Console.WriteLine("PASS " + label);
    }
    private static IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in Walk(type.NestedTypes)) yield return nested;
        }
    }
}
