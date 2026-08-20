using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class ModConfigLocalization
    {
        private const string ModConfigGuid = "com.github.PEAKModding.PEAKLib.ModConfig";
        private const string ConfigFileName = "com.players.info.cfg";

        private static readonly FieldInfo DescriptionBackingField =
            typeof(ConfigDescription).GetField("<Description>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static Type _tmpTextType;
        private static PropertyInfo _tmpTextProp;

        public static void PatchDisplayNames(Harmony harmony)
        {
            if (harmony == null) return;
            if (!Chainloader.PluginInfos.TryGetValue(ModConfigGuid, out var pluginInfo)) return;
            if (pluginInfo == null || pluginInfo.Instance == null) return;

            Assembly modConfigAssembly = pluginInfo.Instance.GetType().Assembly;
            var postfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(ModConfigDisplayNamePostfix),
                BindingFlags.Static | BindingFlags.NonPublic));

            foreach (Type type in modConfigAssembly.GetTypes())
            {
                if (type == null || type.IsAbstract || type.IsInterface) continue;
                if (type.FullName == null ||
                    !type.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal))
                    continue;

                MethodInfo getDisplayName = AccessTools.Method(type, "GetDisplayName", Type.EmptyTypes);
                if (getDisplayName != null) harmony.Patch(getDisplayName, null, postfix);
            }

            var uiPostfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(ModConfigUiChangedPostfix),
                BindingFlags.Static | BindingFlags.NonPublic));

            Type menuType = modConfigAssembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
            if (menuType == null) return;

            foreach (string methodName in new[] { "OnEnable", "ShowSettings", "SetSection", "UpdateSectionTabs" })
            {
                MethodInfo method = null;
                var methods = menuType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name == methodName)
                    {
                        method = methods[i];
                        break;
                    }
                }
                if (method != null) harmony.Patch(method, null, uiPostfix);
            }
        }

        public static void ApplyLocalizedDescriptions()
        {
            try
            {
                SetDescription(PlayersInfoPlugin.CfgModEnabled,
                    LanguageHelper.L("Master switch. Turn off to disable all PlayersInfo HUD features.",
                        "总开关。关闭后禁用 PlayersInfo 的所有 HUD 显示。"));
                SetDescription(PlayersInfoPlugin.CfgEnableStaminaBar,
                    LanguageHelper.L("Show teammate stamina bars. Turn this off to hide only teammate bars.",
                        "显示队友体力条。关闭后只隐藏队友条，不影响本地体力数字。"));
                SetDescription(PlayersInfoPlugin.CfgShowStaminaValue,
                    LanguageHelper.L("Show numeric stamina values on local and teammate bars.",
                        "在本地和队友体力条上显示数字。"));
                SetDescription(PlayersInfoPlugin.CfgShowExtraStaminaCap,
                    LanguageHelper.L("Show teammate extra stamina as current/cap. Off shows current only.",
                        "队友额外体力显示为 当前/上限。关闭后只显示当前值。"));
                SetDescription(PlayersInfoPlugin.CfgInventoryDisplayMode,
                    LanguageHelper.L("Choose whether teammate inventory is hidden, shows contents only, or also shows jetpack fuel.",
                        "选择隐藏队友物品栏、仅显示物品内容，或同时显示喷气背包燃料。"));
                SetDescription(PlayersInfoPlugin.CfgAnchor,
                    LanguageHelper.L("HUD anchor corner on screen.",
                        "HUD 在屏幕上的锚点角落。"));
                SetDescription(PlayersInfoPlugin.CfgOffsetX,
                    LanguageHelper.L("Additional horizontal offset in pixels.",
                        "额外水平偏移，单位像素。"));
                SetDescription(PlayersInfoPlugin.CfgOffsetY,
                    LanguageHelper.L("Additional vertical offset in pixels.",
                        "额外垂直偏移，单位像素。"));
                SetDescription(PlayersInfoPlugin.CfgNearbyRange,
                    LanguageHelper.L("Max distance in meters to show teammate bars. 0 = unlimited.",
                        "显示队友条的最大距离，单位米。0 表示不限距离。"));
                SetDescription(PlayersInfoPlugin.CfgMaxNearbyCount,
                    LanguageHelper.L("Maximum number of nearest teammates shown.",
                        "最多显示几个最近的队友。"));
                SetDescription(PlayersInfoPlugin.CfgTeammateSortMode,
                    LanguageHelper.L("Stable keeps bars in first-seen order; Distance follows current distance.",
                        "Stable 按首次出现顺序固定队友条；Distance 按当前距离排序。"));
                SetDescription(PlayersInfoPlugin.CfgSpectatorNearbyCenter,
                    LanguageHelper.L("While spectating, use the local character or the observed character as the nearby-player center.",
                        "观战时，选择以本机角色或被观看角色作为附近玩家中心。"));
                SetDescription(PlayersInfoPlugin.CfgRoundStamina,
                    LanguageHelper.L("Round stamina values to whole numbers. Off = one decimal place.",
                        "体力数字四舍五入为整数。关闭后显示一位小数。"));
                SetDescription(PlayersInfoPlugin.CfgDebugLogging,
                    LanguageHelper.L("Enable verbose diagnostic logs for troubleshooting.",
                        "启用详细诊断日志，用于排查问题。"));
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("modconfig_desc", "ApplyLocalizedDescriptions failed: " + ex.Message);
            }
        }

        public static void RefreshCache()
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(ModConfigGuid, out var pluginInfo)) return;
                if (pluginInfo == null || pluginInfo.Instance == null) return;

                Type modConfigType = pluginInfo.Instance.GetType();
                foreach (string propName in new[] { "EntriesProcessed", "ModdedKeys", "GetValidKeyPaths" })
                {
                    PropertyInfo prop = modConfigType.GetProperty(propName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.GetValue(null) is IList list) list.Clear();
                }

                foreach (string methodName in new[] { "GenerateValidKeyPaths", "ProcessModEntries", "LoadModSettings" })
                {
                    MethodInfo method = modConfigType.GetMethod(methodName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    method?.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("modconfig_refresh", "Refresh ModConfig cache failed: " + ex.Message);
            }
        }

        private static void ModConfigDisplayNamePostfix(object __instance, ref string __result)
        {
            ConfigEntryBase entry = TryGetConfigEntry(__instance);
            if (!IsPlayersInfoEntry(entry)) return;

            string localized = GetLocalizedEntryName(entry.Definition.Key);
            if (!string.IsNullOrEmpty(localized)) __result = localized;
        }

        private static void ModConfigUiChangedPostfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;
            __instance.StartCoroutine(LocalizeModConfigUiDeferred(__instance.transform));
        }

        private static IEnumerator LocalizeModConfigUiDeferred(Transform root)
        {
            yield return null;
            LocalizeTextInHierarchy(root);
        }

        private static void LocalizeTextInHierarchy(Transform root)
        {
            if (root == null) return;
            try
            {
                EnsureTmpReflection();
                if (_tmpTextType == null || _tmpTextProp == null) return;

                Component[] texts = root.GetComponentsInChildren(_tmpTextType, true);
                for (int i = 0; i < texts.Length; i++)
                {
                    string current = _tmpTextProp.GetValue(texts[i]) as string;
                    if (string.IsNullOrEmpty(current)) continue;

                    string localized = GetLocalizedUiText(current);
                    if (!string.IsNullOrEmpty(localized) && localized != current)
                        _tmpTextProp.SetValue(texts[i], localized);
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("modconfig_ui_loc", "ModConfig UI localization failed: " + ex.Message);
            }
        }

        private static void EnsureTmpReflection()
        {
            if (_tmpTextType != null) return;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                _tmpTextType = assemblies[i].GetType("TMPro.TextMeshProUGUI");
                if (_tmpTextType == null) continue;
                _tmpTextProp = _tmpTextType.GetProperty("text");
                return;
            }
        }

        private static string GetLocalizedUiText(string text)
        {
            string normalized = text.Replace(" ", string.Empty);
            if (normalized == "PlayersInfo") return LanguageHelper.IsChinese ? "队友信息" : "PlayersInfo";
            if (normalized == "Display") return LanguageHelper.IsChinese ? "显示" : "Display";
            if (normalized == "Advanced") return LanguageHelper.IsChinese ? "进阶" : "Advanced";
            return GetLocalizedEntryName(normalized);
        }

        private static string GetLocalizedEntryName(string key)
        {
            bool zh = LanguageHelper.IsChinese;
            switch (key)
            {
                case "Enabled": return zh ? "启用 MOD" : "Enabled";
                case "EnableStaminaBar": return zh ? "队友体力条" : "Teammate Bars";
                case "ShowStaminaValue": return zh ? "显示体力数字" : "Stamina Values";
                case "ShowExtraStaminaCap": return zh ? "显示额外体力上限" : "Extra Stamina Cap";
                case "EnableInventoryRow": return zh ? "队友物品栏显示" : "Teammate Inventory Display";
                case "Anchor": return zh ? "HUD 锚点" : "HUD Anchor";
                case "OffsetX": return zh ? "水平偏移" : "Offset X";
                case "OffsetY": return zh ? "垂直偏移" : "Offset Y";
                case "NearbyRange": return zh ? "显示距离" : "Nearby Range";
                case "MaxNearbyCount": return zh ? "最多显示人数" : "Max Teammates";
                case "TeammateSortMode": return zh ? "队友条排序" : "Teammate Bar Order";
                case "SpectatorNearbyCenter": return zh ? "观战附近中心" : "Spectator Nearby Center";
                case "LocalCharacter": return zh ? "本机角色" : "Local Character";
                case "ObservedCharacter": return zh ? "被观看角色" : "Observed Character";
                case "Disabled": return zh ? "不显示" : "Disabled";
                case "ContentsOnly": return zh ? "仅显示物品内容" : "Contents Only";
                case "ContentsAndJetpackFuel": return zh ? "物品内容与喷气背包燃料" : "Contents and Jetpack Fuel";
                case "ContentsandJetpackFuel": return zh ? "物品内容与喷气背包燃料" : "Contents and Jetpack Fuel";
                case "RoundStaminaValue": return zh ? "体力取整" : "Round Stamina";
                case "DebugLogging": return zh ? "诊断日志" : "Debug Logging";
                case "TopLeft": return zh ? "左上" : "Top Left";
                case "TopRight": return zh ? "右上" : "Top Right";
                case "BottomLeft": return zh ? "左下" : "Bottom Left";
                case "BottomRight": return zh ? "右下" : "Bottom Right";
                case "Stable": return zh ? "固定顺序" : "Stable";
                case "Distance": return zh ? "按距离" : "Distance";
                default: return null;
            }
        }

        private static ConfigEntryBase TryGetConfigEntry(object instance)
        {
            if (instance == null) return null;
            Type type = instance.GetType();

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (typeof(ConfigEntryBase).IsAssignableFrom(fields[i].FieldType))
                    return fields[i].GetValue(instance) as ConfigEntryBase;
            }

            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < props.Length; i++)
            {
                if (!typeof(ConfigEntryBase).IsAssignableFrom(props[i].PropertyType)) continue;
                if (props[i].GetIndexParameters().Length != 0) continue;
                try { return props[i].GetValue(instance, null) as ConfigEntryBase; }
                catch { }
            }

            return null;
        }

        private static bool IsPlayersInfoEntry(ConfigEntryBase entry)
        {
            try
            {
                string path = entry?.ConfigFile?.ConfigFilePath;
                return !string.IsNullOrEmpty(path) &&
                    path.EndsWith(ConfigFileName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (entry?.Description == null || DescriptionBackingField == null) return;
            DescriptionBackingField.SetValue(entry.Description, text);
        }
    }
}

