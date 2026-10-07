using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.Json;
using System.Text.RegularExpressions;

var root = Path.GetFullPath(args[0]);
var dll = @"C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\cache\PeakCode-PeakWorkshop\0.2.0\plugins\PeakWorkshop\PeakWorkshop.dll";
using var module = ModuleDefinition.ReadModule(dll);
var types = module.Types.SelectMany(All).ToArray();
var rows = new List<object>();
var aliases = new Dictionary<string, string>();
var generic = new Dictionary<string, object>();
foreach (var type in types)
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    var actual = type.FullName.Replace('/', '+') + "." + method.Name;
    var semantic = actual;
    var match = Regex.Match(method.Name, @"^<([^>]+)>");
    if (match.Success)
    {
        var owner = type;
        while (owner.DeclaringType != null && owner.Name.StartsWith("<")) owner = owner.DeclaringType;
        semantic = owner.FullName.Replace('/', '+') + "." + match.Groups[1].Value;
        aliases[actual] = semantic;
    }
    var ins = method.Body.Instructions;
    bool text = ins.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "PeakMapForge.Loc" && m.Name == "Tr");
    bool font = ins.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "UnityEngine.GUIStyle" && m.Name is ".ctor" or "get_font" or "set_font");
    if ((text || font) && !method.HasGenericParameters && !type.HasGenericParameters)
        rows.Add(new { Token = method.MetadataToken.ToInt32(), Type = type.FullName.Replace('/', '+'), Name = method.Name, Source = actual, Text = text, Font = font });
    foreach (var gm in ins.Select(i => i.Operand).OfType<GenericInstanceMethod>().Where(m => m.Name == "Strip" && m.DeclaringType.FullName == "PeakMapForge.Airport.WorkshopPanel"))
        generic[gm.FullName] = new { Token = gm.ElementMethod.Resolve().MetadataToken.ToInt32(), Arguments = gm.GenericArguments.Select(t => t.FullName.Replace('/', '+')).ToArray() };
}
var options = new JsonSerializerOptions { WriteIndented = true };
var data = Path.Combine(root, "MOD开发/PeakWorkshopCN/PeakWorkshopCN/Data");
File.WriteAllText(Path.Combine(data, "PatchManifest.json"), JsonSerializer.Serialize(rows, options));
File.WriteAllText(Path.Combine(data, "SourceAliases.json"), JsonSerializer.Serialize(aliases, options));
File.WriteAllText(Path.Combine(data, "GenericManifest.json"), JsonSerializer.Serialize(generic.Values, options));
Console.WriteLine($"Manifest {rows.Count}; callback aliases {aliases.Count}; Strip instances {generic.Count}");
static IEnumerable<TypeDefinition> All(TypeDefinition t) { yield return t; foreach (var n in t.NestedTypes.SelectMany(All)) yield return n; }
