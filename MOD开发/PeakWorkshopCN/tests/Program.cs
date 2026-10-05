using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using PeakWorkshopCN;

internal static class Program
{
    private static int _passed;
    private static bool _chinese = true;
    private static MethodInfo Original => typeof(Program).GetMethod(nameof(Tr), BindingFlags.Static | BindingFlags.Public);
    private static MethodInfo Replacement => typeof(Program).GetMethod(nameof(Translate), BindingFlags.Static | BindingFlags.Public);

    private static void Main()
    {
        try { MainCore(); }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetType().FullName);
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    private static void MainCore()
    {
        Check(!TranslationCatalog.TryGet("de", out _), "machine language code is excluded");
        Check(!TranslationCatalog.TryGet("unknown player input", out _), "unknown text falls back");
        Check(!TranslationCatalog.TryGet(null, out _), "null input falls back");
        Check(TranslationCatalog.TryGet("Save", out var save) && save == "保存", "known fixed label");
        Check(TranslationCatalog.TryGet("Home", out var home) && home == "首页", "expanded fixed catalog");
        Check(CatalogData.TryPair("PeakMapForge.Npc.NpcHud.DrawBosses", "Boss名字 BESIEGT!", "Boss名字 DEFEATED!", out var boss) && boss == "已击败 Boss名字！", "catalog dynamic rule");

        var literal = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldstr, "Speichern"),
            new CodeInstruction(OpCodes.Ldstr, "Save"), new CodeInstruction(OpCodes.Call, Original), new CodeInstruction(OpCodes.Ret) };
        Check(LiteralRewriter.Rewrite(literal, Original, Replacement) == 1 && literal.Count == 5, "literal call rewrite");
        Check((string)literal[0].operand == "Speichern" && (string)literal[1].operand == "Save", "original literal arguments retained");
        Check((string)literal[2].operand == "保存" && Equals(literal[3].operand, Replacement), "Chinese argument and replacement stack");
        Check(LiteralRewriter.Rewrite(literal, Original, Replacement) == 0, "rewriter idempotence");
        var variable = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, Original) };
        Check(LiteralRewriter.Rewrite(variable, Original, Replacement) == 0, "author variable strings untouched");
        var branch = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldstr, "Speichern"),
            new CodeInstruction(OpCodes.Ldstr, "Save"), new CodeInstruction(OpCodes.Call, Original) };
        branch[2].labels.Add(new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel());
        Check(LiteralRewriter.Rewrite(branch, Original, Replacement) == 0, "branch labels are not invalidated");
        branch[2].labels.Clear();
        branch[2].blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
        Check(LiteralRewriter.Rewrite(branch, Original, Replacement) == 0, "exception boundaries are not moved");

        Dynamic("PeakMapForge.Launch.LaunchPanel.OnGUI", "17 im Kartenordner", "17 in the maps folder", "地图文件夹中有 17 张地图");
        Dynamic("PeakMapForge.Launch.LaunchPanel.DrawTeamsLine", "2/4 bereit", "2/4 ready", "2/4 已就绪");
        Dynamic("PeakMapForge.Launch.LaunchPanel.DrawPlayers", "2/4 haben die Karte", "2/4 have the map", "2/4 已有地图");
        Dynamic("PeakMapForge.Teams.TeamHud.DrawBanner", "Save wins! 赢 gewinnt!", "Save wins! 赢 wins!", "Save wins! 赢 获胜！");
        Dynamic("PeakMapForge.Npc.NpcHud.DrawBosses", "Boss名字 BESIEGT!", "Boss名字 DEFEATED!", "已击败 Boss名字！");
        Dynamic("PeakMapForge.Editor.EditorUi.OvSize", "-12 × 3 m · 4 m hoch", "-12 × 3 m · 4 m high", "-12 × 3 米 · 高 4 米");
        Dynamic("PeakMapForge.Editor.EditorUi.ScRules", "2,5 m lichte Höhe nötig.", "Needs 2,5 m of clear height.", "需要 2,5 米净空高度。");
        Dynamic("PeakMapForge.Editor.EditorUi.OvTerrainDraft", "12 m lang · 3 m hoch", "12 m long · 3 m high", "长 12 米 · 高 3 米");
        Dynamic("PeakMapForge.Launch.LaunchPanel.DrawPlayers", "„Save 地图“ (Rev. 42) liegt auf diesem Rechner – gebaut wird die Karte des Hosts, nicht deine Auswahl unten.",
            "“Save 地图” (rev 42) is on this computer – the host’s map gets built, not your selection below.",
            "本机已有“Save 地图”（修订 42）。起飞时构建房主的地图，而不是你下方的选择。");
        Dynamic("PeakMapForge.Launch.LaunchPanel.DrawPlayers", "„Name“ (Rev. 8) fehlt auf diesem Rechner. Beim Abflug verlässt du den Raum – hol dir die Karte vom Host (.peakmap).",
            "“Name” (rev 8) is missing on this computer. You’ll leave the room on departure – get the map from the host (.peakmap).",
            "本机缺少“Name”（修订 8）。起飞时你将离开房间，请向房主获取地图（.peakmap）。");
        Dynamic("PeakMapForge.Launch.LaunchPanel.DrawPlayers", "Nicht alle haben Mod und Karte: A, Save, 小明. Wer die Karte nicht hat, verlässt beim Aufbau den Raum; ohne Mod sieht man die rohe Insel.",
            "Not everyone has the mod and the map: A, Save, 小明. Anyone without the map leaves the room while it builds; without the mod they see the raw island.",
            "以下玩家尚未准备好 MOD 或地图：A, Save, 小明。缺少地图者会在构建时离开房间；未安装 MOD 者只能看到原始岛屿。");
        Check(!DynamicCatalog.TryTranslate("PeakMapForge.Teams.TeamHud.DrawBanner", "Alice gewinnt!", "Bob wins!", out _), "mismatched names rejected");
        Check(!DynamicCatalog.TryTranslate("PeakMapForge.Editor.EditorUi.ScRules", "5 m lichte Höhe nötig.", "Needs 7 m of clear height.", out _), "mismatched values rejected");
        Check(!DynamicCatalog.TryTranslate("Untrusted.Author", "Alice gewinnt!", "Alice wins!", out _), "untrusted dynamic source rejected");
        Check(!DynamicCatalog.TryTranslate(null, "a", "b", out _), "missing dynamic source rejected");
        Check(!DynamicCatalog.TryTranslate("PeakMapForge.Teams.TeamHud.DrawBanner", new string('A', 9000), new string('A', 9000), out _), "oversized dynamic input rejected");

        var harmony = new Harmony("peakworkshopcn.tests");
        harmony.Patch(typeof(Program).GetMethod(nameof(RenderLiteral)), transpiler: new HarmonyMethod(typeof(Program), nameof(Transpile)));
        Check(RenderLiteral() == "保存", "actual Harmony literal patch executes");
        _chinese = false;
        Check(RenderLiteral() == "Save", "disabled patch restores original output");
        Check(RenderAuthorText("Save") == "Save", "author title matching a dictionary word is not patched");
        harmony.UnpatchSelf();
        _chinese = true;
        Check(RenderLiteral() == "Save", "UnpatchSelf restores original call");

        harmony.Patch(typeof(Program).GetMethod(nameof(RenderDynamic)), transpiler: new HarmonyMethod(typeof(Program), nameof(TranspileDynamic)));
        Check(RenderDynamic("Save", "Save") == "Save 获胜！", "actual Harmony dynamic patch preserves player name");
        Check(RenderDynamic("Alice", "Bob") == "Bob wins!", "actual dynamic patch falls back on inconsistent parameters");
        _chinese = false;
        Check(RenderDynamic("Save", "Save") == "Save wins!", "disabled dynamic patch restores original output");
        harmony.UnpatchSelf();
        _chinese = true;

        var config = new ConfigFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "peakworkshopcn-tests.cfg"), false) { SaveOnConfigSet = false };
        var own = config.Bind("General", "Enabled", true, "Own original description");
        var unrelated = config.Bind("Other", "Enabled", true, "Unrelated description");
        var getter = typeof(ConfigDescription).GetProperty("Description").GetGetMethod();
        ConfigurationLocalization.Install(harmony, config);
        Check(ConfigurationLocalization.Count == 1, "configuration map uses section and key");
        Check((string)getter.Invoke(own.Description, null) == "总开关。关闭后游戏按原版方式运行。", "actual Harmony configuration description patch");
        Check((string)getter.Invoke(unrelated.Description, null) == "Unrelated description", "unrelated description remains unchanged");
        Check(own.Value && own.Definition.Section == "General" && own.Definition.Key == "Enabled", "configuration value and identity retained");
        Plugin.Chinese = false;
        Check((string)getter.Invoke(own.Description, null) == "Own original description", "disabled configuration description restores original");
        harmony.UnpatchSelf();
        Plugin.Chinese = true;
        Console.WriteLine(_passed + " tests passed; 0 failed.");
    }

    private static void Dynamic(string source, string de, string en, string expected) =>
        Check(DynamicCatalog.TryTranslate(source, de, en, out var result) && result == expected, "dynamic: " + source + " / " + en);
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        _passed++;
    }
    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();
        LiteralRewriter.Rewrite(list, Original, Replacement);
        return list;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public static string Tr(string de, string en) => en;
    public static string Translate(string de, string en, string cn) => _chinese ? cn : Tr(de, en);
    [MethodImpl(MethodImplOptions.NoInlining)] public static string RenderLiteral() => Tr("Speichern", "Save");
    [MethodImpl(MethodImplOptions.NoInlining)] public static string RenderAuthorText(string title) => Tr(title, title);
    [MethodImpl(MethodImplOptions.NoInlining)] public static string RenderDynamic(string deName, string enName) => Tr(deName + " gewinnt!", enName + " wins!");
    public static string TranslateDynamic(string de, string en, string source) =>
        _chinese && DynamicCatalog.TryTranslate(source, de, en, out var cn) ? cn : Tr(de, en);
    private static IEnumerable<CodeInstruction> TranspileDynamic(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();
        LiteralRewriter.RewriteDynamic(list, Original, typeof(Program).GetMethod(nameof(TranslateDynamic)), "PeakMapForge.Teams.TeamHud.DrawBanner");
        return list;
    }
}

namespace PeakWorkshopCN
{
    internal static class Plugin
    {
        internal static bool Chinese = true;
        internal static void Warn(string message) => Console.WriteLine(message);
    }
}
