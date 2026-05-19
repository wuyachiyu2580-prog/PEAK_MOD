using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// 篝火系统：
    /// 1. 点燃篝火 → 范围内所有玩家灯笼回满。
    /// 2. CampfireHelper 提供篝火近距离检查，供燃料补丁使用。
    /// </summary>
    [HarmonyPatch(typeof(Campfire), "Light_Rpc")]
    internal static class CampfireLightRefuelPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Campfire __instance)
        {
            if (!Plugin.EnableCampfireRefuel.Value) return;
            if (!__instance.Lit) return;

            float radius = __instance.moraleBoostRadius;
            Vector3 pos = __instance.transform.position;
            Plugin.Log?.LogInfo($"[DEBUG] [CampfireRefuel] Campfire lit! radius={radius:F1}");

            foreach (Character ch in Character.AllCharacters)
            {
                if (ch == null || !ch.IsLocal) continue;
                float dist = Vector3.Distance(ch.Center, pos);
                if (dist > radius)
                {
                    Plugin.Log?.LogInfo($"[DEBUG] [CampfireRefuel] Out of range (dist={dist:F1})");
                    continue;
                }

                // 任意状态灌满灯本体（开/关都加满到 max），无灯才失败
                if (LanternHelper.RefillPlayerLanternFuelAnyState(ch))
                {
                    RestoreTracker.ReportLast("Campfire", 0f);
                    LanternUpgradeSystem.AddEvent("Campfire");
                    Plugin.Log?.LogInfo($"[WARMTH_LOG] source=CampfireRefuel | item=Campfire | warmth=REFILL_MAX | dist={dist:F1}m | result=SUCCESS");
                }
                else
                {
                    Plugin.Log?.LogInfo($"[WARMTH_LOG] source=CampfireRefuel | item=Campfire | warmth=0 | dist={dist:F1}m | result=FAILED(no lantern)");
                }
            }
        }
    }

    /// <summary>
    /// 篝火近距离保护工具：通过 Harmony 补丁监听 Campfire.Awake/OnDestroy 维护活跃篝火列表，
    /// 避免每帧 FindObjectsByType。保留 10 秒降级兆底确保不遗漏。
    /// </summary>
    internal static class CampfireHelper
    {
        private static readonly List<Campfire> _activeCampfires = new List<Campfire>();
        private static float _lastFallbackTime = -999f;
        private const float FallbackInterval = 10f;

        /// <summary>注册篝火实例（由 CampfireAwakePatch 调用）。</summary>
        public static void Register(Campfire cf)
        {
            if (cf != null && !_activeCampfires.Contains(cf))
                _activeCampfires.Add(cf);
        }

        /// <summary>注销篝火实例（由 CampfireDestroyPatch 调用）。</summary>
        public static void Unregister(Campfire cf)
        {
            _activeCampfires.Remove(cf);
        }

        /// <summary>角色是否在任意已点燃篝火的保护半径内（使用游戏自身 moraleBoostRadius）。</summary>
        public static bool IsNearLitCampfire(Character character)
        {
            if (character == null) return false;

            // 清理已销毁的引用
            _activeCampfires.RemoveAll(c => c == null);

            // 降级兆底：每 10 秒通过 FindObjectsByType 确保列表完整
            if (Time.time - _lastFallbackTime >= FallbackInterval)
            {
                _lastFallbackTime = Time.time;
                var all = Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None);
                foreach (var cf in all)
                {
                    if (cf != null && !_activeCampfires.Contains(cf))
                        _activeCampfires.Add(cf);
                }
            }

            Vector3 charPos = character.Center;
            foreach (Campfire cf in _activeCampfires)
            {
                if (cf == null || !cf.Lit) continue;
                if (Vector3.Distance(charPos, cf.transform.position) <= cf.moraleBoostRadius)
                    return true;
            }
            return false;
        }
    }

    // ── 篝火生命周期监听：维护活跃列表 ─────────────────────

    [HarmonyPatch(typeof(Campfire), "Awake")]
    internal static class CampfireAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Campfire __instance)
        {
            CampfireHelper.Register(__instance);
        }
    }

    /// <summary>
    /// 篝火销毁监听：动态查找 OnDestroy / OnDisable，手动注册 Harmony 补丁。
    /// 若两者均不存在（游戏版本差异），依赖 CampfireHelper 中的 null 清理兜底。
    /// </summary>
    internal static class CampfireDestroyPatch
    {
        public static void Postfix(Campfire __instance)
        {
            CampfireHelper.Unregister(__instance);
        }

        /// <summary>尝试注册补丁，返回是否成功。</summary>
        public static bool TryApply(HarmonyLib.Harmony harmony)
        {
            var method = HarmonyLib.AccessTools.Method(typeof(Campfire), "OnDestroy")
                      ?? HarmonyLib.AccessTools.Method(typeof(Campfire), "OnDisable");
            if (method == null)
            {
                Plugin.Log?.LogWarning("[DEBUG] CampfireDestroyPatch: no OnDestroy/OnDisable in Campfire, relying on null cleanup");
                return false;
            }
            var postfix = new HarmonyLib.HarmonyMethod(typeof(CampfireDestroyPatch).GetMethod("Postfix"));
            harmony.Patch(method, postfix: postfix);
            Plugin.Log?.LogInfo($"[DEBUG] Patch OK: CampfireDestroyPatch → {method.Name}");
            return true;
        }
    }
}
