using System;
using System.Reflection;
using HarmonyLib;

namespace Lantern_ShootZombies_Night.Patches
{
    internal static class DispelFogFieldGuardPatch
    {
        private static bool _applied;
        private static float _nextLogTime;

        public static void TryApply(Harmony harmony)
        {
            if (_applied || harmony == null) return;

            Type type = AccessTools.TypeByName("DispelFogField");
            if (type == null)
            {
                Plugin.Log?.LogInfo("[DispelFogGuard] DispelFogField type not found; guard skipped.");
                return;
            }

            HarmonyMethod finalizer = new HarmonyMethod(
                AccessTools.Method(typeof(DispelFogFieldGuardPatch), nameof(NullReferenceFinalizer)));

            PatchMethod(harmony, type, "Update", finalizer);
            PatchMethod(harmony, type, "OnDisable", finalizer);
            _applied = true;
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, HarmonyMethod finalizer)
        {
            MethodInfo method = AccessTools.Method(type, methodName);
            if (method == null) return;
            harmony.Patch(method, finalizer: finalizer);
            Plugin.Log?.LogInfo($"[DispelFogGuard] Patched {type.FullName}.{methodName}");
        }

        private static Exception NullReferenceFinalizer(Exception __exception, MethodBase __originalMethod)
        {
            if (__exception == null) return null;
            if (!(__exception is NullReferenceException)) return __exception;

            if (UnityEngine.Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = UnityEngine.Time.unscaledTime + 10f;
                Plugin.Log?.LogWarning($"[DispelFogGuard] Suppressed NullReferenceException in {__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}. This is usually caused by BPR fog-light state being destroyed before its field cleanup.");
            }

            return null;
        }
    }
}
