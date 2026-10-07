using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var root = Path.GetFullPath(args[0]);
var data = Path.GetFullPath(args[1]);
var rules = new List<Rule>();
var unresolved = new List<object>();
var texts = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var file in Directory.GetFiles(data, "*.json").Where(f => Path.GetFileName(f).StartsWith("Translations")))
    foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!)
    {
        if (!texts.TryAdd(entry.Key, entry.Value)) throw new Exception("Duplicate translation: " + entry.Key);
        var slots = Regex.Matches(entry.Key, @"(?<!\{)\{\d+\}(?!\})").Select(m => m.Value).Order().ToArray();
        var translatedSlots = Regex.Matches(entry.Value, @"(?<!\{)\{\d+\}(?!\})").Select(m => m.Value).Order().ToArray();
        if (!slots.SequenceEqual(translatedSlots)) throw new Exception("Changed parameters: " + entry.Key);
    }
// Consolidate the historical in-code fixed catalog into the generated data set.
var core = new Dictionary<string, string>(StringComparer.Ordinal);
var coreTree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(data, "..", "TranslationCatalog.cs")));
foreach (var assignment in coreTree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
    if (assignment.Left is ImplicitElementAccessSyntax access && access.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax key && assignment.Right is LiteralExpressionSyntax value && !texts.ContainsKey(key.Token.ValueText))
    { core[key.Token.ValueText] = value.Token.ValueText; texts[key.Token.ValueText] = value.Token.ValueText; }
if (core.Count > 0) File.WriteAllText(Path.Combine(data, "Translations.Core.json"), JsonSerializer.Serialize(core, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Order())
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    var syntax = tree.GetRoot();
    if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)) throw new Exception("Syntax error: " + file);
    foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => (c.Expression.ToString() == "Loc.Tr" || c.Expression.ToString() == "tr" && new[] { "TeamStandardName", "TeamIssues", "PlayerIssues" }.Contains(c.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText)) && c.ArgumentList.Arguments.Count == 2))
    {
        string source = Source(call);
        var de = Expand(call.ArgumentList.Arguments[0].Expression);
        var en = Expand(call.ArgumentList.Arguments[1].Expression);
        bool added = false;
        foreach (var e in en)
        foreach (var d in de)
            if (TryRule(source, d, e, out var rule)) { rules.Add(rule!); added = true; }
        if (!added && !(de.Count == 1 && en.Count == 1 && de[0].Slots.Count == 0 && en[0].Slots.Count == 0))
            unresolved.Add(new { file = Path.GetRelativePath(root, file), line = tree.GetLineSpan(call.Span).StartLinePosition.Line + 1, source, german = call.ArgumentList.Arguments[0].ToString(), english = call.ArgumentList.Arguments[1].ToString() });
    }
    // Pair assignments in the same initializer/block; only compiled built-in strings enter the catalog.
    foreach (var node in syntax.DescendantNodes().Where(n => n is InitializerExpressionSyntax || n is BlockSyntax))
    {
        var assignments = node.ChildNodes().Select(n => n is ExpressionStatementSyntax statement ? statement.Expression : n)
            .OfType<AssignmentExpressionSyntax>().ToList();
        foreach (var e in assignments.Where(a => Field(a.Left).EndsWith("En", StringComparison.Ordinal)))
        {
            string key = e.Left.ToString();
            string deKey = key.Substring(0, key.Length - 2) + "De";
            var d = assignments.LastOrDefault(a => a.Left.ToString() == deKey);
            if (d == null) continue;
            foreach (var et in Expand(e.Right))
            foreach (var dt in Expand(d.Right))
                if (TryRule("@fields", dt, et, out var rule)) rules.Add(rule!);
        }
    }
}
var unique = rules.DistinctBy(r => r.Source + "\0" + r.De + "\0" + r.En).ToList();
var mapped = unique.Where(r => texts.ContainsKey(r.En)).ToList();
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(Path.Combine(data, "BuiltinRules.json"), JsonSerializer.Serialize(mapped, options));
var inventory = Path.Combine(data, "..", "audit", "2026-10-05-revision");
Directory.CreateDirectory(inventory);
File.WriteAllText(Path.Combine(inventory, "template-inventory.json"), JsonSerializer.Serialize(unique, options));
File.WriteAllText(Path.Combine(inventory, "unresolved-expressions.json"), JsonSerializer.Serialize(unresolved, options));
File.WriteAllText(Path.Combine(inventory, "untranslated-templates.json"), JsonSerializer.Serialize(unique.Where(r => !texts.ContainsKey(r.En)).Select(r => r.En).Distinct().ToList(), options));
Console.WriteLine($"Templates: {unique.Count}; mapped: {mapped.Count}; unresolved expressions: {unresolved.Count}; translation entries: {texts.Count}");

static string Field(ExpressionSyntax expression) => expression is MemberAccessExpressionSyntax m ? m.Name.Identifier.ValueText : expression.ToString();
static string Source(SyntaxNode node)
{
    var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().First().Name.ToString();
    var types = node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText);
    string name = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText
        ?? node.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? ".ctor";
    if (node.Ancestors().OfType<PropertyDeclarationSyntax>().Any() && !node.Ancestors().OfType<MethodDeclarationSyntax>().Any()) name = "get_" + name;
    return ns + "." + string.Join("+", types) + "." + name;
}
static List<Template> Expand(ExpressionSyntax expression)
{
    while (expression is ParenthesizedExpressionSyntax p) expression = p.Expression;
    if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) return new() { new Template(Escape(literal.Token.ValueText), new()) };
    if (expression is InterpolatedStringExpressionSyntax interpolated)
    {
        var text = ""; var slots = new List<string>();
        foreach (var part in interpolated.Contents)
            if (part is InterpolatedStringTextSyntax t) text += Escape(t.TextToken.ValueText);
            else if (part is InterpolationSyntax i) { text += "{" + slots.Count + "}"; slots.Add(i.Expression.ToString() + (i.FormatClause?.ToString() ?? "")); }
        return new() { new Template(text, slots) };
    }
    if (expression is ConditionalExpressionSyntax conditional) return Expand(conditional.WhenTrue).Concat(Expand(conditional.WhenFalse)).Take(32).ToList();
    if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && binary.DescendantNodes().Any(n => n is InterpolatedStringExpressionSyntax || n is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.StringLiteralExpression)))
    {
        return (from a in Expand(binary.Left) from b in Expand(binary.Right) select Join(a, b)).Take(32).ToList();
    }
    return new() { new Template("{0}", new() { expression.ToString() }) };
}
static Template Join(Template a, Template b) => new(a.Text + Regex.Replace(b.Text, @"(?<!\{)\{(\d+)\}(?!\})", m => "{" + (int.Parse(m.Groups[1].Value) + a.Slots.Count) + "}"), a.Slots.Concat(b.Slots).ToList());
static string Escape(string value) => value.Replace("{", "{{").Replace("}", "}}");
static string Normalize(string value) => Regex.Replace(value, @"\b(english: (true|false)|german: (true|false))\b", "language").Replace("En", "Lang").Replace("De", "Lang");
static bool TryRule(string source, Template de, Template en, out Rule? rule)
{
    rule = null;
    if (de.Slots.Count != en.Slots.Count || en.Slots.Count > 12 || en.Text == "de" || en.Text == "en") return false;
    string outer = Regex.Replace(en.Text, @"(?<!\{)\{\d+\}(?!\})", "");
    if (!outer.Any(char.IsLetter) || en.Text.Length > 8192) return false;
    var same = new List<int>();
    if (en.Slots.Count > 0)
    {
        var order = new List<int>();
        for (int i = 0; i < de.Slots.Count; i++)
        {
            int match = en.Slots.FindIndex(s => s == de.Slots[i]);
            if (match < 0) match = en.Slots.FindIndex(s => Normalize(s) == Normalize(de.Slots[i]));
            if (match < 0) { if (de.Slots.Count == 1) match = 0; else return false; }
            order.Add(match);
            if (en.Slots[match] == de.Slots[i]) same.Add(match);
        }
        if (order.Distinct().Count() != en.Slots.Count) return false;
        de = de with { Text = Regex.Replace(de.Text, @"(?<!\{)\{(\d+)\}(?!\})", m => "{" + order[int.Parse(m.Groups[1].Value)] + "}") };
    }
    rule = new Rule(source, de.Text, en.Text, same.Distinct().ToArray());
    return true;
}
record Template(string Text, List<string> Slots);
record Rule(string Source, string De, string En, int[] Same);
