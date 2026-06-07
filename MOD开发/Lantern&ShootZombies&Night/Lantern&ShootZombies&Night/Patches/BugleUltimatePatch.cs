using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// 号角大招：吹号角触发 RPC_StartToot 时，
    /// 半径内所有活玩家的灯一次性回满（溢出进备用池）。
    ///
    /// 设计要点：
    /// - 全局 CD 走 Time.time 时间戳（每端独立累积），不跟号角物品绑定
    /// - RPC_StartToot 会在所有客户端同步触发 Postfix，每端独立做
    ///   "自己到号角距离 &lt;= 半径" 判断 → 给 localCharacter 的灯回满
    /// - 仙子灯（Faerie）不回，保持特殊灯笼规则
    /// </summary>
    [HarmonyPatch(typeof(BugleSFX), "RPC_StartToot")]
    internal static class BugleUltimatePatch
    {
        // 全局时间戳：上次大招触发的 Time.time（本端累积）
        private static float _lastUltTime = -999f;

        [HarmonyPostfix]
        public static void Postfix(BugleSFX __instance)
        {
            if (__instance == null) return;
            if (Plugin.BugleUltimateEnabled == null || !Plugin.BugleUltimateEnabled.Value) return;

            // ── 全局 CD ──
            float cd = Plugin.BugleUltimateCooldown.Value;
            if (Time.time - _lastUltTime < cd)
            {
                float remain = cd - (Time.time - _lastUltTime);
                Plugin.Log?.LogInfo($"[DEBUG] [BugleUlt] CD not ready: remain={remain:F1}s / {cd:F0}s");
                return;
            }
            _lastUltTime = Time.time;

            Character local = Character.localCharacter;
            if (local == null) return;

            // ── 距离判定（本端独立）──
            Vector3 bugleCenter = __instance.transform.position;
            float dist = Vector3.Distance(local.Center, bugleCenter);
            float radius = Plugin.BugleUltimateRadius.Value;
            if (dist > radius)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [BugleUlt] out of range: dist={dist:F1} > radius={radius:F1}");
                return;
            }

            // ── 给 localCharacter 身上所有灯回满 ──
            int refilled = RefillAllLocalLanterns(local, out float totalOverflow, out float lanternMaxAny);
            if (refilled == 0)
            {
                Plugin.Log?.LogInfo("[DEBUG] [BugleUlt] no lanterns to refill");
                return;
            }

            // 溢出进备用池
            if (totalOverflow > 0f && lanternMaxAny > 0f)
                LanternHelper.AddOverflowToReserve(totalOverflow, lanternMaxAny);

            RestoreTracker.ReportLast("Bugle", totalOverflow);
            LanternUpgradeSystem.AddEvent("Bugle");
            Plugin.Log?.LogInfo($"[WARMTH_LOG] source=BugleUlt | count={refilled} | overflow={totalOverflow:F1} | dist={dist:F1}m | cd={cd:F0}s");
        }

        /// <summary>
        /// 给 character 身上所有非仙子灯一次性回满。
        /// 返回处理数量，同时累加溢出值。BugleUltimateRestore &lt; 0 = RefillMax。
        /// </summary>
        private static int RefillAllLocalLanterns(Character character, out float totalOverflow, out float lanternMaxAny)
        {
            totalOverflow = 0f;
            lanternMaxAny = 0f;
            if (character == null || !character.IsLocal || character.player == null) return 0;

            float configRestore = Plugin.BugleUltimateRestore.Value;
            bool refillToMax = configRestore < 0f;

            int count = 0;

            // 1. 手持槽
            if (character.player.itemSlots != null)
            {
                foreach (var slot in character.player.itemSlots)
                {
                    if (TryProcessSlot(slot, refillToMax, configRestore, false, ref totalOverflow, ref lanternMaxAny))
                        count++;
                }
            }

            // 2. tempFullSlot（3槽满时创建的临时槽）
            if (TryProcessSlot(character.player.tempFullSlot, refillToMax, configRestore, false, ref totalOverflow, ref lanternMaxAny))
                count++;

            // 3. 背包——背包里的灯只回燃料，不点亮（游戏设计上背包内灯必需燄灭）
            BackpackSlot backpackSlot = character.player.backpackSlot;
            if (backpackSlot != null && backpackSlot.hasBackpack && backpackSlot.data != null
                && backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out var backpackData)
                && backpackData != null && backpackData.itemSlots != null)
            {
                foreach (var slot in backpackData.itemSlots)
                {
                    if (TryProcessSlot(slot, refillToMax, configRestore, true, ref totalOverflow, ref lanternMaxAny))
                        count++;
                }
            }

            return count;
        }

        private static bool TryProcessSlot(ItemSlot slot, bool refillToMax, float configRestore, bool isInBackpack,
            ref float totalOverflow, ref float lanternMaxAny)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null || slot.data == null) return false;
            if (slot.prefab.name != "Lantern") return false;

            Item worldItem = LanternHelper.FindWorldItemByGuid(slot.data.guid);
            if (worldItem == null) return false;

            Lantern lantern = worldItem.GetComponent<Lantern>();
            if (lantern == null) return false;
            if (LanternHelper.IsSpecialLantern(lantern)) return false;

            if (!worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out FloatItemData fuelData)) return false;

            float max = lantern.startingFuel;
            if (max <= 0f) return false;

            float current = fuelData.Value;
            float delta = refillToMax ? (max - current) : configRestore;
            if (delta <= 0f) return false;

            float raw = current + delta;
            float newFuel = Mathf.Min(raw, max);
            fuelData.Value = newFuel;

            float overflow = raw > max ? raw - max : 0f;
            totalOverflow += overflow;
            lanternMaxAny = max;

            // 同步给其他客户端
            if (worldItem.photonView != null)
                worldItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others, new object[] { worldItem.data });

            // 原来灭了就顺便点亮（仅 owner 发 RPC；背包里的灯跳过，避免状态异常）
            bool wasLit = ReflectionCache.GetLit(lantern);
            if (!wasLit && newFuel > 0.1f && lantern.photonView != null && lantern.photonView.IsMine)
            {
                if (isInBackpack)
                {
                    Plugin.Log?.LogInfo($"[DEBUG] [BugleUlt] Skipped re-lit (in backpack, viewId={lantern.photonView.ViewID})");
                }
                else
                {
                    lantern.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { true });
                    Plugin.Log?.LogInfo($"[DEBUG] [BugleUlt] Re-lit lantern (viewId={lantern.photonView.ViewID})");
                }
            }

            Plugin.Log?.LogInfo($"[DEBUG] [BugleUlt] Refilled: {current:F1} → {newFuel:F1} / max={max:F1}, overflow={overflow:F1}, backpack={isInBackpack}");
            return true;
        }
    }
}
