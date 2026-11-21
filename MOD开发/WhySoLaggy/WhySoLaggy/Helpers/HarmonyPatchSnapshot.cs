using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace WhySoLaggy
{
    internal static class HarmonyPatchSnapshot
    {
        private static readonly object Gate = new object();
        private static Snapshot current;
        private static long capturedFrame = long.MinValue;
        private static int generation;

        internal static int Generation => Volatile.Read(ref generation);

        internal sealed class Snapshot
        {
            private readonly Dictionary<MethodBase, Patches> patches;
            internal readonly MethodBase[] Methods;

            internal Snapshot(MethodBase[] methods)
            {
                Methods = methods;
                patches = new Dictionary<MethodBase, Patches>(methods.Length);
                foreach (MethodBase method in methods)
                {
                    try { patches[method] = Harmony.GetPatchInfo(method); }
                    catch { patches[method] = null; }
                }
            }

            internal Patches GetPatchInfo(MethodBase method) => patches.TryGetValue(method, out var info) ? info : null;
        }

        internal static Snapshot Current
        {
            get
            {
                long frame = DiagnosticContext.Frame;
                lock (Gate)
                {
                    if (current == null || frame == 0 || capturedFrame != frame)
                    {
                        current = Capture();
                        capturedFrame = frame;
                        Interlocked.Increment(ref generation);
                    }
                    return current;
                }
            }
        }

        internal static Snapshot Refresh()
        {
            lock (Gate)
            {
                current = Capture();
                capturedFrame = DiagnosticContext.Frame;
                Interlocked.Increment(ref generation);
                return current;
            }
        }

        internal static void Invalidate()
        {
            lock (Gate) capturedFrame = long.MinValue;
        }

        private static Snapshot Capture() => new Snapshot(Harmony.GetAllPatchedMethods().ToArray());
    }
}
