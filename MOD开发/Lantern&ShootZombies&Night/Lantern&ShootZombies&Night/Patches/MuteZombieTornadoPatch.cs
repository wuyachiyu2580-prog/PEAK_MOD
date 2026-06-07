using HarmonyLib;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// Block all zombie sound playback (grunts, knockout, bite).
    /// RPC_PlaySFX is the single entry point for all MushroomZombie audio.
    /// </summary>
    [HarmonyPatch(typeof(MushroomZombie), "RPC_PlaySFX")]
    static class MuteZombieSfxPatch
    {
        private static bool _loggedMute;

        [HarmonyPrefix]
        static bool Prefix()
        {
            if (Plugin.MuteZombieTornado.Value)
            {
                if (!_loggedMute)
                {
                    _loggedMute = true;
                    Plugin.Log?.LogInfo("[DEBUG] [Mute] Zombie SFX muted (first occurrence)");
                }
                return false;
            }
            _loggedMute = false;
            return true;
        }
    }

    /// <summary>
    /// Mute all AudioSources on the Tornado GameObject.
    /// Tornado audio is driven by an Animator; setting mute=true overrides animated volume.
    /// </summary>
    [HarmonyPatch(typeof(Tornado), "Start")]
    static class MuteTornadoSfxPatch
    {
        [HarmonyPostfix]
        static void Postfix(Tornado __instance)
        {
            if (!Plugin.MuteZombieTornado.Value) return;

            foreach (var src in __instance.GetComponentsInChildren<AudioSource>(true))
                src.mute = true;

            Plugin.Log?.LogInfo("[DEBUG] [Mute] Tornado audio sources muted");
        }
    }
}
