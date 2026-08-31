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

        private static void ModConfigDisplayNamePostfix(object __instance, ref string __result)
        {
            ConfigEntryBase entry = TryGetConfigEntry(__instance);
            if (!IsWhereIsThingEntry(entry))
            {
                return;
            }

            string localized = GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key);
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
            string canonical = GetCanonicalToken(normalized);
            string direct = GetLocalizedToken(canonical);
            if (!string.IsNullOrEmpty(direct))
            {
                return direct;
            }

            if (normalized.IndexOf(',') >= 0)
            {
                string[] tokens = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] localizedTokens = tokens
                    .Select(GetCanonicalToken)
                    .Select(GetLocalizedToken)
                    .ToArray();
                if (localizedTokens.All(value => !string.IsNullOrEmpty(value)))
                {
                    return string.Join(IsChinese ? "、" : ", ", localizedTokens);
                }
            }

            return null;
        }

        private static string GetLocalizedConfigText(string section, string key)
        {
            string canonicalSection = GetCanonicalToken(section == null ? string.Empty : section.Replace(" ", string.Empty));
            string canonicalKey = GetCanonicalToken(key == null ? string.Empty : key.Replace(" ", string.Empty));
            if (string.IsNullOrEmpty(canonicalKey))
            {
                return null;
            }

            // Section is part of the stable identity. Keep the mapping scoped so a future
            // duplicate key in another section cannot be localized accidentally.
            if (!IsKnownConfigKey(canonicalSection, canonicalKey))
            {
                return null;
            }

            return GetLocalizedToken(canonicalKey);
        }

        private static bool IsKnownConfigKey(string section, string key)
        {
            switch (section)
            {
                case "General":
                    return key == "Enabled" || key == "ScanKey" || key == "WindowKey" ||
                        key == "ScanMode" || key == "DisplayDurationSeconds";
                case "Display":
                    return key == "NameLanguage" || key == "MaxDistance" || key == "FontSize" ||
                        key == "LabelFont" || key == "ShowOffscreenDirection" || key == "ShowOwnerNames";
                case "Presets":
                    return key == "PresetSchemaVersion" || key == "LocalPresets" ||
                        key == "ActiveLocalPresetId" || key == "SelectedSharedPresetId" || key == "ShareMode";
                case "Selection":
                    return key == "SelectedItemIds" || key == "SelectedLuggage" ||
                        key == "SelectedLuggageTypes" || key == "SelectedSceneTargetTypes" ||
                        key == "LocationScopes";
                default:
                    return false;
            }
        }

        private static string GetCanonicalToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            switch (value)
            {
                case "物品在哪": return "WhereIsThing";
                case "常规": return "General";
                case "显示": return "Display";
                case "选择": return "Selection";
                case "预设": return "Presets";
                case "启用MOD": return "Enabled";
                case "扫描快捷键": return "ScanKey";
                case "选择窗口快捷键": return "WindowKey";
                case "显示模式": return "ScanMode";
                case "定时显示秒数": return "DisplayDurationSeconds";
                case "名称语言": return "NameLanguage";
                case "最大距离": return "MaxDistance";
                case "标签字号": return "FontSize";
                case "标签字体": return "LabelFont";
                case "屏外方向提示": return "ShowOffscreenDirection";
                case "玩家名": return "ShowOwnerNames";
                case "已选物品ID": return "SelectedItemIds";
                case "兼容行李箱开关": return "SelectedLuggage";
                case "已选行李箱类型": return "SelectedLuggageTypes";
                case "已选场景目标": return "SelectedSceneTargetTypes";
                case "位置范围": return "LocationScopes";
                case "预设版本号": return "PresetSchemaVersion";
                case "本地预设数据": return "LocalPresets";
                case "当前本地预设": return "ActiveLocalPresetId";
                case "当前共享预设": return "SelectedSharedPresetId";
                case "房主共享模式": return "ShareMode";
                case "常驻": return "Persistent";
                case "定时": return "Timed";
                case "关闭": return "Off";
                case "仅默认预设": return "BuiltInOnly";
                case "已发布预设": return "PublishedPresets";
                case "跟随游戏": return "Game";
                case "简体中文": return "SimplifiedChinese";
                case "自动": return "Auto";
                case "游戏默认": return "GameDefault";
                case "TMP默认字体": return "TmpDefault";
                case "无": return "None";
                case "地面": return "Ground";
                case "手持": return "Held";
                case "背包": return "Backpack";
                case "行李箱": return "Luggage";
                default: return value;
            }
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
                case "Presets": return chinese ? "预设" : "Presets";
                case "Enabled": return chinese ? "启用 MOD" : "Enabled";
                case "ScanKey": return chinese ? "扫描快捷键" : "Scan Key";
                case "WindowKey": return chinese ? "选择窗口快捷键" : "Window Key";
                case "ScanMode": return chinese ? "显示模式" : "Display Mode";
                case "DisplayDurationSeconds": return chinese ? "定时显示秒数" : "Display Duration";
                case "NameLanguage": return chinese ? "名称语言" : "Name Language";
                case "MaxDistance": return chinese ? "最大距离" : "Max Distance";
                case "FontSize": return chinese ? "标签字号" : "Label Font Size";
                case "LabelFont": return chinese ? "标签字体" : "Label Font";
                case "ShowOffscreenDirection": return chinese ? "屏外方向提示" : "Off-screen Direction";
                case "ShowOwnerNames": return chinese ? "玩家名" : "Player Names";
                case "SelectedItemIds": return chinese ? "已选物品 ID" : "Selected Item IDs";
                case "SelectedLuggage": return chinese ? "兼容行李箱开关" : "Legacy Luggage Toggle";
                case "SelectedLuggageTypes": return chinese ? "已选行李箱类型" : "Selected Luggage Types";
                case "SelectedSceneTargetTypes": return chinese ? "已选场景目标" : "Selected Scene Targets";
                case "LocationScopes": return chinese ? "位置范围" : "Location Scopes";
                case "PresetSchemaVersion": return chinese ? "预设版本号" : "Preset Schema Version";
                case "LocalPresets": return chinese ? "本地预设数据" : "Local Presets";
                case "ActiveLocalPresetId": return chinese ? "当前本地预设" : "Active Local Preset";
                case "SelectedSharedPresetId": return chinese ? "当前共享预设" : "Selected Shared Preset";
                case "ShareMode": return chinese ? "房主共享模式" : "Share Mode";
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
                case "Off": return chinese ? "关闭" : "Off";
                case "BuiltInOnly": return chinese ? "仅默认预设" : "Built-in Only";
                case "PublishedPresets": return chinese ? "已发布预设" : "Published Presets";
                case "Game": return chinese ? "跟随游戏" : "Follow Game";
                case "English": return "English";
                case "SimplifiedChinese": return chinese ? "简体中文" : "Simplified Chinese";
                case "Auto": return chinese ? "自动" : "Auto";
                case "GameDefault": return chinese ? "游戏默认" : "Game Default";
                case "TmpDefault": return chinese ? "TMP 默认字体" : "TMP Default";
                case "KoreanBinggrae": return "KoreanBinggrae";
                case "Crazk": return "Crazk";
                case "None": return chinese ? "无" : "None";
                case "Ground": return chinese ? "地面" : "Ground";
                case "Held": return chinese ? "手持" : "Held";
                case "Backpack": return chinese ? "背包" : "Backpack";
                case "Luggage": return chinese ? "行李箱" : "Luggage";
                case "Statue": return chinese ? "雕像" : "Statue";
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
                case "LabelFont": return chinese ? "选择仅用于英文位置标签的字体；中文可能显示为口口口。" : "Choose a font for English location labels only. Chinese text may display as tofu boxes.";
                case "ShowOffscreenDirection": return chinese ? "目标在屏幕外时显示方向提示。" : "Show a direction indicator when a target is off-screen.";
                case "ShowOwnerNames": return chinese ? "在支持的目标后显示放置者玩家名。" : "Show the placer name after supported labels.";
                case "SelectedItemIds": return chinese ? "由 Alt+C 选择窗口维护的物品 ID，请勿手动编辑。" : "Item IDs managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedLuggage": return chinese ? "旧版配置兼容项，请使用选择窗口设置行李箱类型。" : "Legacy compatibility setting. Choose luggage types in the selection window.";
                case "SelectedLuggageTypes": return chinese ? "由 Alt+C 选择窗口维护的行李箱类型，请勿手动编辑。" : "Luggage types managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedSceneTargetTypes": return chinese ? "由 Alt+C 选择窗口维护的动态危险和钟塔类型，请勿手动编辑。" : "Scene target types managed by the Alt+C selection window. Do not edit manually.";
                case "LocationScopes": return chinese ? "目标位置范围。雕像范围会显示已选护符在场景雕像手中的碎片；行李箱是否扫描由 Alt+C 窗口中已选的行李箱类型自动控制。" : "Target location scopes. Statue scope shows selected amulet fragments held by scene statues. Luggage scanning is controlled automatically by the luggage types selected in the Alt+C window.";
                case "PresetSchemaVersion": return chinese ? "WhereIsThing 内部使用的预设数据版本，请勿手动编辑。" : "Internal preset schema version used by WhereIsThing. Do not edit manually.";
                case "LocalPresets": return chinese ? "WhereIsThing 保存的本地预设内容，请勿手动编辑。" : "Serialized local presets managed by WhereIsThing. Do not edit manually.";
                case "ActiveLocalPresetId": return chinese ? "当前本地玩家使用的预设 ID，请勿手动编辑。" : "Preset ID currently selected for local use. Do not edit manually.";
                case "SelectedSharedPresetId": return chinese ? "当前客户端选择的共享预设 ID，请勿手动编辑。" : "Shared preset ID currently selected by this client. Do not edit manually.";
                case "ShareMode": return chinese ? "房主是否向客机共享默认预设或已发布预设。" : "Whether the host shares built-in presets or published presets with clients.";
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
