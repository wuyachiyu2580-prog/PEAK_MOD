using HarmonyLib;

namespace UprightCockroach;

[HarmonyPatch(typeof(Character), "Start")]
internal static class CharacterPatch
{
    private static void Postfix(Character __instance)
    {
        if (__instance == null || __instance.isBot || __instance.isZombie || __instance.isScoutmaster ||
            __instance.GetComponent<UprightCockroachVisual>() != null)
        {
            return;
        }

        __instance.gameObject.AddComponent<UprightCockroachVisual>();
    }
}
