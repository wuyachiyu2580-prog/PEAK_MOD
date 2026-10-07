using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PeakWorkshopCNScanner;

// Managed stand-ins validate patch plumbing and isolation, not Unity native rendering.
namespace UnityEngine
{
    public struct Rect { }
    public sealed class GUIContent { public string text; }
}
namespace PeakWorkshopCNScanner
{
    internal sealed class Plugin
    {
        internal static Plugin Instance;
        internal bool Scanning = true, DrawingMarker;
        internal int Observations;
        internal void Observe(UnityEngine.Rect rect, UnityEngine.GUIContent content)
        {
            Observations++;
            // A marker uses Draw itself; this must not recursively observe the marker.
            DrawingMarker = true;
            try { DrawHarness.Draw(rect, new UnityEngine.GUIContent { text = "#1" }, -1, false, false, false, false); }
            finally { DrawingMarker = false; }
        }
    }
}
internal static class DrawHarness
{
    internal static void Verify(Action<bool, string> check)
    {
        var harmony = new Harmony("scanner.tests.draw");
        var content = new UnityEngine.GUIContent { text = "M_Rock {{Alice}}" };
        var plugin = Plugin.Instance = new Plugin();
        try
        {
            harmony.Patch(AccessTools.Method(typeof(DrawHarness), nameof(Draw)),
                postfix: new HarmonyMethod(typeof(DrawPatch), nameof(DrawPatch.Postfix)));
            harmony.Patch(AccessTools.Method(typeof(DrawHarness), nameof(WorkshopGUI)),
                prefix: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Prefix)),
                finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Finalizer)));
            harmony.Patch(AccessTools.Method(typeof(DrawHarness), nameof(TextField)),
                prefix: new HarmonyMethod(typeof(TextFieldPatch), nameof(TextFieldPatch.Prefix)),
                finalizer: new HarmonyMethod(typeof(TextFieldPatch), nameof(TextFieldPatch.Finalizer)));
            Draw(default, content, 0, false, false, false, false);
            check(plugin.Observations == 0, "Global draw hook ignores other UI scopes");
            WorkshopGUI(content);
            check(plugin.Observations == 1 && !plugin.DrawingMarker, "Marker recursion blocked; text field draw excluded and scope restored");
            check(content.text == "M_Rock {{Alice}}", "Display content unmodified by observer");
            plugin.Scanning = false;
            WorkshopGUI(content);
            check(plugin.Observations == 1, "Paused draw hook does not observe");
        }
        finally { harmony.UnpatchSelf(); Plugin.Instance = null; }
        WorkshopGUI(content);
        check(ScopeState.Current == null, "Draw and scope hooks cleanly removed");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static void Draw(UnityEngine.Rect position, UnityEngine.GUIContent content,
        int controlId, bool hover, bool active, bool on, bool focus) { GC.KeepAlive(content); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void WorkshopGUI(UnityEngine.GUIContent content)
    {
        TextField(new UnityEngine.GUIContent { text = "Private typed author text" });
        Draw(default, content, 1, false, false, false, false);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void TextField(UnityEngine.GUIContent content)
    { Draw(default, content, 2, false, false, false, false); }
}
