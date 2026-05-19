using System;
using HarmonyLib;
using UnityEngine;

namespace ItemInfoCN.Patches
{
    // 日志节流：防止某个边角异常把日志刷爆（60Hz * N 分钟 = 几十万行）。
    // 同一类型异常 10 秒内最多 1 条。
    internal static class ThrottledErrorLog
    {
        private const float IntervalSeconds = 10f;
        private static float _lastUpdate = -999f;
        private static float _lastEquip = -999f;
        private static float _lastCook = -999f;
        private static float _lastReduceUses = -999f;

        internal static void LogUpdate(Exception e) => Log(ref _lastUpdate, "Update", e);
        internal static void LogEquip(Exception e) => Log(ref _lastEquip, "Equip", e);
        internal static void LogCook(Exception e) => Log(ref _lastCook, "FinishCooking", e);
        internal static void LogReduceUses(Exception e) => Log(ref _lastReduceUses, "ReduceUsesRPC", e);

        private static void Log(ref float lastTime, string tag, Exception e)
        {
            float now = Time.unscaledTime;
            if (now - lastTime < IntervalSeconds) return;
            lastTime = now;
            Plugin.Log?.LogError($"[ItemInfoCN] {tag} exception (throttled {IntervalSeconds:F0}s): {e.Message}\n{e.StackTrace}");
        }
    }

    // ── Patch 1: Update display every frame ──
    internal static class ItemInfoUpdatePatch
    {
        [HarmonyPatch(typeof(CharacterItems), "Update")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayUpdate(CharacterItems __instance)
        {
            try
            {
                // 初始化还没成功 → 再试一次后直接返回，不要接着碰 null。
                // AddDisplayObject 本身已经做了前置判空，不会再抛。
                if (Plugin.guiManager == null || Plugin.itemInfoDisplayTextMesh == null)
                {
                    Plugin.AddDisplayObject();
                    return;
                }

                // 观察对象在切换/死亡瞬间可能为 null。
                var observed = Character.observedCharacter;
                if (observed == null || observed.data == null) return;

                if (observed.data.currentItem != null)
                {
                    if (Plugin.hasChanged)
                    {
                        Plugin.hasChanged = false;
                        Plugin.ProcessItemGameObject();
                    }
                    else if (Mathf.Abs(observed.data.sinceItemAttach - Plugin.lastKnownSinceItemAttach) >=
                             Plugin.configForceUpdateTime.Value)
                    {
                        Plugin.hasChanged = true;
                        Plugin.lastKnownSinceItemAttach = observed.data.sinceItemAttach;
                    }

                    if (!Plugin.itemInfoDisplayTextMesh.gameObject.activeSelf)
                        Plugin.itemInfoDisplayTextMesh.gameObject.SetActive(true);
                }
                else
                {
                    if (Plugin.itemInfoDisplayTextMesh.gameObject.activeSelf)
                        Plugin.itemInfoDisplayTextMesh.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                ThrottledErrorLog.LogUpdate(e);
            }
        }
    }

    // ── Patch 2: Equip triggers refresh ──
    internal static class ItemInfoEquipPatch
    {
        [HarmonyPatch(typeof(CharacterItems), "Equip")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayEquip(CharacterItems __instance)
        {
            try
            {
                Character c = Traverse.Create(__instance).Field<Character>("character").Value;
                if (c != null && Character.observedCharacter == c)
                    Plugin.hasChanged = true;
            }
            catch (Exception e)
            {
                ThrottledErrorLog.LogEquip(e);
            }
        }
    }

    // ── Patch 3: Finish cooking triggers refresh ──
    internal static class ItemInfoFinishCookingPatch
    {
        [HarmonyPatch(typeof(ItemCooking), "FinishCooking")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayFinishCooking(ItemCooking __instance)
        {
            try
            {
                if (__instance == null || __instance.item == null) return;
                if (Character.observedCharacter == __instance.item.holderCharacter)
                    Plugin.hasChanged = true;
            }
            catch (Exception e)
            {
                ThrottledErrorLog.LogCook(e);
            }
        }
    }

    // ── Patch 4: Reduce uses triggers refresh ──
    internal static class ItemInfoReduceUsesRPCPatch
    {
        [HarmonyPatch(typeof(Action_ReduceUses), "ReduceUsesRPC")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayReduceUsesRPC(Action_ReduceUses __instance)
        {
            try
            {
                Character c = Traverse.Create(__instance).Property<Character>("character").Value;
                if (c != null && Character.observedCharacter == c)
                    Plugin.hasChanged = true;
            }
            catch (Exception e)
            {
                ThrottledErrorLog.LogReduceUses(e);
            }
        }
    }
}
