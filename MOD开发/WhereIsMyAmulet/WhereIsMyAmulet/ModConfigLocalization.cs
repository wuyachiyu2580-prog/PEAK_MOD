using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace WhereIsMyAmulet
{
    internal static class ModConfigLocalization
    {
        private const string ModConfigGuid = "com.github.PEAKModding.PEAKLib.ModConfig";
        private const string ConfigFileName = "com.wuyachiyu.WhereIsMyAmulet.cfg";
        private static readonly FieldInfo DescriptionBackingField = typeof(ConfigDescription).GetField(
            "<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Type _tmpTextType;
        private static PropertyInfo _tmpTextProperty;
        private static MonoBehaviour _activeMenu;

        public static void PatchDisplayNames(Harmony harmony)
        {
            if (harmony == null || !Chainloader.PluginInfos.TryGetValue(ModConfigGuid, out BepInEx.PluginInfo info) ||
                info == null || info.Instance == null)
            {
                return;
            }

            Assembly assembly = info.Instance.GetType().Assembly;
            HarmonyMethod displayPostfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(DisplayNamePostfix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (Type type in assembly.GetTypes())
            {
                if (type == null || type.FullName == null || type.IsAbstract || type.IsInterface ||
                    !type.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo method = AccessTools.Method(type, "GetDisplayName", Type.EmptyTypes);
                if (method != null)
                {
                    harmony.Patch(method, null, displayPostfix);
                }
            }

            Type menuType = assembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
            if (menuType == null)
            {
                return;
            }

            HarmonyMethod menuPostfix = new HarmonyMethod(typeof(ModConfigLocalization).GetMethod(
                nameof(MenuChangedPostfix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (string methodName in new[] { "OnEnable", "ShowSettings", "SetSection", "UpdateSectionTabs" })
            {
                MethodInfo method = menuType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(candidate => candidate.Name == methodName);
                if (method != null)
                {
                    harmony.Patch(method, null, menuPostfix);
                }
            }
        }

        public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (ConfigEntryBase entry in entries.Where(value => value != null))
            {
                SetDescription(entry, GetLocalizedDescription(entry.Definition.Section, entry.Definition.Key));
            }
        }

        public static void RefreshVisibleUi()
        {
            if (_activeMenu != null && _activeMenu.gameObject != null && _activeMenu.gameObject.activeInHierarchy)
            {
                _activeMenu.StartCoroutine(RefreshVisibleUiDeferred(_activeMenu.transform));
            }
        }

        private static IEnumerator RefreshVisibleUiDeferred(Transform root)
        {
            yield return null;
            LocalizeTextInHierarchy(root);
        }

        private static void DisplayNamePostfix(object __instance, ref string __result)
        {
            ConfigEntryBase entry = TryGetConfigEntry(__instance);
            if (!IsOwnEntry(entry))
            {
                return;
            }

            string localized = GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key);
            if (!string.IsNullOrEmpty(localized))
            {
                __result = localized;
            }
        }

        private static void MenuChangedPostfix(MonoBehaviour __instance)
        {
            _activeMenu = __instance;
            if (__instance != null)
            {
                __instance.StartCoroutine(RefreshVisibleUiDeferred(__instance.transform));
            }
        }

        private static void LocalizeTextInHierarchy(Transform root)
        {
            if (root == null)
            {
                return;
            }

            EnsureTmpReflection();
            if (_tmpTextType == null || _tmpTextProperty == null)
            {
                return;
            }

            foreach (Component component in root.GetComponentsInChildren(_tmpTextType, false))
            {
                string current = _tmpTextProperty.GetValue(component, null) as string;
                string localized = GetLocalizedUiText(current);
                if (!string.IsNullOrEmpty(localized) && localized != current)
                {
                    _tmpTextProperty.SetValue(component, localized, null);
                }
            }
        }

        private static void EnsureTmpReflection()
        {
            if (_tmpTextType != null)
            {
                return;
            }

            _tmpTextType = typeof(TextMeshProUGUI);
            _tmpTextProperty = _tmpTextType.GetProperty("text");
        }

        private static string GetLocalizedUiText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string canonical = GetCanonicalToken(text.Replace(" ", string.Empty));
            string localized = GetLocalizedToken(canonical);
            if (!string.IsNullOrEmpty(localized))
            {
                return localized;
            }

            string description = GetLocalizedDescriptionFromRenderedText(text);
            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }

            if (text.IndexOf(',') >= 0)
            {
                string[] values = text.Replace(" ", string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(GetCanonicalToken).Select(GetLocalizedToken).ToArray();
                if (values.All(value => !string.IsNullOrEmpty(value)))
                {
                    return string.Join(IsChinese ? "、" : ", ", values);
                }
            }

            return null;
        }

        private static string GetLocalizedDescriptionFromRenderedText(string text)
        {
            string[] keys = { "Enabled", "ScanKey", "ScanMode", "DisplayDurationSeconds", "MaxDistance", "FontSize", "LabelFont", "ShowStatueFragments", "ShowOffscreenDirection" };
            foreach (string key in keys)
            {
                string section = key == "Enabled" || key == "ScanKey" || key == "ScanMode" || key == "DisplayDurationSeconds" ? "General" : "Display";
                string english = GetDescription(section, key, false);
                string chinese = GetDescription(section, key, true);
                if (string.Equals(text, english, StringComparison.Ordinal) || string.Equals(text, chinese, StringComparison.Ordinal))
                {
                    return IsChinese ? chinese : english;
                }
            }
            return null;
        }

        private static string GetLocalizedDescription(string section, string key)
        {
            return GetDescription(section, key, IsChinese);
        }

        private static string GetLocalizedConfigText(string section, string key)
        {
            if (!IsKnownConfigKey(section, key))
            {
                return null;
            }
            return GetLocalizedToken(key);
        }

        private static bool IsKnownConfigKey(string section, string key)
        {
            if (section == "General") return key == "Enabled" || key == "ScanKey" || key == "ScanMode" || key == "DisplayDurationSeconds";
            if (section == "Display") return key == "MaxDistance" || key == "FontSize" || key == "LabelFont" || key == "ShowStatueFragments" || key == "ShowOffscreenDirection";
            return false;
        }

        private static string GetDescription(string section, string key, bool chinese)
        {
            if (section == "General")
            {
                switch (key)
                {
                    case "Enabled": return chinese ? "启用或关闭 WhereIsMyAmulet。" : "Enable or disable WhereIsMyAmulet.";
                    case "ScanKey": return chinese ? "扫描掉落护符和雕像碎片的快捷键。" : "Key used to scan dropped amulets and statue fragments.";
                    case "ScanMode": return chinese ? "选择标签常驻显示，或在扫描后按设定时间自动隐藏。" : "Keep labels visible, or hide them automatically after the configured duration.";
                    case "DisplayDurationSeconds": return chinese ? "定时模式下标签持续显示的秒数。" : "Number of seconds labels remain visible in Timed mode.";
                }
            }
            if (section == "Display")
            {
                switch (key)
                {
                    case "MaxDistance": return chinese ? "标签显示的最大距离，单位米。0 表示不限制。" : "Maximum label distance in metres. Set to 0 for unlimited.";
                    case "FontSize": return chinese ? "位置标签的字号。" : "Font size used by location labels.";
                    case "LabelFont": return chinese ? "选择仅用于英文位置标签的字体；中文可能显示为口口口。" : "Choose a font for English location labels only. Chinese text may display as tofu boxes.";
                    case "ShowStatueFragments": return chinese ? "显示场景雕像手中的护符碎片，以及祭坛上已经镶嵌的护符碎片。" : "Show amulet fragments held by amulet statues and fragments mounted on Scout Statues.";
                    case "ShowOffscreenDirection": return chinese ? "目标在屏幕外时显示方向提示。" : "Show a direction indicator when a target is off-screen.";
                }
            }
            return string.Empty;
        }

        private static string GetLocalizedToken(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "WhereIsMyAmulet": return chinese ? "护符在哪里" : "WhereIsMyAmulet";
                case "General": return chinese ? "常规" : "General";
                case "Display": return chinese ? "显示" : "Display";
                case "Enabled": return chinese ? "启用 MOD" : "Enabled";
                case "ScanKey": return chinese ? "扫描快捷键" : "Scan Key";
                case "ScanMode": return chinese ? "显示模式" : "Scan Mode";
                case "DisplayDurationSeconds": return chinese ? "定时显示秒数" : "Display Duration";
                case "MaxDistance": return chinese ? "最大距离" : "Max Distance";
                case "FontSize": return chinese ? "标签字号" : "Label Font Size";
                case "LabelFont": return chinese ? "标签字体" : "Label Font";
                case "ShowStatueFragments": return chinese ? "显示雕像碎片" : "Show Statue Fragments";
                case "ShowOffscreenDirection": return chinese ? "屏外方向提示" : "Off-screen Direction";
                case "Auto": return chinese ? "自动" : "Auto";
                case "GameDefault": return chinese ? "游戏默认" : "Game Default";
                case "TmpDefault": return chinese ? "TMP 默认字体" : "TMP Default";
                case "Persistent": return chinese ? "常驻" : "Persistent";
                case "Timed": return chinese ? "定时" : "Timed";
                case "KoreanBinggrae": return chinese ? "KoreanBinggrae" : "KoreanBinggrae";
                case "Crazk": return chinese ? "Crazk" : "Crazk";
                default: return null;
            }
        }

        private static string GetCanonicalToken(string value)
        {
            switch (value)
            {
                case "护符在哪里": return "WhereIsMyAmulet";
                case "常规": return "General";
                case "显示": return "Display";
                case "启用MOD": return "Enabled";
                case "扫描快捷键": return "ScanKey";
                case "最大距离": return "MaxDistance";
                case "标签字号": return "FontSize";
                case "标签字体": return "LabelFont";
                case "显示雕像碎片": return "ShowStatueFragments";
                case "屏外方向提示": return "ShowOffscreenDirection";
                case "显示模式": return "ScanMode";
                case "定时显示秒数": return "DisplayDurationSeconds";
                case "自动": return "Auto";
                case "游戏默认": return "GameDefault";
                case "TMP默认字体": return "TmpDefault";
                default: return value;
            }
        }

        private static ConfigEntryBase TryGetConfigEntry(object instance)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(ConfigEntryBase).IsAssignableFrom(field.FieldType)) return field.GetValue(instance) as ConfigEntryBase;
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(ConfigEntryBase).IsAssignableFrom(property.PropertyType) || property.GetIndexParameters().Length != 0) continue;
                try { return property.GetValue(instance, null) as ConfigEntryBase; } catch { return null; }
            }
            return null;
        }

        private static bool IsOwnEntry(ConfigEntryBase entry)
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
            if (entry == null || entry.Description == null || DescriptionBackingField == null || string.IsNullOrEmpty(text)) return;
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
