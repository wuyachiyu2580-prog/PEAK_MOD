using HarmonyLib;
using PlayersInfo.Helpers;
using PlayersInfo.MonoBehaviours;

namespace PlayersInfo.Patches
{
    /// <summary>
    /// 在 GUIManager 初始化完成后创建 HUD 根节点并触发一次名册扫描。
    /// 此时关卡已就绪，Character.localCharacter / Character.AllCharacters 大概率有值。
    /// 只做"保证已存在"的幂等操作，不重建。
    /// </summary>
    [HarmonyPatch(typeof(GUIManager), "Start")]
    internal static class GUIManagerReadyPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                var tracker = TeamRosterTracker.EnsureExists();
                var coord = TeammateBarsCoordinator.EnsureExists();
                coord.Init();
                coord.AttachToTracker(tracker);
                tracker.RequestRescan();
                PluginLogger.Info("Teammate bars initialized on GUIManager.Start");
            }
            catch (System.Exception ex)
            {
                PluginLogger.Error("GUIManagerReadyPatch failed: " + ex.Message);
            }
        }
    }
}
