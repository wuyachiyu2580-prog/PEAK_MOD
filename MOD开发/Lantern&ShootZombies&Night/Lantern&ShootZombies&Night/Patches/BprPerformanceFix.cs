using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Peak.Network;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// BPR 性能修复：缓存 Lantern.lit 的 FieldInfo，
    /// 替换 BPR.Update() 和 Patch_Lantern_BurnDuration.PrefixUpdateFuel 中
    /// 每帧/每调用 typeof(Lantern).GetField("lit", ...) 的反射查找。
    /// 
    /// 性能收益：每帧节省 ~2-5μs（GetField 查找开销），
    /// 同时消除 BPR.Update 中冗余的 GetComponent&lt;Lantern&gt;() 调用。
    /// </summary>
    internal static class BprPerformanceFix
    {
        // ─── UpdatePrefix 组件缓存（避免每帧 GetComponent）───
        private static int _cachedLanternItemId = -1;
        private static Lantern _cachedLanternComp;

        private static bool _applied;

        /// <summary>应用 BPR 性能补丁。在 Plugin.Awake 中调用。</summary>
        internal static void Apply(Harmony harmony)
        {
            if (_applied) return;

            try
            {
                // 1. Prefix BPR.Update() — 用缓存的 LitField 替换每帧 GetField
                var bprUpdate = AccessTools.Method(typeof(BlackPeakRemix.BlackPeakRemix), "Update");
                if (bprUpdate != null)
                {
                    harmony.Patch(bprUpdate,
                        prefix: new HarmonyMethod(
                            AccessTools.Method(typeof(BprPerformanceFix), nameof(UpdatePrefix)))
                        { priority = Priority.First });
                    Plugin.Log?.LogInfo("[PerfFix] Patched BPR.Update() — cached Lantern.lit FieldInfo");
                }
                else
                {
                    Plugin.Log?.LogWarning("[PerfFix] BPR.Update() method not found");
                }

                // 2. 替换 BPR 的 PrefixUpdateFuel（Patch_Lantern_BurnDuration 是 internal 类，需反射查找）
                var updateFuel = AccessTools.Method(typeof(Lantern), "UpdateFuel");
                var burnDurationType = typeof(BlackPeakRemix.BlackPeakRemix).Assembly
                    .GetType("BlackPeakRemix.Patch_Lantern_BurnDuration");
                MethodInfo bprPrefix = burnDurationType != null
                    ? AccessTools.Method(burnDurationType, "PrefixUpdateFuel")
                    : null;

                if (updateFuel != null && bprPrefix != null)
                {
                    harmony.Unpatch(updateFuel, bprPrefix);
                    harmony.Patch(updateFuel,
                        prefix: new HarmonyMethod(
                            AccessTools.Method(typeof(BprPerformanceFix), nameof(OptimizedPrefixUpdateFuel)))
                        { priority = Priority.First });
                    Plugin.Log?.LogInfo("[PerfFix] Replaced BPR.PrefixUpdateFuel — cached Lantern.lit FieldInfo");
                }
                else
                {
                    Plugin.Log?.LogWarning($"[PerfFix] Cannot replace PrefixUpdateFuel " +
                        $"(updateFuel={updateFuel != null}, burnType={burnDurationType != null}, prefix={bprPrefix != null})");
                }

                _applied = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[PerfFix] BprPerformanceFix.Apply failed: {ex}");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Prefix: 替换 BlackPeakRemix.Update()
        //  原版每帧 typeof(Lantern).GetField("lit", ...) → 预缓存 LitField
        //  同时消除冗余的第二次 GetComponent<Lantern>()
        // ════════════════════════════════════════════════════════════
        private static bool UpdatePrefix()
        {
            try
            {
                // ActiveMasterSwitch 是 public static 字段 → 直接访问
                if (!BlackPeakRemix.BlackPeakRemix.ActiveMasterSwitch)
                    return false;

                if (Character.localCharacter == null)
                    return false;

                CharacterData data = Character.localCharacter.data;
                Item item = (data != null) ? data.currentItem : null;
                if (item == null || item.itemID != 42)
                    return false;

                // LanternSyncHelper 可能是 internal 类 → 通过 ReflectionCache 访问
                if (ReflectionCache.BprSyncHelperType == null) return false;
                Component syncHelper = item.GetComponent(ReflectionCache.BprSyncHelperType);
                if (syncHelper == null)
                {
                    Lantern lantern = item.GetComponent<Lantern>();
                    if (lantern == null) return false;
                    syncHelper = (Component)item.gameObject.AddComponent(ReflectionCache.BprSyncHelperType);
                    ReflectionCache.BprSyncHelperInitMethod?.Invoke(syncHelper, new object[] { lantern });
                }

                // ★ 核心优化：使用 ReflectionCache FieldRef 代替每帧 GetField ★
                // 缓存 Lantern 组件（同一物品不重复查找）
                int itemId = item.GetInstanceID();
                Lantern lanternComp;
                if (itemId != _cachedLanternItemId || _cachedLanternComp == null)
                {
                    lanternComp = item.GetComponent<Lantern>();
                    _cachedLanternItemId = itemId;
                    _cachedLanternComp = lanternComp;
                }
                else
                {
                    lanternComp = _cachedLanternComp;
                }
                if (lanternComp == null) return false;
                bool isLit = ReflectionCache.GetLit(lanternComp);

                // ToggleKey 是 public static ConfigEntry<KeyCode> → 直接访问
                if (isLit && Input.GetKeyDown(BlackPeakRemix.BlackPeakRemix.ToggleKey.Value))
                {
                    if (ReflectionCache.BprSyncHelperModeField != null &&
                        ReflectionCache.BprStateOmni != null && ReflectionCache.BprStateFlashlight != null)
                    {
                        object currentMode = ReflectionCache.BprSyncHelperModeField.GetValue(syncHelper);
                        object newMode = currentMode.Equals(ReflectionCache.BprStateOmni)
                            ? ReflectionCache.BprStateFlashlight : ReflectionCache.BprStateOmni;
                        ReflectionCache.BprSyncHelperModeField.SetValue(syncHelper, newMode);

                        // BPR.Log 是 internal → 反射获取
                        var log = ReflectionCache.BprLogField?.GetValue(null) as ManualLogSource;
                        log?.LogInfo($"[Input] Lantern mode toggled: {newMode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PerfFix] BPR.Update prefix error: {ex.Message}");
            }

            return false; // 跳过原始 BPR.Update()
        }

        // ════════════════════════════════════════════════════════════
        //  Prefix: 替换 Patch_Lantern_BurnDuration.PrefixUpdateFuel
        //  原版每调用 typeof(Lantern).GetField("lit", ...) → 预缓存 LitField
        //  注意：执行顺序在 LanternFuelOverridePatch.UpdateFuelPrefix 之后，
        //  与原 BPR prefix 行为一致。
        // ════════════════════════════════════════════════════════════
        private static bool OptimizedPrefixUpdateFuel(Lantern __instance)
        {
            try
            {
                if (!BlackPeakRemix.BlackPeakRemix.ActiveMasterSwitch || !IsNormalLantern(__instance))
                    return true;

                if (!__instance.HasData(DataEntryKey.Fuel))
                    return true;

                // ★ 核心优化：使用 ReflectionCache FieldRef ★
                bool isLit = ReflectionCache.GetLit(__instance);
                if (!isLit || !NetworkingUtilities.HasAuthority(__instance))
                    return false;

                float burnDuration = BlackPeakRemix.BlackPeakRemix.ActiveLanternBurnDuration;
                if (burnDuration > 20000f)
                    return false;

                float rate = 60f / burnDuration;
                float current = __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value;
                float next = Mathf.Clamp(current - Time.deltaTime * rate, 0f, __instance.startingFuel);
                __instance.GetData<FloatItemData>(DataEntryKey.Fuel).Value = next;

                Item item = __instance.item;
                if (item != null)
                    item.SetUseRemainingPercentage(next / __instance.startingFuel);

                if (next <= 0f && __instance.photonView.IsMine)
                    __instance.SnuffLantern();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PerfFix] PrefixUpdateFuel error: {ex.Message}");
            }

            return false; // 跳过原始 Lantern.UpdateFuel
        }

        private static bool IsNormalLantern(Lantern lantern)
        {
            Item item = (lantern != null) ? lantern.GetComponent<Item>() : null;
            return item != null && item.itemID == 42;
        }
    }
}
