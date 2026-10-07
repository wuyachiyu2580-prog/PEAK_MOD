using System;
using System.Reflection;

namespace PeakWorkshopCNScanner
{
    internal static class TextFieldPatch
    {
        // Unfocused fields also call GUIStyle.Draw. Suppress the entire DoTextField scope,
        // including password fields, rather than relying on DrawWithTextSelection alone.
        internal static void Prefix(out MethodBase __state)
        {
            __state = ScopeState.Current;
            ScopeState.Current = null;
        }
        internal static Exception Finalizer(Exception __exception, MethodBase __state)
        {
            ScopeState.Current = __state;
            return __exception;
        }
    }
}
