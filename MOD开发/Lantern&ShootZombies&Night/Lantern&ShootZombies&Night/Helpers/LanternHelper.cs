using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 灯笼查找（手持 + 临时槽 + 背包）与燃料增量操作。
    /// </summary>
    internal static class LanternHelper
    {
        // ── 灯笼查找日志节流（每 30 秒输出一次，避免刷屏）──
        private static float _lastFindLogTime = -999f;
        private const float FindLogInterval = 30f;

        // ── FindWorldItemByGuid 结果缓存（避免兵底 FindObjectsByType）──
        private static Guid _worldItemCachedGuid;
        private static Item _worldItemCachedResult;
        private static float _worldItemCacheTime;
        private const float WorldItemCacheDuration = 2f;

        // ── FindLitLanternSlot 帧级缓存（同帧多 Patch 调用只搜索一次）──
        private static int _findSlotCachedFrame = -1;
        private static ItemSlot _findSlotCachedResult;
        private static ItemInstanceData _findSlotCachedLiveData;

        // ── 宽限期缓存：BPR 模式切换期间 FlareActive/worldLit 同时无效时兆底 ──
        private struct SlotCacheEntry
        {
            public ItemSlot Slot;
            public ItemInstanceData LiveData;
            public float Expiry;
        }
        private static readonly Dictionary<int, SlotCacheEntry> _slotCacheById = new Dictionary<int, SlotCacheEntry>();
        private const float CacheGracePeriod = 0.8f;
        private static bool _cacheHitLogged;

        // ── 备用暖值 (Reserve Warmth) ──────────────────────────────
        private static float _reserveWarmth;

        // ── 燃料同步 RPC 节流 ──────────────────────────────
        private static readonly Dictionary<int, float> _fuelSyncTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _fuelSyncValue = new Dictionary<int, float>();
        private const float FuelSyncInterval = 2f;
        private const float FuelSyncThreshold = 0.05f; // 与上次同步值相差超过 5% 时才发送

        /// <summary>
        /// 场景切换时清理跨局累积的灯笼同步字典，避免旧实例 ID 残留。
        /// </summary>
        public static void ResetAccumulatedState()
        {
            _fuelSyncTime.Clear();
            _fuelSyncValue.Clear();
        }

        // ── 燃料消耗速度倍率（多源叠加）──────────────────────────────────
        /// <summary>
        /// 所有消耗倍率源。最终倍率 = 所有源的乘积。
        /// 例：flashlight=1.5, night_event=2.0 → 最终 3.0x。
        /// </summary>
        private static readonly Dictionary<string, float> _drainSources = new Dictionary<string, float>();

        /// <summary>
        /// 灯笼燃料消耗速度最终倍率（只读，由所有源相乘得出）。
        /// </summary>
        public static float FuelDrainMultiplier
        {
            get
            {
                if (_drainSources.Count == 0) return 1f;
                float result = 1f;
                foreach (var v in _drainSources.Values)
                    result *= v;
                return result;
            }
        }

        /// <summary>
        /// 注册/更新一个消耗倍率源。multiplier=1.0 时自动移除该源。
        /// </summary>
        public static void SetDrainSource(string key, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f))
            {
                if (_drainSources.Remove(key))
                    Plugin.Log?.LogInfo($"[DEBUG] [DrainSource] REMOVED '{key}' (≈1.0) → final={FuelDrainMultiplier:F2}x");
            }
            else
            {
                float old;
                bool existed = _drainSources.TryGetValue(key, out old);
                _drainSources[key] = multiplier;
                if (!existed || !Mathf.Approximately(old, multiplier))
                    Plugin.Log?.LogInfo($"[DEBUG] [DrainSource] SET '{key}'={(existed ? $"{old:F2}→" : "")}{multiplier:F2}x → final={FuelDrainMultiplier:F2}x");
            }
        }

        /// <summary>移除一个消耗倍率源。</summary>
        public static void RemoveDrainSource(string key)
        {
            if (_drainSources.Remove(key))
                Plugin.Log?.LogInfo($"[DEBUG] [DrainSource] REMOVED '{key}' → final={FuelDrainMultiplier:F2}x");
        }

        /// <summary>
        /// 判断灯笼是否为特殊变体（如 Lantern_Faerie 治疗灯）。
        /// 特殊灯笼不受 MOD 燃料上限、备用池、消耗倍率等管理。
        /// </summary>
        internal static bool IsSpecialLantern(Lantern instance)
        {
            if (instance == null) return false;
            string goName = instance.gameObject?.name;
            return goName != null && goName.Contains("Faerie");
        }

        internal static bool IsLocalPlayerLantern(Item item)
        {
            if (item == null || item.data == null) return false;
            Guid guid = item.data.guid;
            return IsLocalPlayerLanternGuid(guid);
        }

        internal static bool IsLocalPlayerLanternGuid(Guid guid)
        {
            Character local = Character.localCharacter;
            if (local == null || local.player == null) return false;
            return HasLanternGuid(local.player.itemSlots, guid)
                || HasLanternGuid(local.player.tempFullSlot, guid)
                || HasBackpackLanternGuid(local.player.backpackSlot, guid);
        }

        internal static bool IsPrimaryLocalLantern(Item item)
        {
            if (item == null || item.data == null) return false;
            Guid itemGuid = item.data.guid;
            if (itemGuid == Guid.Empty) return false;
            return TryGetPrimaryLocalLanternGuid(out Guid primaryGuid) && itemGuid == primaryGuid;
        }

        internal static bool TryGetPrimaryLocalLanternGuid(out Guid guid)
        {
            guid = Guid.Empty;

            Character local = Character.localCharacter;
            if (local == null || local.player == null) return false;

            ItemInstanceData liveData;
            ItemSlot litSlot = FindLitLanternSlot(local, out liveData);
            ItemInstanceData selected = liveData ?? litSlot?.data;
            if (selected != null && selected.guid != Guid.Empty)
            {
                guid = selected.guid;
                return true;
            }

            if (TryGetAnyLocalLanternData(local, out selected) && selected != null && selected.guid != Guid.Empty)
            {
                guid = selected.guid;
                return true;
            }

            return false;
        }

        private static bool HasLanternGuid(ItemSlot[] slots, Guid guid)
        {
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (HasLanternGuid(slots[i], guid)) return true;
            }
            return false;
        }

        private static bool HasLanternGuid(ItemSlot slot, Guid guid)
        {
            return slot != null
                && !slot.IsEmpty()
                && slot.prefab != null
                && slot.prefab.name == "Lantern"
                && slot.data != null
                && slot.data.guid == guid;
        }

        private static bool HasBackpackLanternGuid(BackpackSlot backpackSlot, Guid guid)
        {
            if (backpackSlot == null || !backpackSlot.hasBackpack || backpackSlot.data == null) return false;
            if (!backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out BackpackData bpData)) return false;
            return bpData != null && HasLanternGuid(bpData.itemSlots, guid);
        }

        internal static void TryRelightAfterFuelGain(Lantern lantern, string reason)
        {
            if (lantern == null || IsSpecialLantern(lantern)) return;
            if (ReflectionCache.GetLit(lantern)) return;

            Item item = lantern.GetComponent<Item>();
            if (item == null || item.data == null) return;
            if (item.data.TryGetDataEntry<BoolItemData>(DataEntryKey.FlareActive, out BoolItemData flareData))
                flareData.Value = true;

            if (lantern.photonView != null)
            {
                lantern.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { true });
                Plugin.Log?.LogInfo($"[DEBUG] [LitSync] SEND LightLanternRPC(true) via {reason}: viewId={lantern.photonView.ViewID}, isMine={lantern.photonView.IsMine}, localSlot={IsLocalPlayerLantern(item)}");
            }
        }

        /// <summary>当前备用暖值（只读）。</summary>
        public static float ReserveWarmth => _reserveWarmth;

        /// <summary>根据配置和灯笼最大值计算备用暖值上限。</summary>
        public static float GetReserveMax(float lanternMax)
        {
            int ratio = (int)Plugin.ReserveWarmthMax.Value;
            return lanternMax * ratio / 100f;
        }

        /// <summary>将溢出的暖值存入备用池。</summary>
        public static void AddOverflowToReserve(float overflow, float lanternMax)
        {
            if (overflow <= 0f) return;
            float max = GetReserveMax(lanternMax);
            if (max <= 0f) return;
            float before = _reserveWarmth;
            _reserveWarmth = Mathf.Min(_reserveWarmth + overflow, max);
            Plugin.Log?.LogInfo($"[DEBUG] [Reserve] +{overflow:F1} → {_reserveWarmth:F1}/{max:F1} (was {before:F1})");
        }

        /// <summary>从备用池消耗指定量，返回实际消耗量。</summary>
        public static float ConsumeReserve(float amount)
        {
            if (_reserveWarmth <= 0f || amount <= 0f) return 0f;
            float before = _reserveWarmth;
            float consumed = Mathf.Min(amount, _reserveWarmth);
            _reserveWarmth -= consumed;
            // 仅记录有意义的消耗（微小每帧消耗由 LogReservePeriodic 2秒摘要覆盖）
            if (consumed >= 0.1f)
                Plugin.Log?.LogInfo($"[DEBUG] [Reserve] -{consumed:F1} → {_reserveWarmth:F1} (was {before:F1})");
            return consumed;
        }

        // ── 燃料增量 ──────────────────────────────────────────────
        /// <summary>
        /// 增量模式：从世界实例读取当前燃料，加上 fuelDelta，clamp 后写入并同步。
        /// </summary>
        public static bool AddPlayerLanternFuel(Character character, float fuelDelta)
        {
            return AddPlayerLanternFuel(character, fuelDelta, out _, out _);
        }

        /// <summary>
        /// 增量模式（带溢出输出）：将 fuelDelta 加到灯笼，溢出部分通过 overflow 返回，灯笼最大值通过 maxFuelOut 返回。
        /// </summary>
        public static bool AddPlayerLanternFuel(Character character, float fuelDelta, out float overflow, out float maxFuelOut)
        {
            overflow = 0f;
            maxFuelOut = 0f;
            if (character == null || !character.IsLocal) return false;
            if (Mathf.Approximately(fuelDelta, 0f)) return false;

            ItemInstanceData liveData;
            ItemSlot litLanternSlot = FindLitLanternSlot(character, out liveData);
            if (litLanternSlot == null)
            {
                Plugin.Log?.LogInfo("[DEBUG] AddPlayerLanternFuel: no lit lantern slot found");
                return false;
            }

            ItemInstanceData dataForGuid = liveData ?? litLanternSlot.data;
            if (dataForGuid == null) return false;
            Guid targetGuid = dataForGuid.guid;
            if (targetGuid == Guid.Empty) return false;

            Item worldItem = FindWorldItemByGuid(targetGuid);
            if (worldItem == null)
            {
                Plugin.Log?.LogWarning($"[DEBUG] AddPlayerLanternFuel: world item not found for guid={targetGuid}");
                return false;
            }

            var lanternComponent = worldItem.GetComponent<Lantern>();
            if (lanternComponent == null)
            {
                Plugin.Log?.LogWarning("[DEBUG] AddPlayerLanternFuel: world item has no Lantern component");
                return false;
            }

            FloatItemData fuelData;
            if (!worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out fuelData))
            {
                Plugin.Log?.LogWarning("[DEBUG] AddPlayerLanternFuel: no Fuel data entry");
                return false;
            }

            float currentFuel = fuelData.Value;
            float maxFuel = lanternComponent.startingFuel;
            maxFuelOut = maxFuel;
            float rawNew = currentFuel + fuelDelta;
            float newFuel = Mathf.Clamp(rawNew, 0f, maxFuel);
            overflow = rawNew > maxFuel ? rawNew - maxFuel : 0f;
            fuelData.Value = newFuel;
            if (currentFuel <= 0.1f && newFuel > 0.1f)
                TryRelightAfterFuelGain(lanternComponent, "fuel-add");

            // 高精度调试：当灯快满或已溢出时录下真实值（定位备用池未入池的根因）
            if (rawNew > maxFuel - 1f)
            {
                Plugin.Log?.LogInfo($"[DEBUG] [FuelMath] currentFuel={currentFuel:F3}, delta={fuelDelta:F3}, rawNew={rawNew:F3}, maxFuel={maxFuel:F3}, overflow={overflow:F3}, lanternInstID={lanternComponent.GetInstanceID()}");
            }

            if (worldItem.photonView != null)
            {
                int syncId = worldItem.GetInstanceID();
                float lastTime, lastFuel = 0f;
                bool hasSync = _fuelSyncTime.TryGetValue(syncId, out lastTime)
                            && _fuelSyncValue.TryGetValue(syncId, out lastFuel);
                bool shouldSync = !hasSync
                    || Mathf.Abs(newFuel - lastFuel) / maxFuel >= FuelSyncThreshold
                    || Time.time - lastTime >= FuelSyncInterval;
                if (shouldSync)
                {
                    worldItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others, new object[] { worldItem.data });
                    _fuelSyncTime[syncId] = Time.time;
                    _fuelSyncValue[syncId] = newFuel;
                }
                // Note: auto re-light is handled in LanternFuelOverridePatch.UpdateFuelPostfix
                // (using Lantern's own photonView, not Item's, to avoid RPC routing issues)
            }

            Plugin.Log?.LogInfo($"[DEBUG] AddPlayerLanternFuel: {currentFuel:F1} +{fuelDelta:F1} = {newFuel:F1} (max={maxFuel:F1})");
            return true;
        }

        /// <summary>
        /// 将灯笼燃料回满到 startingFuel 最大值。
        /// </summary>
        public static bool RefillPlayerLanternFuel(Character character)
        {
            if (character == null || !character.IsLocal) return false;

            ItemInstanceData liveData;
            ItemSlot litLanternSlot = FindLitLanternSlot(character, out liveData);
            if (litLanternSlot == null)
            {
                Plugin.Log?.LogInfo("[DEBUG] RefillPlayerLanternFuel: no lit lantern slot found");
                return false;
            }

            ItemInstanceData dataForGuid = liveData ?? litLanternSlot.data;
            if (dataForGuid == null) return false;
            Guid targetGuid = dataForGuid.guid;
            if (targetGuid == Guid.Empty) return false;

            Item worldItem = FindWorldItemByGuid(targetGuid);
            if (worldItem == null)
            {
                Plugin.Log?.LogWarning("[DEBUG] RefillPlayerLanternFuel: world item not found");
                return false;
            }

            var lanternComponent = worldItem.GetComponent<Lantern>();
            if (lanternComponent == null) return false;

            FloatItemData fuelData;
            if (!worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out fuelData)) return false;

            float currentFuel = fuelData.Value;
            float maxFuel = lanternComponent.startingFuel;
            fuelData.Value = maxFuel;

            if (worldItem.photonView != null)
                worldItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others, new object[] { worldItem.data });

            Plugin.Log?.LogInfo($"[DEBUG] RefillPlayerLanternFuel: {currentFuel:F1} → {maxFuel:F1} (FULL)");
            return true;
        }

        /// <summary>
        /// 任意状态加燃料：优先走点亮的灯（退位 AddPlayerLanternFuel），找不到再回落到背包/没点的灯。
        /// 溢出量由调用者自行转入备用池。
        /// </summary>
        public static bool AddPlayerLanternFuelAnyState(Character character, float fuelDelta, out float overflow, out float maxFuelOut)
        {
            // 先走点亮路径（保留帧级缓存/同步节流）
            if (AddPlayerLanternFuel(character, fuelDelta, out overflow, out maxFuelOut))
                return true;

            overflow = 0f;
            maxFuelOut = 0f;
            if (character == null || !character.IsLocal) return false;
            if (Mathf.Approximately(fuelDelta, 0f)) return false;

            ItemInstanceData liveData;
            ItemSlot slot = FindAnyLanternSlot(character, out liveData);
            if (slot == null)
            {
                Plugin.Log?.LogInfo("[DEBUG] AddPlayerLanternFuelAnyState: no lantern found (lit or unlit)");
                return false;
            }

            ItemInstanceData dataForGuid = liveData ?? slot.data;
            if (dataForGuid == null) return false;
            Guid targetGuid = dataForGuid.guid;
            if (targetGuid == Guid.Empty) return false;

            Item worldItem = FindWorldItemByGuid(targetGuid);
            if (worldItem == null) return false;

            var lanternComponent = worldItem.GetComponent<Lantern>();
            if (lanternComponent == null) return false;

            FloatItemData fuelData;
            if (!worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out fuelData)) return false;

            float currentFuel = fuelData.Value;
            float maxFuel = lanternComponent.startingFuel;
            maxFuelOut = maxFuel;
            float rawNew = currentFuel + fuelDelta;
            float newFuel = Mathf.Clamp(rawNew, 0f, maxFuel);
            overflow = rawNew > maxFuel ? rawNew - maxFuel : 0f;
            fuelData.Value = newFuel;
            if (currentFuel <= 0.1f && newFuel > 0.1f)
                TryRelightAfterFuelGain(lanternComponent, "fuel-add-any-state");

            if (worldItem.photonView != null)
                worldItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others, new object[] { worldItem.data });

            Plugin.Log?.LogInfo($"[DEBUG] AddPlayerLanternFuelAnyState (unlit): {currentFuel:F1} +{fuelDelta:F1} = {newFuel:F1} (max={maxFuel:F1}, overflow={overflow:F1})");
            return true;
        }

        /// <summary>
        /// 任意状态回满灯：优先走点亮路径，失败再找背包/没点的灯满。
        /// </summary>
        public static bool RefillPlayerLanternFuelAnyState(Character character)
        {
            if (RefillPlayerLanternFuel(character)) return true;

            if (character == null || !character.IsLocal) return false;

            ItemInstanceData liveData;
            ItemSlot slot = FindAnyLanternSlot(character, out liveData);
            if (slot == null)
            {
                Plugin.Log?.LogInfo("[DEBUG] RefillPlayerLanternFuelAnyState: no lantern found (lit or unlit)");
                return false;
            }

            ItemInstanceData dataForGuid = liveData ?? slot.data;
            if (dataForGuid == null) return false;
            Guid targetGuid = dataForGuid.guid;
            if (targetGuid == Guid.Empty) return false;

            Item worldItem = FindWorldItemByGuid(targetGuid);
            if (worldItem == null) return false;

            var lanternComponent = worldItem.GetComponent<Lantern>();
            if (lanternComponent == null) return false;

            FloatItemData fuelData;
            if (!worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out fuelData)) return false;

            float currentFuel = fuelData.Value;
            float maxFuel = lanternComponent.startingFuel;
            fuelData.Value = maxFuel;

            if (worldItem.photonView != null)
                worldItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others, new object[] { worldItem.data });

            Plugin.Log?.LogInfo($"[DEBUG] RefillPlayerLanternFuelAnyState (unlit): {currentFuel:F1} → {maxFuel:F1} (FULL)");
            return true;
        }

        /// <summary>
        /// 找任何一盏灯笼（不论点/灭）。手持 → tempFullSlot → 背包。
        /// </summary>
        internal static bool TryGetAnyLocalLanternData(Character character, out ItemInstanceData data)
        {
            ItemInstanceData liveData;
            ItemSlot slot = FindAnyLanternSlot(character, out liveData);
            data = liveData ?? slot?.data;
            return data != null;
        }

        private static ItemSlot FindAnyLanternSlot(Character character, out ItemInstanceData liveData)
        {
            liveData = null;
            if (character == null || character.player == null) return null;

            // 1. 手持
            foreach (var slot in character.player.itemSlots)
            {
                if (slot == null || slot.IsEmpty() || slot.prefab == null) continue;
                if (slot.prefab.name != "Lantern") continue;
                Guid guid = (slot.data != null) ? slot.data.guid : Guid.Empty;
                Item worldItem = (guid != Guid.Empty) ? FindWorldItemByGuid(guid) : null;
                liveData = worldItem != null ? worldItem.data : slot.data;
                return slot;
            }

            // 2. tempFullSlot
            ItemSlot tempSlot = character.player.tempFullSlot;
            if (tempSlot != null && !tempSlot.IsEmpty() && tempSlot.prefab != null && tempSlot.prefab.name == "Lantern")
            {
                Guid guid = (tempSlot.data != null) ? tempSlot.data.guid : Guid.Empty;
                Item worldItem = (guid != Guid.Empty) ? FindWorldItemByGuid(guid) : null;
                liveData = worldItem != null ? worldItem.data : tempSlot.data;
                return tempSlot;
            }

            // 3. 背包
            BackpackSlot backpackSlot = character.player.backpackSlot;
            if (backpackSlot != null && backpackSlot.hasBackpack && backpackSlot.data != null)
            {
                BackpackData backpackData;
                if (backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out backpackData))
                {
                    if (backpackData != null && backpackData.itemSlots != null)
                    {
                        foreach (var slot in backpackData.itemSlots)
                        {
                            if (slot == null || slot.IsEmpty() || slot.prefab == null) continue;
                            if (slot.prefab.name != "Lantern") continue;
                            Guid guid = (slot.data != null) ? slot.data.guid : Guid.Empty;
                            Item worldItem = (guid != Guid.Empty) ? FindWorldItemByGuid(guid) : null;
                            liveData = worldItem != null ? worldItem.data : slot.data;
                            return slot;
                        }
                    }
                }
            }

            return null;
        }

        public static ItemSlot FindLitLanternSlot(Character character)
        {
            return FindLitLanternSlot(character, out _);
        }

        public static ItemSlot FindLitLanternSlot(Character character, out ItemInstanceData liveData)
        {
            liveData = null;
            if (character == null || character.player == null) return null;

            // ── 帧级缓存：同一帧内多个 Patch 调用只搜索一次 ──
            int frame = Time.frameCount;
            if (frame == _findSlotCachedFrame)
            {
                liveData = _findSlotCachedLiveData;
                return _findSlotCachedResult;
            }
            _findSlotCachedFrame = frame;

            // 1. 手持物品槽（要求 FlareActive=True）
            foreach (var slot in character.player.itemSlots)
            {
                if (IsLitLanternSlot(slot))
                {
                    if (Time.time - _lastFindLogTime >= FindLogInterval)
                    {
                        Plugin.Log?.LogInfo($"[DEBUG] Lit lantern found in HAND slot: {slot.prefab.name}");
                        _lastFindLogTime = Time.time;
                    }
                    liveData = slot.data;
                    UpdateCache(slot, liveData);
                    return slot;
                }
            }

            // 1b. 兜底：手持灯笼但 FlareActive 不可用时，通过世界实例 lit 字段检查
            //     （BPR 手电筒模式等可能导致 slot.data 中 FlareActive 状态不准）
            foreach (var slot in character.player.itemSlots)
            {
                ItemSlot result = CheckSlotWorldLit(slot, "HAND(worldLit)", out liveData);
                if (result != null) { UpdateCache(result, liveData); return result; }
            }

            // 1c. 临时第4槽（3个常规槽满时游戏创建的 tempFullSlot, slotID=250）
            ItemSlot tempSlot = character.player.tempFullSlot;
            if (tempSlot != null)
            {
                if (IsLitLanternSlot(tempSlot))
                {
                    if (Time.time - _lastFindLogTime >= FindLogInterval)
                    {
                        Plugin.Log?.LogInfo($"[DEBUG] Lit lantern found in TEMP slot (slotID=250): {tempSlot.prefab.name}");
                        _lastFindLogTime = Time.time;
                    }
                    liveData = tempSlot.data;
                    UpdateCache(tempSlot, liveData);
                    return tempSlot;
                }
                ItemSlot tempResult = CheckSlotWorldLit(tempSlot, "TEMP(worldLit,slotID=250)", out liveData);
                if (tempResult != null) { UpdateCache(tempResult, liveData); return tempResult; }
            }

            // 2. 背包（game 会 SnuffLantern，FlareActive 永远 false，只按名字匹配）
            BackpackSlot backpackSlot = character.player.backpackSlot;
            if (backpackSlot != null && backpackSlot.hasBackpack && backpackSlot.data != null)
            {
                BackpackData backpackData;
                if (backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out backpackData))
                {
                    if (backpackData != null && backpackData.itemSlots != null)
                    {
                        foreach (var slot in backpackData.itemSlots)
                        {
                            if (slot == null || slot.IsEmpty() || slot.prefab == null) continue;
                            if (slot.prefab.name != "Lantern") continue;

                            Guid guid = (slot.data != null) ? slot.data.guid : Guid.Empty;
                            Item worldItem = (guid != Guid.Empty) ? FindWorldItemByGuid(guid) : null;
                            if (worldItem != null)
                            {
                                var lanComp = worldItem.GetComponent<Lantern>();
                                bool worldLit = false;
                                if (lanComp != null)
                                {
                                    worldLit = ReflectionCache.GetLit(lanComp);
                                }
                                if (Time.time - _lastFindLogTime >= FindLogInterval)
                                {
                                    Plugin.Log?.LogInfo($"[DEBUG] Lantern found in BACKPACK, world instance OK (ViewID={worldItem.GetComponent<PhotonView>()?.ViewID}, worldLit={worldLit})");
                                    _lastFindLogTime = Time.time;
                                }
                                liveData = worldItem.data;
                            }
                            else
                            {
                                if (Time.time - _lastFindLogTime >= FindLogInterval)
                                {
                                    Plugin.Log?.LogInfo($"[DEBUG] Lantern found in BACKPACK, no world instance (guid={guid}), using slot data");
                                    _lastFindLogTime = Time.time;
                                }
                                liveData = slot.data;
                            }
                            UpdateCache(slot, liveData);
                            return slot;
                        }
                    }
                }
            }

            // 3. 宽限期兆底：BPR 模式切换期间 FlareActive/worldLit 同时无效时使用字典缓存
            foreach (var kv in _slotCacheById)
            {
                var entry = kv.Value;
                if (Time.time >= entry.Expiry) continue;
                if (entry.Slot == null || entry.Slot.IsEmpty() || entry.Slot.prefab?.name != "Lantern") continue;
            
                Guid cGuid = entry.Slot.data?.guid ?? Guid.Empty;
                if (cGuid != Guid.Empty)
                {
                    Item cItem = FindWorldItemByGuid(cGuid);
                    liveData = cItem != null ? cItem.data : entry.LiveData;
                }
                else
                {
                    liveData = entry.LiveData;
                }
            
                if (!_cacheHitLogged)
                {
                    _cacheHitLogged = true;
                    Plugin.Log?.LogInfo($"[DEBUG] [FindLantern] Grace-period cache HIT (id={kv.Key}, BPR mode switch?)");
                }
                _findSlotCachedResult = entry.Slot;
                _findSlotCachedLiveData = liveData;
                return entry.Slot;
            }

            // 缓存过期或无缓存，清除命中日志标记
            _cacheHitLogged = false;
            _findSlotCachedResult = null;
            _findSlotCachedLiveData = null;
            return null;
        }

        /// <summary>更新字典缓存。</summary>
        private static void UpdateCache(ItemSlot slot, ItemInstanceData liveData)
        {
            int id = (slot?.data != null) ? slot.data.guid.GetHashCode() : 0;
            _slotCacheById[id] = new SlotCacheEntry
            {
                Slot = slot,
                LiveData = liveData,
                Expiry = Time.time + CacheGracePeriod
            };
            _cacheHitLogged = false;
            // 同步更新帧级缓存
            _findSlotCachedResult = slot;
            _findSlotCachedLiveData = liveData;
        }

        /// <summary>检查单个槽位的世界实例 lit 状态（FlareActive 兜底）。</summary>
        private static ItemSlot CheckSlotWorldLit(ItemSlot slot, string slotLabel, out ItemInstanceData liveData)
        {
            liveData = null;
            if (slot == null || slot.IsEmpty() || slot.prefab == null) return null;
            if (slot.prefab.name != "Lantern") return null;
            Guid guid = (slot.data != null) ? slot.data.guid : Guid.Empty;
            if (guid == Guid.Empty) return null;
            Item worldItem = FindWorldItemByGuid(guid);
            if (worldItem == null) return null;
            var lanComp = worldItem.GetComponent<Lantern>();
            if (lanComp == null) return null;
            bool worldLit = ReflectionCache.GetLit(lanComp);
            if (worldLit)
            {
                Plugin.Log?.LogInfo($"[DEBUG] Lit lantern found in {slotLabel} via world-instance fallback");
                liveData = worldItem.data;
                return slot;
            }
            return null;
        }

        /// <summary>
        /// 通过 GUID 查找世界实例。优先搜索活跃列表，
        /// 若未命中则搜索场景中所有 Lantern（处理 kinematic 持有物被移出活跃列表的情况）。
        /// </summary>
        internal static Item FindWorldItemByGuid(Guid guid)
        {
            // ── 缓存命中：同一 GUID 且未过期且对象有效 ──
            if (guid == _worldItemCachedGuid && _worldItemCachedResult != null
                && Time.time - _worldItemCacheTime < WorldItemCacheDuration)
            {
                return _worldItemCachedResult;
            }
        
            Item found = null;
        
            // 优先：活跃物品列表
            foreach (var item in Item.ALL_ACTIVE_ITEMS)
            {
                if (item != null && item.data != null && item.data.guid == guid)
                {
                    found = item;
                    break;
                }
            }
        
            // 兵底：搜索所有 Lantern 实例
            if (found == null)
            {
                foreach (var lantern in UnityEngine.Object.FindObjectsByType<Lantern>(FindObjectsSortMode.None))
                {
                    var item = lantern.GetComponent<Item>();
                    if (item != null && item.data != null && item.data.guid == guid)
                    {
                        found = item;
                        break;
                    }
                }
            }
        
            // 更新缓存
            _worldItemCachedGuid = guid;
            _worldItemCachedResult = found;
            _worldItemCacheTime = Time.time;
        
            return found;
        }

        private static bool IsLitLanternSlot(ItemSlot slot)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null) return false;
            if (slot.prefab.name != "Lantern") return false;
            BoolItemData litData;
            return slot.data != null && slot.data.TryGetDataEntry(DataEntryKey.FlareActive, out litData) && litData.Value;
        }
    }
}
