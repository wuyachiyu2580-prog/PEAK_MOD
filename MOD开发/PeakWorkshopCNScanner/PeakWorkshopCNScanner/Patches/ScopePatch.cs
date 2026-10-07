using System;
using System.Reflection;

namespace PeakWorkshopCNScanner
{
    internal static class ScopeState
    {
        [ThreadStatic] internal static MethodBase Current;
    }

    internal static class ScopePatch
    {
        internal static void Prefix(MethodBase __originalMethod, out MethodBase __state)
        {
            __state = ScopeState.Current;
            ScopeState.Current = __originalMethod;
        }

        // Preserve the original exception and unwind even on early returns/other patch failures.
        internal static Exception Finalizer(Exception __exception, MethodBase __state)
        {
            ScopeState.Current = __state;
            return __exception;
        }
    }
}
