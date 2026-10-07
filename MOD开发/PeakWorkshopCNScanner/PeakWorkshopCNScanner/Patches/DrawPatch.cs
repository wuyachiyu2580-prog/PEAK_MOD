using UnityEngine;

namespace PeakWorkshopCNScanner
{
    internal static class DrawPatch
    {
        internal static void Postfix(Rect __0, GUIContent __1)
        {
            if (ScopeState.Current == null) return;
            var plugin = Plugin.Instance;
            if (ReferenceEquals(plugin, null) || !plugin.Scanning || plugin.DrawingMarker) return;
            plugin.Observe(__0, __1);
        }
    }
}
