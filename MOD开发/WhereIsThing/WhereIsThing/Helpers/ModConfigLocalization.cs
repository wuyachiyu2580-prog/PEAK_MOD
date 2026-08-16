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

namespace WhereIsThing
{
    internal static class ModConfigLocalization
    {
        private const string ModConfigGuid = "com.github.PEAKModding.PEAKLib.ModConfig";
        private const string ConfigFileName = "com.wuyachiyu.WhereIsThing.cfg";

        private static readonly FieldInfo DescriptionBackingField =
            typeof(ConfigDescription).GetField("<Description>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static Type _tmpTextType;
        private static PropertyInfo _tmpTextProperty;

        public static void PatchDisplayNames(Harmony harmony)
        {
            if (harmony == null || !Chainloader.PluginInfos.TryGetValue(ModConfigGuid, out PluginInfo pluginInfo) ||
                pluginInfo == null || pluginInfo.Instance == null)
            {
                return;
            }

            Assembly modConfigAssembly = pluginInfo.Instance.GetType().Assembly;
            HarmonyMethod displayNamePostfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(ModConfigDisplayNamePostfix), BindingFlags.Static | BindingFlags.NonPublic));

            foreach (Type type in modConfigAssembly.GetTypes())
            {
                if (type == null || type.IsAbstract || type.IsInterface || type.FullName == null ||
                    !type.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo getDisplayName = AccessTools.Method(type, "GetDisplayName", Type.EmptyTypes);
                if (getDisplayName != null)
                {
                    harmony.Patch(getDisplayName, null, displayNamePostfix);
                }
            }

            Type menuType = modConfigAssembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
            if (menuType == null)
            {
                return;
            }

            HarmonyMethod uiPostfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(ModConfigUiChangedPostfix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (string methodName in new[] { "OnEnable", "ShowSettings", "SetSection", "UpdateSectionTabs" })
            {
                MethodInfo method = menuType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(candidate => candidate.Name == methodName);
                if (method != null)
                {
                    harmony.Patch(method, null, uiPostfix);
                }
            }
        }

        public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (ConfigEntryBase entry in entries.Where(entry => entry != null))
            {
                SetDescription(entry, GetLocalizedDescription(entry.Definition.Key));
            }
        }

        public static void RefreshCache()
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(ModConfigGuid, out PluginInfo pluginInfo) ||
                    pluginInfo == null || pluginInfo.Instance == null)
                {
                    return;
                }

                Type modConfigType = pluginInfo.Instance.GetType();
                foreach (string propertyName in new[] { "EntriesProcessed", "ModdedKeys", "GetValidKeyPaths" })
                {
                    PropertyInfo property = modConfigType.GetProperty(propertyName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null && property.GetValue(null, null) is IList list)
                    {
                        list.Clear();
                    }
                }

                foreach (string methodName in new[] { "GenerateValidKeyPaths", "ProcessModEntries", "LoadModSettings" })
                {
                    MethodInfo method = modConfigType.GetMethod(methodName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            catch
            {
                // ModConfig is optional and may still be initializing.
            }
        }

        private static void ModConfigDisplayNamePostfix(object __instance, ref string __result)
        {
            ConfigEntryBase entry = TryGetConfigEntry(__instance);
            if (!IsWhereIsThingEntry(entry))
            {
                return;
            }

            string localized = GetLocalizedUiText(entry.Definition.Key);
            if (!string.IsNullOrEmpty(localized))
            {
                __result = localized;
            }
        }

        private static void ModConfigUiChangedPostfix(MonoBehaviour __instance)
        {
            if (__instance != null)
            {
                __instance.StartCoroutine(LocalizeModConfigUiDeferred(__instance.transform));
            }
        }

        private static IEnumerator LocalizeModConfigUiDeferred(Transform root)
        {
            yield return null;
            LocalizeTextInHierarchy(root);
        }

        private static void LocalizeTextInHierarchy(Transform root)
        {
            if (root == null)
            {
                return;
            }

            try
            {
                EnsureTmpReflection();
                if (_tmpTextType == null || _tmpTextProperty == null)
                {
                    return;
                }

                foreach (Component component in root.GetComponentsInChildren(_tmpTextType, true))
                {
                    string current = _tmpTextProperty.GetValue(component, null) as string;
                    string localized = GetLocalizedUiText(current);
                    if (!string.IsNullOrEmpty(localized) && localized != current)
                    {
                        _tmpTextProperty.SetValue(component, localized, null);
                    }
                }
            }
            catch
            {
                // Keep ModConfig usable even if its UI implementation changes.
            }
        }

        private static void EnsureTmpReflection()
        {
            if (_tmpTextType != null)
            {
                return;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _tmpTextType = assembly.GetType("TMPro.TextMeshProUGUI");
                if (_tmpTextType != null)
                {
                    _tmpTextProperty = _tmpTextType.GetProperty("text");
                    return;
                }
            }
        }

        private static string GetLocalizedUiText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string normalized = text.Replace(" ", string.Empty);
            string direct = GetLocalizedToken(normalized);
            if (!string.IsNullOrEmpty(direct))
            {
                return direct;
            }

            if (normalized.IndexOf(',') >= 0)
            {
                string[] tokens = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] localizedTokens = tokens.Select(GetLocalizedToken).ToArray();
                if (localizedTokens.All(value => !string.IsNullOrEmpty(value)))
                {
                    return string.Join(IsChinese ? "、" : ", ", localizedTokens);
                }
            }

            return null;
        }

        private static string GetLocalizedToken(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "WhereIsThing": return chinese ? "物品在哪" : "WhereIsThing";
                case "General": return chinese ? "常规" : "General";
                case "Display": return chinese ? "显示" : "Display";
                case "Selection": return chinese ? "选择" : "Selection";
                case "Enabled": return chinese ? "启用 MOD" : "Enabled";
                case "ScanKey": return chinese ? "扫描快捷键" : "Scan Key";
                case "WindowKey": return chinese ? "选择窗口快捷键" : "Window Key";
                case "ScanMode": return chinese ? "显示模式" : "Display Mode";
                case "DisplayDurationSeconds": return chinese ? "定时显示秒数" : "Display Duration";
                case "NameLanguage": return chinese ? "名称语言" : "Name Language";
                case "MaxDistance": return chinese ? "最大距离" : "Max Distance";
                case "FontSize": return chinese ? "标签字号" : "Label Font Size";
                case "ShowOffscreenDirection": return chinese ? "屏外方向提示" : "Off-screen Direction";
                case "SelectedItemIds": return chinese ? "已选物品 ID" : "Selected Item IDs";
                case "SelectedLuggage": return chinese ? "兼容行李箱开关" : "Legacy Luggage Toggle";
                case "SelectedLuggageTypes": return chinese ? "已选行李箱类型" : "Selected Luggage Types";
                case "SelectedSceneTargetTypes": return chinese ? "已选场景目标" : "Selected Scene Targets";
                case "LocationScopes": return chinese ? "位置范围" : "Location Scopes";
                case "MushroomZombie": return chinese ? "森蕈僵尸" : "Mushroom Zombie";
                case "Beetle": return chinese ? "甲虫" : "Beetle";
                case "Scorpion": return chinese ? "蝎子" : "Scorpion";
                case "Spider": return chinese ? "蜘蛛" : "Spider";
                case "BeeSwarm": return chinese ? "蜂群" : "Bee Swarm";
                case "Scoutmaster": return chinese ? "童军领队" : "Scoutmaster";
                case "TumbleWeed": return chinese ? "风滚草" : "Tumbleweed";
                case "GhostBall": return chinese ? "鬼球" : "Ghost Ball";
                case "SpikeTrap": return chinese ? "地刺" : "Spike Trap";
                case "Antlion": return chinese ? "蚁狮" : "Antlion";
                case "VenusFlyTrap": return chinese ? "捕蝇草" : "Venus Flytrap";
                case "Tornado": return chinese ? "龙卷风" : "Tornado";
                case "NapberryHypnoOrb": return chinese ? "未摘下的晚安莓" : "Unpicked Napberry";
                case "ArrowShooter": return chinese ? "箭矢发射器" : "Arrow Shooter";
                case "MovingSawBlade": return chinese ? "移动锯刃" : "Moving Sawblade";
                case "SpikeRoller": return chinese ? "滚刺机关" : "Spike Roller";
                case "SwingingAxe": return chinese ? "摆斧机关" : "Swinging Axe";
                case "GloomBellTower": return chinese ? "雾沼钟塔" : "Gloom Bell Tower";
                case "Persistent": return chinese ? "常驻" : "Persistent";
                case "Timed": return chinese ? "定时" : "Timed";
                case "Game": return chinese ? "跟随游戏" : "Follow Game";
                case "English": return "English";
                case "SimplifiedChinese": return chinese ? "简体中文" : "Simplified Chinese";
                case "None": return chinese ? "无" : "None";
                case "Ground": return chinese ? "地面" : "Ground";
                case "Held": return chinese ? "手持" : "Held";
                case "Backpack": return chinese ? "背包" : "Backpack";
                case "Luggage": return chinese ? "行李箱" : "Luggage";
                default: return null;
            }
        }

        private static string GetLocalizedDescription(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "Enabled": return chinese ? "启用或关闭 WhereIsThing。" : "Enable or disable WhereIsThing.";
                case "ScanKey": return chinese ? "显示当前已选目标位置的快捷键。" : "Key used to show the locations of selected targets.";
                case "WindowKey": return chinese ? "按住 Alt 后使用此键打开物品选择窗口。" : "Hold Alt and press this key to open the target selection window.";
                case "ScanMode": return chinese ? "选择位置标签常驻显示，或在数秒后自动隐藏。" : "Keep location labels visible or hide them automatically after a few seconds.";
                case "DisplayDurationSeconds": return chinese ? "定时模式下标签持续显示的秒数。" : "Number of seconds labels remain visible in Timed mode.";
                case "NameLanguage": return chinese ? "物品名称跟随游戏语言，或强制使用英文/简体中文。" : "Follow the game language for item names, or force English/Simplified Chinese.";
                case "MaxDistance": return chinese ? "显示标签的最大距离，单位米。0 表示不限制。" : "Maximum label distance in meters. Set to 0 for unlimited.";
                case "FontSize": return chinese ? "位置与距离标签的字号。" : "Font size used by location and distance labels.";
                case "ShowOffscreenDirection": return chinese ? "目标在屏幕外时显示方向提示。" : "Show a direction indicator when a target is off-screen.";
                case "SelectedItemIds": return chinese ? "由 Alt+C 选择窗口维护的物品 ID，请勿手动编辑。" : "Item IDs managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedLuggage": return chinese ? "旧版配置兼容项，请使用选择窗口设置行李箱类型。" : "Legacy compatibility setting. Choose luggage types in the selection window.";
                case "SelectedLuggageTypes": return chinese ? "由 Alt+C 选择窗口维护的行李箱类型，请勿手动编辑。" : "Luggage types managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedSceneTargetTypes": return chinese ? "由 Alt+C 选择窗口维护的动态危险和钟塔类型，请勿手动编辑。" : "Scene target types managed by the Alt+C selection window. Do not edit manually.";
                case "LocationScopes": return chinese ? "目标位置范围。行李箱是否扫描由 Alt+C 窗口中已选的行李箱类型自动控制。" : "Target location scopes. Luggage scanning is controlled automatically by the luggage types selected in the Alt+C window.";
                default: return string.Empty;
            }
        }

        private static ConfigEntryBase TryGetConfigEntry(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(ConfigEntryBase).IsAssignableFrom(field.FieldType))
                {
                    return field.GetValue(instance) as ConfigEntryBase;
                }
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(ConfigEntryBase).IsAssignableFrom(property.PropertyType) || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }
                try
                {
                    return property.GetValue(instance, null) as ConfigEntryBase;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool IsWhereIsThingEntry(ConfigEntryBase entry)
        {
            try
            {
                string path = entry == null || entry.ConfigFile == null ? null : entry.ConfigFile.ConfigFilePath;
                return !string.IsNullOrEmpty(path) && path.EndsWith(ConfigFileName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (entry == null || entry.Description == null || DescriptionBackingField == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            DescriptionBackingField.SetValue(entry.Description, text);
        }

        private static bool IsChinese
        {
            get
            {
                return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                    LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
            }
        }
    }
}
