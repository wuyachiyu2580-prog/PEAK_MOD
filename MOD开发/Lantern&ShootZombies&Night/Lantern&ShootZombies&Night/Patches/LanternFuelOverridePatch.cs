using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// 覆盖灯笼燃料上限，支持 30/60/90/120/240 秒以及无限模式。
    /// 使用 Prefix（最高优先级）+ Postfix 的双重策略，确保优先级高于 BlackPeakRemix：
    ///   Prefix:  在 BPR 读取燃料之前注入安全值，防止 BPR 误判熄灭
    ///   Postfix: 在 BPR 修改之后覆盖为我们的正确值
    /// </summary>
    [HarmonyPatch(typeof(Lantern))]
    internal static class LanternFuelOverridePatch
    {
        // Per-lantern fuel tracking (key = instance ID)
        private static readonly Dictionary<int, float> _trackedFuel = new Dictionary<int, float>();

        // Per-lantern: save fuel before game's UpdateFuel runs (for GameDefault drain compensation)
        private static float _prefixSavedFuel = -1f;

        // Per-lantern: track whether we need to re-light after external fuel addition
        private static readonly HashSet<int> _needsRelight = new HashSet<int>();

        // ── [LitSync] 诊断：记录每盏灯上次 lit 值，检测变化事件 ──
        private static readonly Dictionary<int, bool> _lastKnownLit = new Dictionary<int, bool>();

        /// <summary>
        /// 场景切换时清理所有与旧实例 ID 绑定的缓存，避免跨局累积。
        /// </summary>
        public static void ResetAccumulatedState()
        {
            _trackedFuel.Clear();
            _needsRelight.Clear();
            _lastKnownLit.Clear();
            _prefixSavedFuel = -1f;
            _wasCampfireProtecting = false;
        }

        private static bool HasLocalFuelAuthority(Lantern lantern)
        {
            if (lantern == null || lantern.photonView == null) return false;
            if (lantern.photonView.IsMine) return true;
            Item item = lantern.GetComponent<Item>();
            return LanternHelper.IsLocalPlayerLantern(item);
        }

        private static bool HasPrimaryLocalFuelAuthority(Lantern lantern)
        {
            if (!HasLocalFuelAuthority(lantern)) return false;
            Item item = lantern.GetComponent<Item>();
            return LanternHelper.IsPrimaryLocalLantern(item);
        }

        // ── Awake: override startingFuel before OnInstanceDataSet ──

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        public static void AwakePostfix(Lantern __instance)
        {
            // 特殊灯笼（如治疗灯 Lantern_Faerie）不受 MOD 管理
            if (LanternHelper.IsSpecialLantern(__instance)) return;

            int val = (int)Plugin.LanternMaxFuel.Value;
            if (val == 0) return;

            float newMax = val > 0 ? (float)val : 99999f;

            // 升级系统：应用容量加成
            if (Plugin.EnableUpgradeSystem != null && Plugin.EnableUpgradeSystem.Value
                && LanternUpgradeSystem.CapacityLevel > 0)
            {
                float baseFuel = newMax;
                newMax *= LanternUpgradeSystem.CapacityMultiplier;
                Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] Upgrade capacity bonus: base={baseFuel:F0} ×{LanternUpgradeSystem.CapacityMultiplier:F1} = {newMax:F0}");
            }

            __instance.startingFuel = newMax;
            Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] startingFuel \u2192 {newMax}");
        }

        // ── OnInstanceDataSet: initialize per-lantern tracking ──

        [HarmonyPostfix]
        [HarmonyPatch("OnInstanceDataSet")]
        public static void OnInstanceDataSetPostfix(Lantern __instance)
        {
            if (LanternHelper.IsSpecialLantern(__instance)) return;

            // [LitSync] 通道 2：SetItemInstanceDataRPC → OnInstanceDataSet 路径
            // 记录 FlareActive 同步值，与原版 Lantern.Update 的 VFX 切换配对
            bool flareActive = __instance.HasData(DataEntryKey.FlareActive)
                && __instance.GetData<BoolItemData>(DataEntryKey.FlareActive).Value;
            int viewId2 = __instance.photonView != null ? __instance.photonView.ViewID : -1;
            bool isMine2 = __instance.photonView != null && __instance.photonView.IsMine;
            Plugin.Log?.LogInfo($"[DEBUG] [LitSync] OnInstanceDataSet: viewId={viewId2}, FlareActive={flareActive}, lit={ReflectionCache.GetLit(__instance)}, isMine={isMine2}");

            int val = (int)Plugin.LanternMaxFuel.Value;
            if (val == 0) return;

            int id = __instance.GetInstanceID();
            float max = __instance.startingFuel;
            bool hasLocalFuelAuthority = HasLocalFuelAuthority(__instance);

            float dataFuel = max;
            if (__instance.HasData(DataEntryKey.Fuel))
                dataFuel = __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value;

            if (hasLocalFuelAuthority &&
                _trackedFuel.TryGetValue(id, out float tracked) &&
                dataFuel < tracked)
            {
                dataFuel = tracked;
                ReflectionCache.SetFuel(__instance, tracked);
                if (__instance.HasData(DataEntryKey.Fuel))
                    __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = tracked;
            }
            else
            {
                _trackedFuel[id] = Mathf.Clamp(dataFuel, 0f, max);
            }

            Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] OnInstanceDataSet: id={id}, dataFuel={dataFuel:F1}, tracked={_trackedFuel[id]:F1}, max={max:F1}");

            Item item = __instance.GetComponent<Item>();
            if (item != null)
                item.SetUseRemainingPercentage(_trackedFuel[id] / max);
        }

        // ── [LitSync] 通道 1：LightLanternRPC Postfix——记录每次 RPC 本地生效 ──
        // owner 发送和 remote 接收都会走这里，便于双端日志对账确认 lit 同步成功否
        [HarmonyPostfix]
        [HarmonyPatch("LightLanternRPC")]
        public static void LightLanternRPCPostfix(Lantern __instance, bool litValue)
        {
            if (LanternHelper.IsSpecialLantern(__instance)) return;
            int viewId = __instance.photonView != null ? __instance.photonView.ViewID : -1;
            bool isMine = __instance.photonView != null && __instance.photonView.IsMine;
            bool lightGOActive = __instance.lanternLight != null
                && __instance.lanternLight.gameObject.activeSelf;
            Plugin.Log?.LogInfo($"[DEBUG] [LitSync] LightLanternRPC RECEIVED: viewId={viewId}, lit={litValue}, isMine={isMine}, lightGO.active={lightGOActive}");
        }

        // ── UpdateFuel Prefix (highest priority): feed safe value to BPR ──
        // BPR 的 Prefix 会读取 GetData<FloatItemData>(Fuel).Value，
        // 我们先注入安全值，防止 BPR 误判为 0 而熄灭灯笼。
        [HarmonyPrefix]
        [HarmonyPatch("UpdateFuel")]
        [HarmonyPriority(Priority.First)]
        public static void UpdateFuelPrefix(Lantern __instance)
        {
            // Save fuel for GameDefault drain multiplier compensation
            _prefixSavedFuel = -1f;
            if (LanternHelper.IsSpecialLantern(__instance)) return;

            int val = (int)Plugin.LanternMaxFuel.Value;
            bool hasLocalFuelAuthority = HasLocalFuelAuthority(__instance);
            if (val == 0 && hasLocalFuelAuthority)
            {
                _prefixSavedFuel = ReflectionCache.GetFuel(__instance);
            }
            if (val == 0) return;

            // 非拥有者灯笼（如客机看到的主机灯笼）由 Photon 网络同步管理，
            // 不应覆盖其 fuel 值。否则初始化时 tracked=0 会被写入 fuel 字段，
            // 导致 BPR 误判灯笼无燃料而熄灭灯光。
            if (!hasLocalFuelAuthority) return;

            int id = __instance.GetInstanceID();
            float max = __instance.startingFuel;

            // Pick up external fuel additions (warmth restore patches add fuel between frames)
            if (_trackedFuel.ContainsKey(id) && __instance.HasData(DataEntryKey.Fuel))
            {
                float dataFuel = __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value;
                if (dataFuel > _trackedFuel[id])
                {
                    float before = _trackedFuel[id];
                    // Mark for re-light if fuel went from ~0 to positive
                    if (before < 0.1f && dataFuel > 0f)
                        _needsRelight.Add(id);
                    _trackedFuel[id] = Mathf.Min(dataFuel, max);
                    Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] Prefix: external fuel detected! {before:F1} → {_trackedFuel[id]:F1} (data={dataFuel:F1})");
                }
            }

            float safe = val == -1 ? max
                       : (_trackedFuel.ContainsKey(id) ? _trackedFuel[id] : max);

            // Write to fuel field & data BEFORE BPR reads it
            ReflectionCache.SetFuel(__instance, safe);
            if (__instance.HasData(DataEntryKey.Fuel))
                __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = safe;
        }

        // ── UpdateFuel Postfix: overwrite BPR result with our correct values ──
        // 在所有 Prefix（含 BPR）执行完毕后，用我们的数据覆盖。

        // Debug throttle for reserve consumption
        private static float _reserveLogTimer;
        private static float _lastLoggedReserve = -1f;
        // Debug throttle for campfire protection
        private static bool _wasCampfireProtecting;

        [HarmonyPostfix]
        [HarmonyPatch("UpdateFuel")]
        public static void UpdateFuelPostfix(Lantern __instance)
        {
            if (LanternHelper.IsSpecialLantern(__instance)) return;

            // ── 统一读取 lit 状态（避免同一 Postfix 链中多次反射）──
            bool lit = ReflectionCache.GetLit(__instance);

            // [LitSync] 通道 1：LightLanternRPC → this.lit 路径
            // 每帧 Postfix 对比 lit 变化（owner+remote 都打），配对 VFX 切换时刻
            int idSync = __instance.GetInstanceID();
            if (!_lastKnownLit.TryGetValue(idSync, out bool prevLit) || prevLit != lit)
            {
                _lastKnownLit[idSync] = lit;
                int viewIdSync = __instance.photonView != null ? __instance.photonView.ViewID : -1;
                bool isMineSync = __instance.photonView != null && __instance.photonView.IsMine;
                bool lanternLightActive = __instance.lanternLight != null
                    && __instance.lanternLight.gameObject.activeSelf;
                Plugin.Log?.LogInfo($"[DEBUG] [LitSync] lit CHANGED: viewId={viewIdSync}, {prevLit}→{lit}, isMine={isMineSync}, lightGO.active={lanternLightActive}");
            }

            bool hasAnyLocalFuelAuthority = HasLocalFuelAuthority(__instance);
            bool hasPrimaryLocalFuelAuthority = hasAnyLocalFuelAuthority && HasPrimaryLocalFuelAuthority(__instance);

            // ---- Reserve consumption for GameDefault mode ----
            ConsumeReserveForLantern(__instance, lit, hasPrimaryLocalFuelAuthority);

            // ---- Campfire protection for GameDefault mode ----
            CompensateCampfireProtection(__instance, lit, hasPrimaryLocalFuelAuthority);

            // ---- Drain multiplier compensation for GameDefault mode ----
            CompensateDrainMultiplier(__instance, hasPrimaryLocalFuelAuthority);

            // ---- Periodic debug summary (all modes) ----
            LogReservePeriodic(__instance, lit, hasPrimaryLocalFuelAuthority);

            int val = (int)Plugin.LanternMaxFuel.Value;
            if (val == 0)
            {
                return;
            }

            int id = __instance.GetInstanceID();
            float max = __instance.startingFuel;

            // ── Infinite mode ──
            if (val == -1)
            {
                if (hasAnyLocalFuelAuthority)
                {
                    ReflectionCache.SetFuel(__instance, max);
                    if (__instance.HasData(DataEntryKey.Fuel))
                        __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = max;
                }
                _trackedFuel[id] = max;
                Item infItem = __instance.GetComponent<Item>();
                if (infItem != null) infItem.SetUseRemainingPercentage(1f);
                return;
            }

            // ── Specific value mode (30/60/90/120/240) ──
            // Only owner manages fuel; non-owners read synced data
            bool hasLocalFuelAuthority = hasPrimaryLocalFuelAuthority;
            if (!hasLocalFuelAuthority)
            {
                if (hasAnyLocalFuelAuthority)
                {
                    if (!_trackedFuel.ContainsKey(id))
                    {
                        float currentFuel = __instance.HasData(DataEntryKey.Fuel)
                            ? __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value
                            : ReflectionCache.GetFuel(__instance);
                        _trackedFuel[id] = Mathf.Clamp(currentFuel, 0f, max);
                    }

                    float localFuel = Mathf.Clamp(_trackedFuel[id], 0f, max);
                    ReflectionCache.SetFuel(__instance, localFuel);
                    if (__instance.HasData(DataEntryKey.Fuel))
                        __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = localFuel;
                    Item localItem = __instance.GetComponent<Item>();
                    if (localItem != null) localItem.SetUseRemainingPercentage(localFuel / max);
                    return;
                }

                // Non-owner: just fix percentage display with our startingFuel
                if (__instance.HasData(DataEntryKey.Fuel))
                {
                    float syncFuel = __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value;
                    Item syncItem = __instance.GetComponent<Item>();
                    if (syncItem != null) syncItem.SetUseRemainingPercentage(syncFuel / max);
                }
                return;
            }

            // Owner: apply 1:1 consumption rate (1 second fuel per real second)
            // lit 已在 Postfix 入口统一读取，此处直接使用

            if (!_trackedFuel.ContainsKey(id))
                _trackedFuel[id] = max;

            // ---- Auto re-light: if fuel was added while snuffed, re-light (owner-only) ----
            if (!lit && _trackedFuel[id] > 0f && _needsRelight.Contains(id))
            {
                _needsRelight.Remove(id);
                // 重新读取 lit 状态，防止网络延迟导致的重复点火 RPC
                bool freshLit = ReflectionCache.GetLit(__instance);
                if (!freshLit)
                {
                    int viewIdRe = __instance.photonView != null ? __instance.photonView.ViewID : -1;
                    Plugin.Log?.LogInfo($"[DEBUG] [LitSync] SEND LightLanternRPC(true) via AUTO-RE-LIT: viewId={viewIdRe}");
                    __instance.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { true });
                    lit = true;
                    Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] Postfix: AUTO RE-LIT lantern! (fuel={_trackedFuel[id]:F1})");
                }
                else
                {
                    lit = true; // 已被其他 RPC 点燃
                }
            }
            else if (lit)
            {
                _needsRelight.Remove(id); // Clear flag if already lit
            }

            if (lit)
            {
                float drain = LanternHelper.FuelDrainMultiplier;
                float burn = Time.deltaTime * drain;
                // 篝火保护：在篝火附近时不消耗燃料
                if (IsLanternHolderNearCampfire(__instance))
                {
                    burn = 0f;
                }
                else
                {
                    // 备用消耗已在 ConsumeReserveForLantern 中统一处理
                    // 自定义模式下，备用消耗直接减少 burn
                    float fromReserve = LanternHelper.ConsumeReserve(burn);
                    burn -= fromReserve;
                }
                _trackedFuel[id] -= burn;
                if (_trackedFuel[id] <= 0f)
                {
                    _trackedFuel[id] = 0f;
                    if (__instance.photonView != null && __instance.photonView.IsMine)
                        __instance.SnuffLantern();
                    else if (__instance.photonView != null)
                        __instance.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { false });
                    lit = false;
                }
            }
            else
            {
                // ── AutoRefill：熄灭后自动回燃料至上限比例 ──
                TryAutoRefill(__instance, id, max);
            }

            float fuel = _trackedFuel[id];
            ReflectionCache.SetFuel(__instance, fuel);
            if (__instance.HasData(DataEntryKey.Fuel))
                __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = fuel;
            Item ownerItem = __instance.GetComponent<Item>();
            if (ownerItem != null) ownerItem.SetUseRemainingPercentage(fuel / max);
        }

        // ── AutoRefill 节流日志 ──
        private static readonly Dictionary<int, float> _autoRefillLogTimer = new Dictionary<int, float>();

        /// <summary>
        /// 熄灭的灯自动回燃料至 max * AutoRefillCapPercent；
        /// 可选仅白天 / 仅被玩家持有时生效。仅 Owner 调用。
        /// </summary>
        private static void TryAutoRefill(Lantern __instance, int id, float max)
        {
            if (Plugin.AutoRefillEnabled == null || !Plugin.AutoRefillEnabled.Value) return;
            if (!_trackedFuel.ContainsKey(id)) return;

            float cap = max * Mathf.Clamp01(Plugin.AutoRefillCapPercent.Value);
            float cur = _trackedFuel[id];
            if (cur >= cap) return;

            // 白天/持有门控
            bool dayOk = !Plugin.AutoRefillDaytimeOnly.Value || DayNightTracker.IsDaytime;
            if (!dayOk) return;

            bool holderOk = true;
            if (Plugin.AutoRefillRequireHold.Value)
            {
                Item item = __instance.GetComponent<Item>();
                holderOk = item != null && item.holderCharacter != null;
            }
            if (!holderOk) return;

            float add = Mathf.Max(0f, Plugin.AutoRefillRate.Value) * Time.deltaTime;
            float next = Mathf.Min(cur + add, cap);
            _trackedFuel[id] = next;

            if (next > cur) RestoreTracker.ReportAutoRefillActive();

            // 每盏灯 5 秒打一次进度，避免刷屏
            if (!_autoRefillLogTimer.TryGetValue(id, out float t)) t = 0f;
            t += Time.deltaTime;
            if (t >= 5f)
            {
                _autoRefillLogTimer[id] = 0f;
                Plugin.Log?.LogInfo($"[DEBUG] [AutoRefill] id={id}, {cur:F1} → {next:F1} / cap={cap:F1} (rate={Plugin.AutoRefillRate.Value:F2}/s)");
            }
            else
            {
                _autoRefillLogTimer[id] = t;
            }
        }

        /// <summary>
        /// 备用暖值消耗：游戏默认模式下（val==0），游戏已扣减燃料，我们从备用池补回。
        /// 自定义模式下，由 PostFix 主逻辑处理，这里不做额外操作。
        /// </summary>
        private static void ConsumeReserveForLantern(Lantern __instance, bool lit, bool hasPrimaryLocalFuelAuthority)
        {
            int val = (int)Plugin.LanternMaxFuel.Value;
            // 自定义模式在主逻辑中处理备用消耗
            if (val != 0) return;

            if (!hasPrimaryLocalFuelAuthority) return;

            // GameDefault 模式下的自动重灯：如果灯灭了但燃料>0，重新点灯
            if (!lit && __instance.HasData(DataEntryKey.Fuel))
            {
                float fuel = __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value;
                if (fuel > 0.1f)
                {
                    __instance.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { true });
                    Plugin.Log?.LogInfo($"[DEBUG] [LanternFuel] GameDefault: AUTO RE-LIT (fuel={fuel:F1})");
                    lit = true;
                }
            }

            if (LanternHelper.ReserveWarmth <= 0f) return;
            if (!lit) return;

            float consumed = LanternHelper.ConsumeReserve(Time.deltaTime);
            if (consumed > 0f)
            {
                // 游戏已扣减了 deltaTime，我们从备用池补回
                float currentFuel = ReflectionCache.GetFuel(__instance);
                float newFuel = currentFuel + consumed;
                ReflectionCache.SetFuel(__instance, newFuel);
                if (__instance.HasData(DataEntryKey.Fuel))
                    __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = newFuel;
            }
        }

        /// <summary>
        /// 篝火保护（GameDefault模式）：游戏已扣减燃料，在篝火附近时补回消耗量。
        /// </summary>
        private static void CompensateCampfireProtection(Lantern __instance, bool lit, bool hasPrimaryLocalFuelAuthority)
        {
            int val = (int)Plugin.LanternMaxFuel.Value;
            if (val != 0) return; // Only for GameDefault mode
            if (!hasPrimaryLocalFuelAuthority) return;

            if (!lit) return;
            if (!IsLanternHolderNearCampfire(__instance)) return;
        
            // Game already consumed deltaTime of fuel; add it back
            float compensate = Time.deltaTime;
            float currentFuel = ReflectionCache.GetFuel(__instance);
            float maxFuel = __instance.startingFuel;
            float newFuel = Mathf.Min(currentFuel + compensate, maxFuel);
            ReflectionCache.SetFuel(__instance, newFuel);
            if (__instance.HasData(DataEntryKey.Fuel))
                __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = newFuel;
        
            // 仅记录进入/离开篡火保护范围的状态变化
            if (!_wasCampfireProtecting)
            {
                _wasCampfireProtecting = true;
                Plugin.Log?.LogInfo($"[DEBUG] [CampfireProtect] GameDefault: campfire protection STARTED (fuel={newFuel:F1}/{maxFuel:F1})");
            }
        }

        /// <summary>
        /// 消耗速度倍率补偿（GameDefault模式）：游戏已按原始速率扣燃料，
        /// 根据 FuelDrainMultiplier 补回多扣或加扣差额。
        /// drain=0.5 → 游戏扣了1.0，应只扣0.5，补回0.5
        /// drain=2.0 → 游戏扣了1.0，应扣2.0，再扣1.0
        /// </summary>
        private static float _drainLogTimer = 30f;

        private static void CompensateDrainMultiplier(Lantern __instance, bool hasPrimaryLocalFuelAuthority)
        {
            int val = (int)Plugin.LanternMaxFuel.Value;
            if (val != 0) return;
            if (!hasPrimaryLocalFuelAuthority) return;
            if (_prefixSavedFuel < 0f) return;

            float drain = LanternHelper.FuelDrainMultiplier;
            if (Mathf.Approximately(drain, 1f)) return;

            float currentFuel = ReflectionCache.GetFuel(__instance);
            float gameConsumed = _prefixSavedFuel - currentFuel;
            if (gameConsumed <= 0f) return;

            float desiredConsumed = gameConsumed * drain;
            float adjustment = gameConsumed - desiredConsumed;
            float newFuel = Mathf.Max(currentFuel + adjustment, 0f);
            float maxFuel = __instance.startingFuel;
            newFuel = Mathf.Min(newFuel, maxFuel);

            ReflectionCache.SetFuel(__instance, newFuel);
            if (__instance.HasData(DataEntryKey.Fuel))
                __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = newFuel;

            _drainLogTimer += Time.deltaTime;
            if (_drainLogTimer >= 30f)
            {
                _drainLogTimer = 0f;
                Plugin.Log?.LogInfo($"[DEBUG] [FuelDrain] GameDefault: drain={drain:F2}x, consumed {gameConsumed:F4} → {desiredConsumed:F4}, fuel={newFuel:F1}/{maxFuel:F1}");
            }
        }

        /// <summary>
        /// 检查灯笼持有者是否在篝火保护范围内。
        /// </summary>
        private static bool IsLanternHolderNearCampfire(Lantern lantern)
        {
            if (!Plugin.EnableCampfireRefuel.Value) return false;
            Item item = lantern.GetComponent<Item>();
            if (item == null) return false;

            // 优先用 holderCharacter；背包内灯笼的世界实例 holderCharacter 可能为 null，
            // 此时回退到 Character.localCharacter（因为已在上层确认 photonView.IsMine）
            Character holder = item.holderCharacter;
            if (holder == null)
                holder = Character.localCharacter;
            if (holder == null || !holder.IsLocal) return false;

            bool near = CampfireHelper.IsNearLitCampfire(holder);
            // 篝火保护状态变化时日志
            if (!near && _wasCampfireProtecting)
            {
                _wasCampfireProtecting = false;
                Plugin.Log?.LogInfo("[DEBUG] [CampfireProtect] Campfire protection ENDED (left range)");
            }
            return near;
        }

        /// <summary>
        /// 节流日志：每10秒输出一次备用暖值与灯状态摘要，适用于所有模式。
        /// </summary>
        private static void LogReservePeriodic(Lantern __instance, bool lit, bool hasPrimaryLocalFuelAuthority)
        {
            if (!hasPrimaryLocalFuelAuthority) return;

            _reserveLogTimer += Time.deltaTime;
            if (_reserveLogTimer < 10f) return;
            _reserveLogTimer = 0f;

            float curReserve = LanternHelper.ReserveWarmth;
            int val = (int)Plugin.LanternMaxFuel.Value;
            float fuel = ReflectionCache.GetFuel(__instance);
            float maxReserve = LanternHelper.GetReserveMax(__instance.startingFuel);

            // 仅当备用值>0 或 备用值刚清零时输出
            if (curReserve > 0f || _lastLoggedReserve > 0f)
            {
                _lastLoggedReserve = curReserve;
                Plugin.Log?.LogInfo($"[DEBUG] [Reserve] status: reserve={curReserve:F1}/{maxReserve:F1}, fuel={fuel:F1}, lit={lit}, mode={(val == 0 ? "GameDefault" : val.ToString())}");
            }
        }
    }
}
