// Read-only audit: writes reports only; does not patch or load the game assembly.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PeakWorkshopCN;

string root = Path.GetFullPath(args[0]), dll = Path.GetFullPath(args[1]), patchDll = Path.GetFullPath(args[2]), output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);
var calls = new List<Call>();
var raw = new List<Site>();
var branches = new List<Site>();
var helpers = new List<Site>();
var aliases = new List<Site>();
var fields = new List<Site>();
var files = new List<object>();
var parseErrors = new List<string>();
var sink = new Regex(@"^(?:Label|Hint|Tip|Tooltip|Muted|Heading|Text|Say|Status|SetText|NewText|MakeText|Button|ButtonAt|ListRow|KeyValue|InspectorHeader|InHeader|InCaption|BeginSection|SectionTitle|InfoBox|HelpBox|Toggle|Chip|Chips|Grid|Segmented|SearchField|Field|TextRow|OvLabel|DragListRow|PaTile|Confirm|Toast|Notify|ShowMessage|Message|HudLabel|C)$");
foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Order())
{
    string content = File.ReadAllText(file), rel = Path.GetRelativePath(root, file).Replace('\\', '/');
    var tree = CSharpSyntaxTree.ParseText(content);
    files.Add(new { file = rel, sha256 = Hash(file) });
    parseErrors.AddRange(tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => rel + ": " + d));
    var nodes = tree.GetRoot().DescendantNodes().ToList();
    foreach (var call in nodes.OfType<InvocationExpressionSyntax>())
    {
        string expression = call.Expression.ToString(), name = expression.Split('.').Last().Split('<')[0];
        var arguments = call.ArgumentList.Arguments;
        string source = Source(call);
        int line = tree.GetLineSpan(call.Span).StartLinePosition.Line + 1;
        if (expression == "Loc.Tr" && arguments.Count == 2)
        {
            string de = Literal(arguments[0].Expression), en = Literal(arguments[1].Expression), cn = null;
            bool literal = de != null && en != null;
            string state = literal ? (en == "en" && de == "de" ? "preserve-language-code" : TranslationCatalog.TryGet(en, out cn) ? "dictionary-present" : "missing-dictionary") :
                DynamicCatalog.HasRules(source) ? "source-registered-not-proven-covered" : "no-source-rule";
            calls.Add(new Call(rel, line, source, literal ? "literal" : "nonliteral", state, de ?? arguments[0].ToString(), en ?? arguments[1].ToString(), cn,
                call.Ancestors().Any(n => n is AnonymousFunctionExpressionSyntax || n is LocalFunctionStatementSyntax)));
        }
        if (sink.IsMatch(name))
        {
            foreach (var argument in arguments)
            {
                var a = argument.Expression;
                if (a.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(n => n.Expression.ToString() == "Loc.Tr")) continue;
                bool stringLiteral = a.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(n => n.IsKind(SyntaxKind.StringLiteralExpression) && n.Token.ValueText.Any(char.IsLetter));
                bool data = Regex.IsMatch(a.ToString(), @"(?i)name|label|title|text|desc|prefab|material|\.en\b|\.de\b|message|status|hint|summary|error|tip|caption|string\.");
                if (stringLiteral || data) raw.Add(new Site(rel, line, source, expression, a.ToString(), "syntactic-candidate-not-confirmed-UI-gap"));
            }
        }
        if (expression == "tr" || expression == "Tr") aliases.Add(new Site(rel, line, source, expression, call.ToString(), "review-local-helper-or-delegate"));
    }
    foreach (var node in nodes.OfType<MemberAccessExpressionSyntax>().Where(n => n.ToString() == "Loc.En"))
        branches.Add(new Site(rel, tree.GetLineSpan(node.Span).StartLinePosition.Line + 1, Source(node), "Loc.En", node.Parent.ToString(), "review-display-vs-data-culture"));
    foreach (var node in nodes.OfType<MethodDeclarationSyntax>().Where(n => n.ParameterList.Parameters.Any(p => p.Type?.ToString() == "bool" && Regex.IsMatch(p.Identifier.ValueText, "(?i)english|german"))))
        helpers.Add(new Site(rel, tree.GetLineSpan(node.Span).StartLinePosition.Line + 1, Source(node), node.ReturnType.ToString(), node.ParameterList.ToString(), "explicit-language-helper"));
    foreach (var node in nodes.OfType<AssignmentExpressionSyntax>().Where(n => Regex.IsMatch(n.Left.ToString(), @"(?:De|En)$")))
        fields.Add(new Site(rel, tree.GetLineSpan(node.Span).StartLinePosition.Line + 1, Source(node), node.Left.ToString(), node.Right.ToString(), "bilingual-field-producer"));
}
using var assembly = AssemblyDefinition.ReadAssembly(dll);
using var patch = AssemblyDefinition.ReadAssembly(patchDll);
var types = assembly.MainModule.Types.SelectMany(Types).ToList();
var ilCalls = new List<ILCall>();
var reg = new List<object>();
foreach (var type in types)
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    string source = type.FullName.Replace('/', '+') + "." + method.Name;
    bool registered = DynamicCatalog.HasRules(source);
    var instructions = method.Body.Instructions;
    var targets = new HashSet<Instruction>(instructions.SelectMany(i => i.Operand is Instruction target ? new[] { target } : i.Operand as Instruction[] ?? Array.Empty<Instruction>()));
    int count = 0;
    for (int i = 0; i < instructions.Count; i++)
    {
        var instruction = instructions[i];
        if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference mr || mr.DeclaringType.FullName != "PeakMapForge.Loc" || mr.Name != "Tr") continue;
        count++;
        bool literal = i >= 2 && instructions[i-1].OpCode == OpCodes.Ldstr && instructions[i-2].OpCode == OpCodes.Ldstr;
        string en = literal ? (string)instructions[i-1].Operand : null, cn = null;
        bool mapped = en != null && TranslationCatalog.TryGet(en, out cn);
        bool boundary = targets.Contains(instruction) || method.Body.ExceptionHandlers.Any(e => e.TryStart == instruction || e.TryEnd == instruction || e.HandlerStart == instruction || e.HandlerEnd == instruction || e.FilterStart == instruction);
        bool generic = method.HasGenericParameters || GenericType(type);
        ilCalls.Add(new ILCall(method.FullName, source, instruction.Offset, literal, en, mapped, registered, generic, boundary,
            generic ? "generic-runtime-hook-needs-verification" : boundary ? "rewriter-skips-boundary" : mapped ? "fixed-candidate" : registered ? "dynamic-fallback-candidate" : "no-replacement-candidate"));
    }
    if (registered) reg.Add(new { source, method = method.FullName, trCalls = count, generic = method.HasGenericParameters || GenericType(type) });
}
var dataResourceChecks = patch.MainModule.Resources.OfType<EmbeddedResource>().Where(r => r.Name.StartsWith("PeakWorkshopCN.Data.")).Select(r =>
{
    string name = r.Name["PeakWorkshopCN.Data.".Length..];
    string current = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../PeakWorkshopCN/Data", name));
    string installedHash = Convert.ToHexString(SHA256.HashData(r.GetResourceData()));
    return new { name, installedHash, currentHash = File.Exists(current) ? Hash(current) : null, same = File.Exists(current) && Hash(current) == installedHash };
}).ToList();
var ruleData = CatalogData.Rules.SelectMany(g => g.Value).ToList();
var dynamicRules = ruleData.Where(r => Regex.IsMatch(r.En, @"(?<!\{)\{\d+\}(?!\})")).ToList();
var probes = new List<object>();
foreach (var rule in dynamicRules)
{
    string Fill(string template, string value) => Regex.Replace(template, @"(?<!\{)\{\d+\}(?!\})", value);
    string payload = "玩家{{保留}}_Ω", de = Fill(rule.De, payload), en = Fill(rule.En, payload);
    bool translated = rule.TryMatch(de, en, out var cn);
    if (translated && !cn.Contains(payload)) probes.Add(new { source = rule.Source, template = rule.En, kind = "captured-braces-changed", input = en, output = cn });
    if (rule.Same.Length == 0 && rule.TryMatch(Fill(rule.De, "甲"), Fill(rule.En, "乙"), out var unsafeResult))
        probes.Add(new { source = rule.Source, template = rule.En, kind = "no-capture-equality-check-review-parameter-semantics", input = "de=甲; en=乙", output = unsafeResult });
}
Write("source-files.json", files);
Write("source-parse-errors.json", parseErrors);
Csv("tr-source-calls.csv", calls);
Csv("missing-fixed.csv", calls.Where(c => c.Status == "missing-dictionary"));
Csv("nonliteral-review.csv", calls.Where(c => c.Kind == "nonliteral"));
Csv("tr-il-calls.csv", ilCalls);
Csv("display-candidates.csv", raw);
Csv("language-branches.csv", branches);
Csv("language-helpers.csv", helpers);
Csv("local-bilingual-calls.csv", aliases);
Csv("bilingual-fields.csv", fields);
Write("dynamic-registration.json", reg);
Write("generated-rule-probes.json", probes);
Write("resource-verification.json", dataResourceChecks);
var modules = calls.GroupBy(c => c.File.Split('/')[0]).Select(g => new { module = g.Key, total = g.Count(), mappedFixed = g.Count(c => c.Status == "dictionary-present"), missingFixed = g.Count(c => c.Status == "missing-dictionary"), preservedFixed = g.Count(c => c.Status == "preserve-language-code"), nonliteral = g.Count(c => c.Kind == "nonliteral"), nonliteralSourceRegistered = g.Count(c => c.Status == "source-registered-not-proven-covered") }).OrderByDescending(r => r.missingFixed).ToList();
Write("module-summary.json", modules);
Csv("method-summary.csv", calls.GroupBy(c => new {c.File, c.Source}).Select(g => new { file = g.Key.File, source = g.Key.Source, line = g.Min(c => c.Line), mapped = g.Count(c => c.Status == "dictionary-present"), missing = g.Count(c => c.Status == "missing-dictionary"), nonliteral = g.Count(c => c.Kind == "nonliteral") }).OrderByDescending(m => m.missing));
var report = new { sourceRoot = root, originalVersion = assembly.Name.Version.ToString(), originalHash = Hash(dll), patchVersion = patch.Name.Version.ToString(), patchHash = Hash(patchDll), files = files.Count, parseErrors = parseErrors.Count, sourceCalls = calls.Count, ilCalls = ilCalls.Count, statuses = calls.GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()), uniqueMissingEnglish = calls.Where(c => c.Status == "missing-dictionary").Select(c => c.English).Distinct().Count(), ilMapped = ilCalls.Count(c => c.Mapped), ilNonliteral = ilCalls.Count(c => !c.Literal), ilDynamicSourceNonliteral = ilCalls.Count(c => !c.Literal && c.DynamicSource), ilGenericCalls = ilCalls.Count(c => c.Generic), ilBoundaries = ilCalls.Count(c => c.Boundary), rawDisplayCandidates = raw.Count, directLanguageReads = branches.Count, explicitLanguageHelpers = helpers.Count, localBilingualCalls = aliases.Count, generatedRules = ruleData.Count, generatedDynamicRules = dynamicRules.Count, generatedFixedRules = ruleData.Count - dynamicRules.Count, dynamicRulesWithoutCaptureEquality = dynamicRules.Count(r => r.Same.Length == 0), braceProbeFailures = probes.Count(p => p.GetType().GetProperty("kind").GetValue(p)?.ToString() == "captured-braces-changed"), allEmbeddedDataMatch = dataResourceChecks.All(c => c.same), modules };
Write("summary.json", report);
Console.WriteLine(JsonSerializer.Serialize(report, Options()));

void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Options()), new UTF8Encoding(false));
void Csv<T>(string name, IEnumerable<T> records)
{
    var props = typeof(T).GetProperties();
    string Quote(object value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    using var writer = new StreamWriter(Path.Combine(output, name), false, new UTF8Encoding(true));
    writer.WriteLine(string.Join(',', props.Select(p => Quote(p.Name))));
    foreach (var row in records) writer.WriteLine(string.Join(',', props.Select(p => Quote(p.GetValue(row)))));
}
static string Literal(ExpressionSyntax expr) => expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression) ? lit.Token.ValueText : null;
static string Source(SyntaxNode node)
{
    var ancestors = node.AncestorsAndSelf();
    string ns = ancestors.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
    string type = string.Join('+', ancestors.OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText));
    string member = ancestors.OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    if (member == null && ancestors.OfType<PropertyDeclarationSyntax>().FirstOrDefault() is { } prop) member = "get_" + prop.Identifier.ValueText;
    return ns + "." + type + "." + (member ?? ".ctor");
}
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static bool GenericType(TypeDefinition type) => type.HasGenericParameters || type.DeclaringType != null && GenericType(type.DeclaringType);
static IEnumerable<TypeDefinition> Types(TypeDefinition type) { yield return type; foreach (var n in type.NestedTypes.SelectMany(Types)) yield return n; }
static JsonSerializerOptions Options() => new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
record Call(string File, int Line, string Source, string Kind, string Status, string German, string English, string Chinese, bool InLambdaOrLocalFunction);
record Site(string File, int Line, string Source, string Kind, string Expression, string Status);
record ILCall(string Method, string Source, int Offset, bool Literal, string English, bool Mapped, bool DynamicSource, bool Generic, bool Boundary, string StaticClassification);
