using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PeakWorkshopCN;

var dll = args[0];
using var assembly = AssemblyDefinition.ReadAssembly(dll);
var allTypes = assembly.MainModule.Types.SelectMany(Types).ToList();
var sites = new List<object>();
int total = 0, translated = 0;
foreach (var type in allTypes)
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    var il = method.Body.Instructions;
    for (int i = 2; i < il.Count; i++)
    {
        if (il[i].OpCode != OpCodes.Call || il[i].Operand is not MethodReference called ||
            called.DeclaringType.FullName != "PeakMapForge.Loc" || called.Name != "Tr" ||
            il[i - 1].OpCode != OpCodes.Ldstr || il[i - 2].OpCode != OpCodes.Ldstr) continue;
        string en = (string)il[i - 1].Operand;
        bool mapped = TranslationCatalog.TryGet(en, out var cn);
        total++;
        if (mapped && method.Name != ".cctor") translated++;
        sites.Add(new { method = method.FullName, english = en, german = (string)il[i - 2].Operand, chinese = cn,
            mapped, staticInitializer = method.Name == ".cctor" });
    }
}
var result = new { originalVersion = assembly.Name.Version.ToString(), literalSites = total,
    mappedMethodSites = translated, sites };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Original: {assembly.Name.Version}; literal sites: {total}; mapped method sites: {translated}; report: {args[1]}");
int dynamicMethods = 0, dynamicCalls = 0;
foreach (var type in allTypes)
foreach (var method in type.Methods.Where(m => m.HasBody && DynamicCatalog.HasRules(type.FullName + "." + m.Name)))
{
    dynamicMethods++;
    dynamicCalls += method.Body.Instructions.Count(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference called &&
        called.DeclaringType.FullName == "PeakMapForge.Loc" && called.Name == "Tr");
}
Console.WriteLine($"Dynamic whitelist: {dynamicMethods} source methods exist; {dynamicCalls} original Tr calls (includes fixed/fallback calls).");
if (args.Length > 3 && args[2] == "--verify")
{
    using var patch = AssemblyDefinition.ReadAssembly(args[3]);
    var fontType = patch.MainModule.GetType("PeakWorkshopCN.FontPatches");
    var targets = fontType.Methods.Single(m => m.Name == "Install").Body.Instructions
        .Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand)
        .Where(s => s.StartsWith("PeakMapForge.") && s.Contains(':')).ToList();
    foreach (var target in targets)
    {
        var parts = target.Split(':');
        var methods = allTypes.Single(t => t.FullName == parts[0]).Methods.Where(m => m.Name == parts[1]).ToList();
        if (methods.Count != 1) throw new Exception("Ambiguous/missing hook: " + target);
        var method = methods[0];
        bool valid = parts[1] == "FontFor" ? method.ReturnType.FullName == "TMPro.TMP_FontAsset" &&
            method.Parameters.Any(p => p.Name == "material" && p.ParameterType.FullName == "UnityEngine.Material&") :
            parts[1] == "NewText" || parts[1] == "MakeText" ? method.ReturnType.FullName.StartsWith("TMPro.TextMeshPro") :
            parts[1] == "Style" || parts[1] == "St" || parts[1] == "_fieldStyle" ? method.ReturnType.FullName == "UnityEngine.GUIStyle" :
            method.ReturnType.FullName == "System.Void";
        if (!valid) throw new Exception("Unexpected font hook signature: " + method.FullName);
    }
    foreach (var field in new[] { "_font", "_fontMaterial" })
        if (!allTypes.Single(t => t.FullName == "PeakMapForge.Generation.LoadOverlay").Fields.Any(f => f.Name == field && f.IsStatic))
            throw new Exception("Missing overlay font field: " + field);
    var configType = patch.MainModule.GetType("PeakWorkshopCN.ConfigurationLocalization");
    var configKeys = configType.Methods.Single(m => m.Name == ".cctor").Body.Instructions
        .Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).Where(s => s.Contains('/') && !s.Contains(' ')).ToHashSet();
    var bindings = new HashSet<string>();
    foreach (var method in allTypes.SelectMany(t => t.Methods).Where(m => m.HasBody))
    {
        string section = null;
        var il = method.Body.Instructions;
        for (int i = 0; i < il.Count - 1; i++)
            if (il[i].OpCode == OpCodes.Ldstr && il[i + 1].OpCode == OpCodes.Ldstr &&
                il.Skip(i + 2).Take(25).Any(x => x.Operand is MethodReference mr && mr.DeclaringType.FullName == "BepInEx.Configuration.ConfigFile" && mr.Name == "Bind"))
            {
                section = (string)il[i].Operand;
                string key = (string)il[i + 1].Operand;
                if (configKeys.Contains(section + "/" + key)) bindings.Add(section + "/" + key);
            }
    }
    Console.WriteLine($"Verified: {targets.Count} font hook signatures; {bindings.Count} section/key names recognized in original binding IL.");
}
else if (args.Length > 2)
    foreach (var item in sites.Cast<dynamic>().Where(s => !s.mapped && ((string)s.method).Contains(args[2])).Take(65))
        Console.WriteLine(item.english);

static IEnumerable<TypeDefinition> Types(TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes.SelectMany(Types)) yield return nested;
}
