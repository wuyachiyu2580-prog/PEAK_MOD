using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// ModConfig 配置项名称 / 分区名 / 插件名的双语本地化。
    /// 包含 GetDisplayName postfix、TMPro UI 文字替换、缓存刷新、描述更新。
    /// </summary>
    internal static class ModConfigLocalization
    {
        private static readonly FieldInfo _descriptionBackingField =
            typeof(ConfigDescription).GetField("<Description>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        // TMPro reflection cache
        private static Type _tmpTextType;
        private static PropertyInfo _tmpTextProp;

        // ── Public API ──────────────────────────────────────────────

        /// <summary>注册所有 ModConfig 相关的 Harmony 补丁。</summary>
        public static void PatchDisplayNames(Harmony harmony)
        {
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue("com.github.PEAKModding.PEAKLib.ModConfig", out pluginInfo))
                return;
            if (pluginInfo?.Instance == null) return;

            Assembly mcAssembly = pluginInfo.Instance.GetType().Assembly;

            // 1. Patch GetDisplayName on BepInEx setting-option types (config entry names)
            HarmonyMethod postfix = new HarmonyMethod(
                typeof(ModConfigLocalization).GetMethod(nameof(ModConfigDisplayNamePostfix),
                    BindingFlags.Static | BindingFlags.NonPublic));

            int count = 0;
            foreach (Type type in mcAssembly.GetTypes()
                .Where(t => t != null && !t.IsAbstract && !t.IsInterface
                    && t.FullName != null
                    && t.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal)))
            {
                MethodInfo getDisplayName = AccessTools.Method(type, "GetDisplayName", Type.EmptyTypes);
                if (getDisplayName != null)
                {
                    harmony.Patch(getDisplayName, null, postfix);
                    count++;
                }
            }
            Plugin.Log?.LogInfo($"[DEBUG] Patched {count} ModConfig GetDisplayName methods");

            // 2. Patch ModdedSettingsMenu UI methods for section/mod name localization
            HarmonyMethod uiPostfix = new HarmonyMethod(
                typeof(ModConfigLocalization).GetMethod(nameof(ModConfigUiChangedPostfix),
                    BindingFlags.Static | BindingFlags.NonPublic));

            Type menuType = mcAssembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
            if (menuType != null)
            {
                foreach (string methodName in new[] { "OnEnable", "ShowSettings", "SetSection", "UpdateSectionTabs" })
                {
                    MethodInfo mi = menuType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == methodName);
                    if (mi != null)
                    {
                        harmony.Patch(mi, null, uiPostfix);
                        Plugin.Log?.LogInfo($"[DEBUG] Patched ModdedSettingsMenu.{methodName} for UI localization");
                    }
                }
            }
        }

        /// <summary>
        /// 清除 PEAKLib.ModConfig 缓存并重新处理，使 UI 刷新本地化描述。
        /// </summary>
        public static void RefreshCache()
        {
            try
            {
                PluginInfo pluginInfo;
                if (!Chainloader.PluginInfos.TryGetValue("com.github.PEAKModding.PEAKLib.ModConfig", out pluginInfo))
                    return;
                if (pluginInfo?.Instance == null) return;

                Type mcType = pluginInfo.Instance.GetType();

                foreach (string propName in new[] { "EntriesProcessed", "ModdedKeys", "GetValidKeyPaths" })
                {
                    PropertyInfo prop = mcType.GetProperty(propName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    IList list = prop?.GetValue(null) as IList;
                    if (list != null) list.Clear();
                }

                foreach (string methodName in new[] { "GenerateValidKeyPaths", "ProcessModEntries", "LoadModSettings" })
                {
                    MethodInfo mi = mcType.GetMethod(methodName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    mi?.Invoke(null, null);
                }

                Plugin.Log?.LogInfo("[DEBUG] ModConfig cache refreshed for localization");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] RefreshModConfigCache failed: {ex.Message}");
            }
        }

        /// <summary>根据当前语言更新所有配置项描述文本。</summary>
        public static void ApplyLocalizedDescriptions()
        {
            try
            {
                SetDescription(Plugin.LanternMaxFuel,
                    LanguageHelper.L("Fuel capacity. GameDefault=unchanged, others=seconds, Infinite=never burns out.",
                      "燃料上限。默认=不改，其他=秒数，无限=永不熄灭。"));
                SetDescription(Plugin.EnableWarmthReduction,
                    LanguageHelper.L("Enable warmth multiplier below.",
                      "启用下方的暖值倍率。"));
                SetDescription(Plugin.LanternWarmthMultiplier,
                    LanguageHelper.L("Cold resistance when holding lit lantern. 1=full block, 0.5=half cold, 0=no protection. Step=0.05",
                      "持灯抗寒能力。1=完全抵御，0.5=减半受冷，0=无保护。步进=0.05"));
                SetDescription(Plugin.EnableWarmthRestore,
                    LanguageHelper.L("Master switch for warmth restore (zombie hit).",
                      "回暖总开关（打僵尸回暖）。"));
                SetDescription(Plugin.HitRestoreWarmth,
                    LanguageHelper.L("Warmth per zombie hit (seconds).",
                      "每次击杀僵尸回暖（秒）。"));
                SetDescription(Plugin.RestoreRadius,
                    LanguageHelper.L("Range for hit restore (meters).",
                      "击杀回暖范围（米）。"));
                SetDescription(Plugin.HitRestoreCooldown,
                    LanguageHelper.L("Min interval between hit restores (seconds). Step=0.1",
                      "击杀回暖最小间隔（秒）。步进=0.1"));
                SetDescription(Plugin.ReserveWarmthMax,
                    LanguageHelper.L("Reserve = max × ratio. Overflow fills reserve; burning drains it first.",
                      "备用池 = 上限×比例。溢出存备用，燃烧优先消耗。"));
                SetDescription(Plugin.AutoRefillEnabled,
                    LanguageHelper.L("When lantern is out of fuel, slowly refill up to a cap.",
                      "灯熄灭后自动缓慢回燃料至上限。"));
                SetDescription(Plugin.AutoRefillCapPercent,
                    LanguageHelper.L("Refill upper cap (ratio of max fuel). Step=0.05",
                      "自动回料上限（占满燃料比例）。步进=0.05"));
                SetDescription(Plugin.AutoRefillRate,
                    LanguageHelper.L("Seconds of fuel gained per real second. Step=0.1",
                      "每现实秒回复的燃料秒数。步进=0.1"));
                SetDescription(Plugin.AutoRefillDaytimeOnly,
                    LanguageHelper.L("Only refill during daytime.",
                      "仅在白天自动回料。"));
                SetDescription(Plugin.AutoRefillRequireHold,
                    LanguageHelper.L("Only refill while a player is holding the lantern.",
                      "仅在玩家持有灯时自动回料。"));
                SetDescription(Plugin.BugleUltimateEnabled,
                    LanguageHelper.L("Enable bugle ultimate: nearby players' lanterns refill, long global cooldown.",
                      "启用号角大招：附近玩家灯笼回满，全局长CD。"));
                SetDescription(Plugin.BugleUltimateCooldown,
                    LanguageHelper.L("Global cooldown between bugle ultimates (seconds).",
                      "号角大招CD（秒），全局共享。"));
                SetDescription(Plugin.BugleUltimateRadius,
                    LanguageHelper.L("Ultimate effect radius (meters).",
                      "大招生效范围（米）。"));
                SetDescription(Plugin.BugleUltimateRestore,
                    LanguageHelper.L("Warmth gained per ultimate. -1 means refill to max.",
                      "单次大招回暖秒数。-1 表示回满。"));
                SetDescription(Plugin.PurgeExtraLanterns,
                    LanguageHelper.L("Auto destroy duplicate lanterns on a player (Faerie lantern exempt).",
                      "自动销毁玩家身上的多余灯笼（仙子提灯豁免）。"));
                SetDescription(Plugin.StrayBugleCleanupEnabled,
                    LanguageHelper.L("Host cleans stray (unheld, far away) bugles after grace period.",
                      "房主自动清理无人持有且偏远的号角。"));
                SetDescription(Plugin.StrayBugleDistance,
                    LanguageHelper.L("Distance from nearest player to count as stray (meters).",
                      "距最近玩家大于此距离才算偏远（米）。"));
                SetDescription(Plugin.StrayBugleGracePeriod,
                    LanguageHelper.L("Must stay stray for N seconds before cleanup.",
                      "持续N秒满足条件才清理。"));
                SetDescription(Plugin.EnableCampfireRefuel,
                    LanguageHelper.L("Light campfire = refill lantern; near campfire = no fuel drain.",
                      "篝火补满&暂停消耗。"));
                SetDescription(Plugin.FlashlightDrainMultiplier,
                    LanguageHelper.L("Fuel drain multiplier when BPR flashlight mode is active. 1=normal, 2=double. Step=0.05",
                      "BPR手电筒模式激活时的燃料消耗倍率。1=正常，2=双倍消耗。步进=0.05"));
                SetDescription(Plugin.CompanionDrainMultiplier,
                    LanguageHelper.L("Drain multiplier when companions are nearby. More players = stronger effect (scales to 3). Step=0.05",
                      "附近有同伴时的消耗倍率。人越多效果越强（最多按3人计算）。步进=0.05"));
                SetDescription(Plugin.SoloDrainMultiplier,
                    LanguageHelper.L("Drain multiplier when alone. Step=0.05",
                      "独自行动时的消耗倍率。步进=0.05"));
                SetDescription(Plugin.ProximityGracePeriod,
                    LanguageHelper.L("Seconds before companion/solo multiplier activates after state change.",
                      "状态切换后，结伴/独行倍率生效前的等待秒数。"));
                SetDescription(Plugin.EnableHud,
                    LanguageHelper.L("Show visual HUD panel (fuel bar, multipliers, status).",
                      "显示可视化信息面板（燃料条、倍率、状态）。"));
                SetDescription(Plugin.HudPos,
                    LanguageHelper.L("HUD panel screen position (8 positions).",
                      "HUD面板屏幕位置（8方位）。"));
                SetDescription(Plugin.HudSize,
                    LanguageHelper.L("HUD panel size preset (Small/Medium/Large/ExtraLarge).",
                      "HUD面板尺寸预设（小/中/大/超大）。"));
                SetDescription(Plugin.ShowDayNightOnHud,
                    LanguageHelper.L("Show day/night info on HUD (day count, time, BPR darkness).",
                      "在HUD上显示日夜信息（天数、时间、BPR黑暗状态）。"));
                SetDescription(Plugin.SpawnItemsKey,
                    LanguageHelper.L("Hotkey to spawn Bugle & Backpack if missing. Set to None to disable.",
                      "快捷键生成号角和背包（身上没有时）。设为None可禁用。"));
                SetDescription(Plugin.BugleRecallKey,
                    LanguageHelper.L("Host-only hotkey: destroy ALL bugles in the scene (held or dropped). Set to None to disable.",
                      "房主专属快捷键：销毁场景里所有号角（含被持有/掉落的）。设为None可禁用。"));
                SetDescription(Plugin.MuteZombieTornado,
                    LanguageHelper.L("Mute all zombie and tornado sounds.",
                      "静音所有僵尸和龙卷风的声音。"));
                SetDescription(Plugin.ActivePreset,
                    LanguageHelper.L("Config preset. Custom=manual, Casual=easy, Balanced=default, Hardcore=challenging. Switching applies all values at once.",
                      "配置预设。自定义=手动调节，休闲=轻松，平衡=默认值，硬核=高挑战。切换时一键覆盖所有平衡相关配置。"));
                SetDescription(Plugin.EnableUpgradeSystem,
                    LanguageHelper.L("Enable lantern upgrade system. Points auto-spend to upgrade (capacity first, then efficiency).",
                      "启用灯笼升级系统。点数足够时自动升级（先容量再效率）。"));
                SetDescription(Plugin.UpgradeLevelCostsCsv,
                    LanguageHelper.L("Point cost per level (comma separated, 5 values).",
                      "每级升级所需点数（逗号分隔，5 个值）。"));
                SetDescription(Plugin.UpgradeCapacityBonusCsv,
                    LanguageHelper.L("Capacity bonus per level (comma separated, 5 values). Final multiplier = 1 + value.",
                      "每级容量加成（5 个值）。最终倍率 = 1 + 值。"));
                SetDescription(Plugin.UpgradeEfficiencyBonusCsv,
                    LanguageHelper.L("Efficiency bonus per level (comma separated, 5 values). Final drain multiplier = 1 - value.",
                      "每级效率加成（5 个值）。最终消耗倍率 = 1 - 值。"));
                SetDescription(Plugin.UpgradePassiveTickInterval,
                    LanguageHelper.L("Passive points accrual interval in seconds.",
                      "被动累积点数的间隔秒数。"));
                SetDescription(Plugin.UpgradePassivePointsPerTick,
                    LanguageHelper.L("Points gained per passive tick.",
                      "每次被动累积获得的点数。"));
                SetDescription(Plugin.UpgradeHitPoints,
                    LanguageHelper.L("Points gained per zombie hit.",
                      "每次打僵尸获得的点数。"));
                SetDescription(Plugin.UpgradeCampfirePoints,
                    LanguageHelper.L("Points gained per campfire light-up.",
                      "每次点燃篝火获得的点数。"));
                SetDescription(Plugin.UpgradeBuglePoints,
                    LanguageHelper.L("Points gained per bugle ultimate trigger.",
                      "每次号角大招触发获得的点数。"));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] ApplyLocalizedDescriptions failed: {ex.Message}");
            }
        }

        // ── Harmony callbacks ───────────────────────────────────────

        /// <summary>Postfix: replace display name with localized version for our config keys.</summary>
        private static void ModConfigDisplayNamePostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            string normalized = __result.Replace(" ", "");
            string localized = GetLocalizedKeyName(normalized);
            if (localized != null) __result = localized;
        }

        /// <summary>After the ModConfig UI updates, wait one frame then localize text.</summary>
        private static void ModConfigUiChangedPostfix(MonoBehaviour __instance)
        {
            __instance.StartCoroutine(LocalizeModConfigUiDeferred(__instance.transform));
        }

        private static IEnumerator LocalizeModConfigUiDeferred(Transform root)
        {
            yield return null;
            LocalizeTextInHierarchy(root);
        }

        // ── Private helpers ─────────────────────────────────────────

        private static void LocalizeTextInHierarchy(Transform root)
        {
            try
            {
                if (_tmpTextType == null)
                {
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _tmpTextType = asm.GetType("TMPro.TextMeshProUGUI");
                        if (_tmpTextType != null)
                        {
                            _tmpTextProp = _tmpTextType.GetProperty("text");
                            break;
                        }
                    }
                }
                if (_tmpTextType == null || _tmpTextProp == null) return;

                Component[] texts = root.GetComponentsInChildren(_tmpTextType, true);
                foreach (Component textComp in texts)
                {
                    string current = _tmpTextProp.GetValue(textComp) as string;
                    if (string.IsNullOrEmpty(current)) continue;
                    string normalized = current.Replace(" ", "");
                    string localized = GetLocalizedKeyName(normalized);
                    if (localized != null && localized != current)
                    {
                        _tmpTextProp.SetValue(textComp, localized);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DEBUG] LocalizeTextInHierarchy: {ex.Message}");
            }
        }

        private static string GetLocalizedKeyName(string key)
        {
            bool zh = LanguageHelper.IsChinese;
            switch (key)
            {
                // ---- Section names (5 分区：Lantern / Restore / Bugle / Upgrade / Display) ----
                case "Lantern":
                    return zh ? "灯笼" : "Lantern";
                case "Restore":
                    return zh ? "回暖" : "Restore";
                case "Bugle":
                    return zh ? "号角" : "Bugle";
                case "Upgrade":
                    return zh ? "灯笼升级" : "Upgrade";
                case "Display":
                    return zh ? "显示与操作" : "Display";
                // ---- Plugin name ----
                case "LanternShootZombiesNight":
                    return zh ? "\u7164\u706f\u4e0e\u591c\u665a" : "LanternShootZombiesNight";
                // ---- Config entry names ----
                case "EnableWarmthReduction":
                    return zh ? "暖值倍率开关" : "Enable Warmth Reduction";
                case "LanternWarmthMultiplier":
                    return zh ? "暖值倍率" : "Warmth Multiplier";
                case "LanternMaxFuel":
                    return zh ? "燃料上限" : "Max Fuel";
                // ---- Enum values: LanternFuelOption ----
                case "GameDefault":
                    return zh ? "\u9ed8\u8ba4" : "Game Default";
                case "Seconds30":
                    return zh ? "30\u79d2" : "30 Seconds";
                case "Seconds60":
                    return zh ? "60\u79d2" : "60 Seconds";
                case "Seconds90":
                    return zh ? "90\u79d2" : "90 Seconds";
                case "Seconds120":
                    return zh ? "120\u79d2" : "120 Seconds";
                case "Seconds240":
                    return zh ? "240\u79d2" : "240 Seconds";
                case "Infinite":
                    return zh ? "\u65e0\u9650" : "Infinite";
                case "EnableWarmthRestore":
                    return zh ? "回暖总开关" : "Enable Restore";
                case "HitRestoreWarmth":
                    return zh ? "击杀回暖" : "Hit Restore";
                case "RestoreRadius":
                    return zh ? "回暖范围" : "Restore Radius";
                case "HitRestoreCooldown":
                    return zh ? "回暖冷却" : "Restore Cooldown";
                case "ChestRestoreWarmth":
                case "UseItemRestoreWarmth":
                case "ConsumeRestoreWarmth":
                case "CookingRestoreWarmth":
                case "RescueRestoreWarmth":
                case "CookedFoodBonusMultiplier":
                case "HuddleRestoreWarmth":
                case "HuddleTickInterval":
                case "BugleRestoreWarmth":
                case "UseUnifiedRestore":
                case "UnifiedRestoreWarmth":
                case "RestoreVarietyCount":
                    return null; // 已在 0.2.0 删除
                case "AutoRefillEnabled":
                    return zh ? "自动回料开关" : "Auto Refill";
                case "AutoRefillCapPercent":
                    return zh ? "回料上限比例" : "Refill Cap";
                case "AutoRefillRate":
                    return zh ? "回料速率" : "Refill Rate";
                case "AutoRefillDaytimeOnly":
                    return zh ? "仅白天回料" : "Daytime Only";
                case "AutoRefillRequireHold":
                    return zh ? "仅持有时回料" : "Require Hold";
                case "BugleUltimateEnabled":
                    return zh ? "大招开关" : "Ultimate Enabled";
                case "BugleUltimateCooldown":
                    return zh ? "大招CD" : "Ultimate Cooldown";
                case "BugleUltimateRadius":
                    return zh ? "大招范围" : "Ultimate Radius";
                case "BugleUltimateRestore":
                    return zh ? "大招回暖量" : "Ultimate Restore";
                case "PurgeExtraLanterns":
                    return zh ? "自动销毁多余灯" : "Purge Extra Lanterns";
                case "StrayBugleCleanupEnabled":
                    return zh ? "清理无主号角" : "Cleanup Stray Bugle";
                case "StrayBugleDistance":
                    return zh ? "无主距离阈值" : "Stray Distance";
                case "StrayBugleGracePeriod":
                    return zh ? "无主持续时间" : "Stray Grace";
                case "ReserveWarmthMax":
                    return zh ? "备用池比例" : "Reserve Ratio";
                case "EnableCampfireRefuel":
                    return zh ? "篝火补满&暂停消耗" : "Campfire Refuel";
                // ---- HUD 相关条目（Display 分区） ----
                case "EnableHud":
                    return zh ? "信息面板" : "Enable HUD";
                case "HudPos":
                    return zh ? "面板位置" : "HUD Position";
                case "HudFontSize":
                case "HudSize":
                    return zh ? "面板尺寸" : "HUD Size";
                case "ShowDayNightOnHud":
                    return zh ? "显示日夜" : "Day/Night HUD";
                case "SpawnItemsKey":
                    return zh ? "生成装备快捷键" : "Spawn Items Key";
                case "BugleRecallKey":
                    return zh ? "召回号角快捷键" : "Bugle Recall Key";
                case "MuteZombieTornado":
                    return zh ? "僵尸龙卷风静音" : "Mute Zombie & Tornado";
                // ---- Upgrade 分区条目 ----
                case "EnableUpgradeSystem":
                    return zh ? "升级系统" : "Upgrade System";
                case "LevelCostsCsv":
                    return zh ? "升级消耗点数" : "Level Costs";
                case "CapacityBonusCsv":
                    return zh ? "容量加成表" : "Capacity Bonus";
                case "EfficiencyBonusCsv":
                    return zh ? "效率加成表" : "Efficiency Bonus";
                case "PassiveTickInterval":
                    return zh ? "被动累积间隔" : "Passive Interval";
                case "PassivePointsPerTick":
                    return zh ? "被动每次点数" : "Passive Points";
                case "HitPoints":
                    return zh ? "打僵尸点数" : "Hit Points";
                case "CampfirePoints":
                    return zh ? "篝火点数" : "Campfire Points";
                case "BuglePoints":
                    return zh ? "号角大招点数" : "Bugle Points";
                // ---- Enum: HudSizePreset ----
                case "Small":
                    return zh ? "小" : "Small";
                case "Medium":
                    return zh ? "中" : "Medium";
                case "Large":
                    return zh ? "大" : "Large";
                case "ExtraLarge":
                    return zh ? "超大" : "Extra Large";
                // ---- Enum: HudPosition (8方位) ----
                case "TopLeft":
                    return zh ? "左上" : "Top Left";
                case "Top":
                    return zh ? "上方" : "Top";
                case "TopRight":
                    return zh ? "右上" : "Top Right";
                case "Left":
                    return zh ? "左侧" : "Left";
                case "Right":
                    return zh ? "右侧" : "Right";
                case "BottomLeft":
                    return zh ? "左下" : "Bottom Left";
                case "Bottom":
                    return zh ? "下方" : "Bottom";
                case "BottomRight":
                    return zh ? "右下" : "Bottom Right";
                // ---- Drain multipliers (已归入 Lantern 分区) ----
                case "FlashlightDrainMultiplier":
                    return zh ? "手电筒消耗倍率" : "Flashlight Drain";
                case "CompanionDrainMultiplier":
                    return zh ? "结伴消耗倍率" : "Companion Drain";
                case "SoloDrainMultiplier":
                    return zh ? "独行消耗倍率" : "Solo Drain";
                case "ProximityGracePeriod":
                    return zh ? "状态生效时间" : "Grace Period";
                // ---- Config entry: Preset ----
                case "ActivePreset":
                    return zh ? "配置预设" : "Active Preset";
                // ---- Enum values: ConfigPreset ----
                case "Custom":
                    return zh ? "自定义" : "Custom";
                case "Casual":
                    return zh ? "休闲" : "Casual";
                case "Balanced":
                    return zh ? "平衡" : "Balanced";
                case "Hardcore":
                    return zh ? "硬核" : "Hardcore";
                // ---- Enum values: ReserveWarmthRatio ----
                case "Off":
                    return zh ? "关闭" : "Off";
                case "Quarter":
                    return zh ? "1/4" : "1/4";
                case "Half":
                    return zh ? "1/2" : "1/2";
                case "ThreeQuarters":
                    return zh ? "3/4" : "3/4";
                default:
                    return null;
            }
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (entry?.Description == null || _descriptionBackingField == null) return;
            _descriptionBackingField.SetValue(entry.Description, text);
        }
    }
}
