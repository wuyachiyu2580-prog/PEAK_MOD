using System;
using System.Reflection;
using DreamyAscent.Helpers;
using HarmonyLib;

namespace DreamyAscent.Services
{
    internal static class DaCompatibilityPatchService
    {
        private const string HarmonyId = "com.wuyachiyu.dreamyascent.compatibility";
        private static Harmony s_harmony;

        public static void Initialize()
        {
            if (s_harmony != null)
            {
                return;
            }

            s_harmony = new Harmony(HarmonyId);
            PatchGuiManagerStartFinalizer();
        }

        private static void PatchGuiManagerStartFinalizer()
        {
            try
            {
                Type guiManagerType = AccessTools.TypeByName("GUIManager");
                MethodInfo startMethod = guiManagerType != null ? AccessTools.Method(guiManagerType, "Start") : null;
                MethodInfo finalizer = AccessTools.Method(typeof(DaCompatibilityPatchService), nameof(GuiManagerStartFinalizer));
                if (startMethod == null || finalizer == null)
                {
                    DaLog.OnceWarn("compat-gui-manager-start-missing", "Compatibility patch skipped because GUIManager.Start was not found.");
                    return;
                }

                s_harmony.Patch(startMethod, finalizer: new HarmonyMethod(finalizer));
                DaLog.Info("Compatibility patch installed for GUIManager.Start external postfix exceptions.");
            }
            catch (Exception ex)
            {
                DaLog.OnceWarn("compat-gui-manager-start-failed", "Failed to install GUIManager.Start compatibility patch: " + ex.Message);
            }
        }

        private static Exception GuiManagerStartFinalizer(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            if (__exception is NullReferenceException && IsKnownExternalGuiManagerPatchException(__exception))
            {
                DaLog.OnceWarn(
                    "compat-gui-manager-start-external-nullref",
                    "Suppressed external GUIManager.Start NullReferenceException from PatchGUIManager. This is not a DreamyAscent failure.");
                return null;
            }

            return __exception;
        }

        private static bool IsKnownExternalGuiManagerPatchException(Exception exception)
        {
            string stackTrace = exception != null ? exception.StackTrace ?? string.Empty : string.Empty;
            return stackTrace.IndexOf("PatchGUIManager", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   stackTrace.IndexOf("DreamyAscent", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
