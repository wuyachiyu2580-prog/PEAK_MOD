using HarmonyLib;

namespace StateKeeper
{
    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    internal static class PauseMenuMainPagePatches
    {
        private static void Postfix(PauseMenuMainPage __instance)
        {
            StateKeeperUi.BuildPauseMenu(__instance);
        }
    }

    [HarmonyPatch(typeof(PauseMenuMainPage), "OnEnable")]
    internal static class PauseMenuMainPageEnablePatches
    {
        private static void Postfix(PauseMenuMainPage __instance)
        {
            StateKeeperUi.BuildPauseMenu(__instance);
        }
    }
}
