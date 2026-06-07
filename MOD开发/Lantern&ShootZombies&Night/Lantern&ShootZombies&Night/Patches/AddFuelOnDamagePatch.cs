using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// 当飞镖击中僵尸时，僵尸范围内的本地玩家获得灯暖值。
    /// Hook Action_RaycastDart.DartImpact (本地碰撞) 和 RPC_DartImpact (网络同步)，
    /// 每个客户端独立判断自己是否在范围内，天然支持多人。
    /// </summary>
    public static class ZombieHitFuelHelper
    {
        private static float _lastFuelAddTime = -1f;

        public static void TryAddFuelOnHit(Character hitCharacter, string source)
        {
            if (hitCharacter == null) return;
            if (!hitCharacter.isZombie && !hitCharacter.isBot) return;

            // Master switch
            if (!Plugin.EnableWarmthRestore.Value) return;

            // Configurable cooldown to prevent duplicates (DartImpact + RPC may both fire)
            float cooldown = Plugin.HitRestoreCooldown.Value;
            if (Time.time - _lastFuelAddTime < cooldown) return;

            Character localChar = Character.localCharacter;
            if (localChar == null) return;

            // 使用 Center（Torso 位置）而非 transform.position（根 transform 在 PEAK 中不随移动更新）
            // 僵尸侧同样用 Center，保持两端一致
            float distance = Vector3.Distance(localChar.Center, hitCharacter.Center);
            float radius = Plugin.RestoreRadius.Value;

            if (distance > radius)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [HitRestore] {source}: zombie hit but out of range (dist={distance:F1}, radius={radius:F1})");
                return;
            }

            float warmthToAdd = Plugin.HitRestoreWarmth.Value;
            if (warmthToAdd <= 0f) return;

            // Hit is exempt from variety check (same as bugle)
            // 任意状态：先灵灯本体（包含关灯/背包的灯），溢出再入备用池——避免被备用池小上限截流
            if (LanternHelper.AddPlayerLanternFuelAnyState(localChar, warmthToAdd, out float overflow, out float lanternMax))
            {
                _lastFuelAddTime = Time.time;
                RestoreTracker.ReportLast("Hit", warmthToAdd);
                LanternUpgradeSystem.AddEvent("Hit");
                float storedInLantern = warmthToAdd - overflow;
                Plugin.Log?.LogInfo($"[WARMTH_LOG] source=HitRestore | item=ZombieHit({source}) | warmth=+{warmthToAdd:F1}s | lantern+{storedInLantern:F1}s | overflow={overflow:F1}s | dist={distance:F1}m | result=SUCCESS");

                // 溢出部分进备用池
                if (overflow > 0f)
                    LanternHelper.AddOverflowToReserve(overflow, lanternMax);
            }
            else
            {
                // 玩家根本没灯 → 暂存备用池，等将来荥灯时再消费
                int maxOpt = (int)Plugin.LanternMaxFuel.Value;
                float fallbackMax = (maxOpt > 0) ? maxOpt : 300f; // GameDefault / Infinite 用 300
                LanternHelper.AddOverflowToReserve(warmthToAdd, fallbackMax);
                _lastFuelAddTime = Time.time;
                RestoreTracker.ReportLast("Hit", warmthToAdd);
                LanternUpgradeSystem.AddEvent("Hit");
                Plugin.Log?.LogInfo($"[WARMTH_LOG] source=HitRestore | item=ZombieHit({source}) | warmth=+{warmthToAdd:F1}s | dist={distance:F1}m | result=RESERVED(no lantern, stored in reserve)");
            }
        }
    }

    /// <summary>
    /// Postfix on Action_RaycastDart.DartImpact — 仅在射击者客户端触发
    /// </summary>
    [HarmonyPatch]
    public static class LocalDartFuelPatch
    {
        public static MethodBase TargetMethod()
        {
            try
            {
                Type type = typeof(Item).Assembly.GetType("Action_RaycastDart");
                if (type == null) return null;
                return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m =>
                        string.Equals(m.Name, "DartImpact", StringComparison.Ordinal)
                        && m.GetParameters().Length == 3
                        && m.GetParameters()[0].ParameterType == typeof(Character)
                        && m.GetParameters()[1].ParameterType == typeof(Vector3)
                        && m.GetParameters()[2].ParameterType == typeof(Vector3));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[LocalDartFuelPatch] TargetMethod error: {ex}");
                return null;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(MonoBehaviour __instance, Character hitCharacter, Vector3 origin, Vector3 endpoint)
        {
            ZombieHitFuelHelper.TryAddFuelOnHit(hitCharacter, "DartImpact");
        }
    }

    /// <summary>
    /// Postfix on Action_RaycastDart.RPC_DartImpact — 所有客户端都会收到
    /// </summary>
    [HarmonyPatch]
    public static class RpcDartFuelPatch
    {
        public static MethodBase TargetMethod()
        {
            try
            {
                Type type = typeof(Item).Assembly.GetType("Action_RaycastDart");
                if (type == null) return null;
                return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m =>
                        string.Equals(m.Name, "RPC_DartImpact", StringComparison.Ordinal)
                        && (m.GetParameters().Length == 3 || m.GetParameters().Length == 4)
                        && m.GetParameters()[0].ParameterType == typeof(int)
                        && m.GetParameters()[1].ParameterType == typeof(Vector3)
                        && m.GetParameters()[2].ParameterType == typeof(Vector3));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[RpcDartFuelPatch] TargetMethod error: {ex}");
                return null;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(MonoBehaviour __instance, int characterID, Vector3 origin, Vector3 endpoint)
        {
            if (characterID <= 0) return;

            PhotonView pv = PhotonView.Find(characterID);
            if (pv == null) return;

            Character hitChar = pv.GetComponentInParent<Character>();
            ZombieHitFuelHelper.TryAddFuelOnHit(hitChar, "RPC_DartImpact");
        }
    }
}
