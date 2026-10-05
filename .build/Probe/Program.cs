using System;
namespace PeakWorkshopCN { internal static class Program { public static void Main() { Console.WriteLine(CatalogData.Texts.Count); Console.WriteLine(CatalogData.Rules.Count); Console.WriteLine(TranslationCatalog.TryGet("Save", out var x)); } } }
