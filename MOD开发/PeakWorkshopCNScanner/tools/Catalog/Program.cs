using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2) throw new ArgumentException("Catalog <original PeakWorkshop.dll> <output.json.gz>");
using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
var scopes = new List<object>();
var literals = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var type in Walk(assembly.MainModule.Types))
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    if (method.Name == "OnGUI" && !method.IsStatic && !method.HasGenericParameters &&
        !type.HasGenericParameters && method.Parameters.Count == 0 && method.ReturnType.FullName == "System.Void")
        scopes.Add(new { Token = method.MetadataToken.ToInt32(), Type = type.FullName.Replace('/', '+') });
    var il = method.Body.Instructions;
    for (int i = 2; i < il.Count; i++)
    {
        if (!(il[i].Operand is MethodReference target) || target.Name != "Tr" ||
            target.DeclaringType.FullName != "PeakMapForge.Loc" || target.Parameters.Count != 2) continue;
        if (il[i - 1].OpCode != OpCodes.Ldstr || il[i - 2].OpCode != OpCodes.Ldstr) continue;
        foreach (var text in new[] { (string)il[i - 2].Operand, (string)il[i - 1].Operand })
            if (!string.IsNullOrWhiteSpace(text) && !literals.ContainsKey(text))
                literals.Add(text, type.FullName + "." + method.Name + " @IL_" + il[i].Offset.ToString("X4"));
    }
}
if (scopes.Count == 0 || literals.Count < 100) throw new InvalidDataException("No verified UI catalog found.");
string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0])));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
using (var file = File.Create(args[1]))
using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
    JsonSerializer.Serialize(gzip, new { Sha256 = hash, Scopes = scopes, Literals = literals });
Console.WriteLine($"Catalog: {scopes.Count} OnGUI scopes; {literals.Count} exact original Tr literals; SHA256 {hash}");

static IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> roots)
{
    foreach (var root in roots)
    {
        yield return root;
        foreach (var nested in Walk(root.NestedTypes)) yield return nested;
    }
}
